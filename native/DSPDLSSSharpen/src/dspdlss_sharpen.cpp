#include <Windows.h>
#include <d3d11.h>

#include <cmath>
#include <cstdio>
#include <mutex>
#include <new>
#include <string>

#include "Unity/IUnityGraphics.h"
#include "gaussian_usm_cs.h"

// D3D11 bridge for optimized 3x3 Gaussian unsharp masking.
//
// This pass reads only the completed DLSS output. Four bilinear samples
// reconstruct the Gaussian kernel and a fifth center load supplies the USM
// term. A separate scratch texture prevents simultaneous SRV/UAV binding.

namespace
{
struct SharpenEventData
{
    uint64_t sequence;
    void* output;
    float strength;
    uint32_t width;
    uint32_t height;
    uint32_t reserved;
};

struct SharpenResultData
{
    uint64_t sequence;
    int32_t result;
    uint32_t consecutiveFailures;
};

struct SharpenPacket
{
    uint64_t sequence{};
    ID3D11Texture2D* output{};
    float strength{};
    uint32_t width{};
    uint32_t height{};
};

static_assert(sizeof(SharpenEventData) == 32, "Managed/native SharpenEventData layout mismatch");
static_assert(sizeof(SharpenResultData) == 16, "Managed/native SharpenResultData layout mismatch");

struct SharpenConstants
{
    float invOutputWidth;
    float invOutputHeight;
    float strength;
    float padding;
};
static_assert(sizeof(SharpenConstants) == 16, "Unexpected Gaussian USM constant layout");

std::mutex g_resultMutex;
SharpenResultData g_latestResult{};
uint32_t g_consecutiveFailures{};
std::string g_status{"Gaussian USM loaded; waiting for the first render event"};
bool g_reportedFirstSuccess{};

ID3D11Device* g_device{};
ID3D11DeviceContext* g_context{};
ID3D11ComputeShader* g_shader{};
ID3D11Buffer* g_constants{};
ID3D11SamplerState* g_linearSampler{};

ID3D11Texture2D* g_outputOwner{};
ID3D11ShaderResourceView* g_outputSrv{};
ID3D11Texture2D* g_scratch{};
ID3D11UnorderedAccessView* g_scratchUav{};

struct PipelineKey
{
    ID3D11Texture2D* output{};
    uint32_t width{};
    uint32_t height{};
    DXGI_FORMAT format{DXGI_FORMAT_UNKNOWN};
    float strength{};
};

PipelineKey g_pipelineKey{};
bool g_pipelineKeyValid{};

constexpr int32_t kSuccess = 0;
constexpr int32_t kInvalidEvent = -1;
constexpr int32_t kInvalidInput = -2;
constexpr int32_t kDeviceFailure = -3;
constexpr int32_t kPipelineFailure = -4;
constexpr uint32_t kBlockWidth = 8;
constexpr uint32_t kBlockHeight = 8;

void PublishResult(uint64_t sequence, int32_t result, const std::string& status)
{
    std::lock_guard<std::mutex> lock(g_resultMutex);
    if (result == kSuccess)
        g_consecutiveFailures = 0;
    else
        ++g_consecutiveFailures;
    g_latestResult = {sequence, result, g_consecutiveFailures};
    if (!status.empty())
        g_status = status;
}

DXGI_FORMAT ViewFormat(DXGI_FORMAT format)
{
    switch (format)
    {
    case DXGI_FORMAT_R16G16B16A16_TYPELESS: return DXGI_FORMAT_R16G16B16A16_FLOAT;
    case DXGI_FORMAT_R32_TYPELESS: return DXGI_FORMAT_R32_FLOAT;
    case DXGI_FORMAT_R8G8B8A8_TYPELESS: return DXGI_FORMAT_R8G8B8A8_UNORM;
    case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB: return DXGI_FORMAT_R8G8B8A8_UNORM;
    case DXGI_FORMAT_B8G8R8A8_TYPELESS: return DXGI_FORMAT_B8G8R8A8_UNORM;
    case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB: return DXGI_FORMAT_B8G8R8A8_UNORM;
    case DXGI_FORMAT_R16G16_TYPELESS: return DXGI_FORMAT_R16G16_FLOAT;
    default: return format;
    }
}

void ReleaseViews()
{
    if (g_outputSrv) { g_outputSrv->Release(); g_outputSrv = nullptr; }
    if (g_outputOwner) { g_outputOwner->Release(); g_outputOwner = nullptr; }
}

void ReleaseScratch()
{
    if (g_scratchUav) { g_scratchUav->Release(); g_scratchUav = nullptr; }
    if (g_scratch) { g_scratch->Release(); g_scratch = nullptr; }
}

void ReleaseDeviceObjects()
{
    ReleaseViews();
    ReleaseScratch();
    if (g_shader) { g_shader->Release(); g_shader = nullptr; }
    if (g_constants) { g_constants->Release(); g_constants = nullptr; }
    if (g_linearSampler) { g_linearSampler->Release(); g_linearSampler = nullptr; }
    if (g_context) { g_context->Release(); g_context = nullptr; }
    if (g_device) { g_device->Release(); g_device = nullptr; }
    g_pipelineKey = {};
    g_pipelineKeyValid = false;
}

bool SamePipelineKey(const PipelineKey& left, const PipelineKey& right)
{
    return left.output == right.output && left.width == right.width &&
        left.height == right.height && left.format == right.format &&
        left.strength == right.strength;
}

bool EnsureShader(std::string& status)
{
    if (g_shader)
        return true;
    const HRESULT hr = g_device->CreateComputeShader(
        g_gaussian_usm_cs, sizeof(g_gaussian_usm_cs), nullptr, &g_shader);
    if (FAILED(hr) || !g_shader)
    {
        status = "Gaussian USM compute shader creation failed";
        return false;
    }
    return true;
}

bool EnsurePipeline(SharpenPacket* packet, std::string& status)
{
    D3D11_TEXTURE2D_DESC outputDesc{};
    packet->output->GetDesc(&outputDesc);
    if (outputDesc.Width != packet->width || outputDesc.Height != packet->height)
    {
        status = "DLSS output dimensions do not match the event";
        return false;
    }
    if (outputDesc.SampleDesc.Count != 1)
    {
        status = "multisampled DLSS output is unsupported";
        return false;
    }

    const DXGI_FORMAT viewFormat = ViewFormat(outputDesc.Format);
    if (viewFormat == DXGI_FORMAT_UNKNOWN)
    {
        status = "DLSS output has no typed SRV/UAV format";
        return false;
    }

    PipelineKey key{packet->output, outputDesc.Width, outputDesc.Height,
        outputDesc.Format, packet->strength};
    const bool resourcesChanged = !g_pipelineKeyValid ||
        g_pipelineKey.output != key.output || g_pipelineKey.width != key.width ||
        g_pipelineKey.height != key.height || g_pipelineKey.format != key.format;
    if (resourcesChanged)
    {
        g_pipelineKeyValid = false;
        ReleaseViews();
        ReleaseScratch();

        D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc{};
        srvDesc.Format = viewFormat;
        srvDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
        srvDesc.Texture2D.MipLevels = 1;
        if (FAILED(g_device->CreateShaderResourceView(packet->output, &srvDesc, &g_outputSrv)))
        {
            status = "DLSS output SRV creation failed";
            return false;
        }

        D3D11_TEXTURE2D_DESC scratchDesc = outputDesc;
        scratchDesc.MipLevels = 1;
        scratchDesc.ArraySize = 1;
        scratchDesc.SampleDesc.Count = 1;
        scratchDesc.SampleDesc.Quality = 0;
        scratchDesc.Usage = D3D11_USAGE_DEFAULT;
        scratchDesc.BindFlags = D3D11_BIND_UNORDERED_ACCESS;
        scratchDesc.CPUAccessFlags = 0;
        scratchDesc.MiscFlags = 0;
        if (FAILED(g_device->CreateTexture2D(&scratchDesc, nullptr, &g_scratch)))
        {
            status = "Gaussian USM scratch texture allocation failed";
            ReleaseViews();
            return false;
        }

        D3D11_UNORDERED_ACCESS_VIEW_DESC uavDesc{};
        uavDesc.Format = viewFormat;
        uavDesc.ViewDimension = D3D11_UAV_DIMENSION_TEXTURE2D;
        if (FAILED(g_device->CreateUnorderedAccessView(g_scratch, &uavDesc, &g_scratchUav)))
        {
            status = "Gaussian USM scratch UAV creation failed";
            ReleaseViews();
            ReleaseScratch();
            return false;
        }

        g_outputOwner = packet->output;
        g_outputOwner->AddRef();
    }

    if (!EnsureShader(status))
        return false;

    if (!g_linearSampler)
    {
        D3D11_SAMPLER_DESC samplerDesc{};
        samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
        samplerDesc.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDesc.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
        samplerDesc.MaxLOD = D3D11_FLOAT32_MAX;
        if (FAILED(g_device->CreateSamplerState(&samplerDesc, &g_linearSampler)))
        {
            status = "Gaussian USM linear-clamp sampler creation failed";
            return false;
        }
    }

    if (!g_constants)
    {
        D3D11_BUFFER_DESC desc{};
        desc.ByteWidth = sizeof(SharpenConstants);
        desc.Usage = D3D11_USAGE_DYNAMIC;
        desc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
        desc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
        if (FAILED(g_device->CreateBuffer(&desc, nullptr, &g_constants)))
        {
            status = "Gaussian USM constant buffer creation failed";
            return false;
        }
    }

    if (!g_pipelineKeyValid || !SamePipelineKey(g_pipelineKey, key))
    {
        const SharpenConstants config{
            1.0f / static_cast<float>(packet->width),
            1.0f / static_cast<float>(packet->height),
            packet->strength,
            0.0f};

        D3D11_MAPPED_SUBRESOURCE mapped{};
        if (FAILED(g_context->Map(g_constants, 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped)))
        {
            status = "Gaussian USM constant buffer update failed";
            return false;
        }
        *static_cast<SharpenConstants*>(mapped.pData) = config;
        g_context->Unmap(g_constants, 0);
        g_pipelineKey = key;
        g_pipelineKeyValid = true;
    }
    return true;
}

bool EnsureDevice(SharpenPacket* packet, std::string& status)
{
    ID3D11Device* outputDevice{};
    packet->output->GetDevice(&outputDevice);
    if (!outputDevice)
    {
        status = "could not obtain the DLSS output D3D11 device";
        return false;
    }

    if (outputDevice != g_device)
    {
        ReleaseDeviceObjects();
        g_device = outputDevice;
        g_device->GetImmediateContext(&g_context);
        if (!g_context)
        {
            status = "could not obtain the D3D11 immediate context";
            ReleaseDeviceObjects();
            return false;
        }
    }
    else
    {
        outputDevice->Release();
    }
    return true;
}

void RunSharpen(SharpenPacket* packet)
{
    ID3D11ComputeShader* oldShader{};
    ID3D11ClassInstance* oldClassInstances[D3D11_SHADER_MAX_INTERFACES]{};
    UINT oldClassInstanceCount = D3D11_SHADER_MAX_INTERFACES;
    ID3D11ShaderResourceView* oldSrvs[D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT]{};
    ID3D11UnorderedAccessView* oldUavs[D3D11_PS_CS_UAV_REGISTER_COUNT]{};
    ID3D11Buffer* oldConstants{};
    ID3D11SamplerState* oldSampler{};
    g_context->CSGetShader(&oldShader, oldClassInstances, &oldClassInstanceCount);
    g_context->CSGetShaderResources(0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT, oldSrvs);
    g_context->CSGetUnorderedAccessViews(0, D3D11_PS_CS_UAV_REGISTER_COUNT, oldUavs);
    g_context->CSGetConstantBuffers(0, 1, &oldConstants);
    g_context->CSGetSamplers(0, 1, &oldSampler);

    ID3D11UnorderedAccessView* noUavs[D3D11_PS_CS_UAV_REGISTER_COUNT]{};
    g_context->CSSetUnorderedAccessViews(0, D3D11_PS_CS_UAV_REGISTER_COUNT, noUavs, nullptr);
    g_context->CSSetShader(g_shader, nullptr, 0);
    g_context->CSSetShaderResources(0, 1, &g_outputSrv);
    g_context->CSSetUnorderedAccessViews(0, 1, &g_scratchUav, nullptr);
    g_context->CSSetConstantBuffers(0, 1, &g_constants);
    g_context->CSSetSamplers(0, 1, &g_linearSampler);
    g_context->Dispatch(
        (packet->width + kBlockWidth - 1) / kBlockWidth,
        (packet->height + kBlockHeight - 1) / kBlockHeight,
        1);

    // The copy is performed only after all plugin SRV/UAV bindings are gone.
    // Restore the complete CS state that was cleared for the copy transaction.
    ID3D11ShaderResourceView* noSrvs[D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT]{};
    g_context->CSSetShaderResources(0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT, noSrvs);
    g_context->CSSetUnorderedAccessViews(0, D3D11_PS_CS_UAV_REGISTER_COUNT, noUavs, nullptr);
    g_context->CopyResource(packet->output, g_scratch);

    g_context->CSSetShader(oldShader, oldClassInstances, oldClassInstanceCount);
    g_context->CSSetShaderResources(0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT, oldSrvs);
    g_context->CSSetUnorderedAccessViews(0, D3D11_PS_CS_UAV_REGISTER_COUNT, oldUavs, nullptr);
    g_context->CSSetConstantBuffers(0, 1, &oldConstants);
    g_context->CSSetSamplers(0, 1, &oldSampler);

    if (oldShader) oldShader->Release();
    for (UINT i = 0; i < oldClassInstanceCount; ++i)
        if (oldClassInstances[i]) oldClassInstances[i]->Release();
    for (ID3D11ShaderResourceView* srv : oldSrvs)
        if (srv) srv->Release();
    for (ID3D11UnorderedAccessView* uav : oldUavs)
        if (uav) uav->Release();
    if (oldConstants) oldConstants->Release();
    if (oldSampler) oldSampler->Release();
}

void ReleasePacket(SharpenPacket* packet)
{
    if (!packet)
        return;
    if (packet->output) packet->output->Release();
    delete packet;
}

bool QueryTexture(void* pointer, ID3D11Texture2D** texture)
{
    *texture = nullptr;
    if (!pointer)
        return false;
    IUnknown* unknown = static_cast<IUnknown*>(pointer);
    return SUCCEEDED(unknown->QueryInterface(__uuidof(ID3D11Texture2D),
        reinterpret_cast<void**>(texture))) && *texture;
}

void UNITY_INTERFACE_API OnRenderEventAndData(int eventId, void* data)
{
    SharpenPacket* packet = static_cast<SharpenPacket*>(data);
    if (!packet)
        return;

    int32_t result = kSuccess;
    std::string status;
    if (eventId != 1)
    {
        result = kInvalidEvent;
        status = "unexpected Gaussian USM render event id";
    }
    else if (!packet->output || !packet->width || !packet->height ||
        !std::isfinite(packet->strength) || packet->strength <= 0.0f ||
        packet->strength > 1.0f)
    {
        result = kInvalidInput;
        status = "incomplete Gaussian USM output, dimensions, or strength";
    }
    else if (!EnsureDevice(packet, status))
    {
        result = kDeviceFailure;
    }
    else if (!EnsurePipeline(packet, status))
    {
        result = kPipelineFailure;
    }
    else
    {
        RunSharpen(packet);
        if (!g_reportedFirstSuccess)
        {
            g_reportedFirstSuccess = true;
            char message[256]{};
            std::snprintf(message, sizeof(message),
                "Gaussian USM first dispatch ok: %ux%u strength=%.3f sequence=%llu",
                packet->width, packet->height, packet->strength,
                static_cast<unsigned long long>(packet->sequence));
            status = message;
        }
    }

    const uint64_t sequence = packet->sequence;
    ReleasePacket(packet);
    PublishResult(sequence, result, status);
}
}

extern "C" __declspec(dllexport) UnityRenderingEventAndData __stdcall DSPDLSSSharpenGetRenderEventFunc()
{
    return OnRenderEventAndData;
}

extern "C" __declspec(dllexport) void* __stdcall DSPDLSSSharpenCreateEventData(const SharpenEventData* parameters)
{
    if (!parameters || parameters->sequence == 0 || !parameters->output ||
        !parameters->width || !parameters->height || !std::isfinite(parameters->strength) ||
        parameters->strength <= 0.0f || parameters->strength > 1.0f)
        return nullptr;

    SharpenPacket* packet = new (std::nothrow) SharpenPacket();
    if (!packet)
        return nullptr;
    packet->sequence = parameters->sequence;
    packet->strength = parameters->strength;
    packet->width = parameters->width;
    packet->height = parameters->height;

    // QueryInterface is the explicit D3D11 resource gate. D3D12 textures and
    // unrelated Unity native pointers are rejected before the render callback.
    if (!QueryTexture(parameters->output, &packet->output))
    {
        ReleasePacket(packet);
        return nullptr;
    }
    return packet;
}

extern "C" __declspec(dllexport) void __stdcall DSPDLSSSharpenReleaseEventData(void* eventData)
{
    ReleasePacket(static_cast<SharpenPacket*>(eventData));
}

extern "C" __declspec(dllexport) int __stdcall DSPDLSSSharpenTryGetResult(SharpenResultData* result)
{
    if (!result)
        return 0;
    std::lock_guard<std::mutex> lock(g_resultMutex);
    if (g_latestResult.sequence == 0)
        return 0;
    *result = g_latestResult;
    return 1;
}

extern "C" __declspec(dllexport) void __stdcall DSPDLSSSharpenGetStatus(char* buffer, uint32_t capacity)
{
    if (!buffer || capacity == 0)
        return;
    std::lock_guard<std::mutex> lock(g_resultMutex);
    std::snprintf(buffer, capacity, "%s", g_status.c_str());
}
