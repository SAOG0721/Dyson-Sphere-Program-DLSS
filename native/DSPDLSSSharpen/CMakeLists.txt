cmake_minimum_required(VERSION 3.21)
project(DSPDLSSSharpen LANGUAGES CXX)

if(NOT DEFINED UNITY_NATIVE_PLUGIN_ROOT AND DEFINED ENV{UNITY_NATIVE_PLUGIN_ROOT})
    set(UNITY_NATIVE_PLUGIN_ROOT "$ENV{UNITY_NATIVE_PLUGIN_ROOT}")
endif()
if(NOT EXISTS "${UNITY_NATIVE_PLUGIN_ROOT}/Unity/IUnityInterface.h")
    message(FATAL_ERROR "Set UNITY_NATIVE_PLUGIN_ROOT to Unity-NativeRenderingPlugin/PluginSource/source")
endif()

if(NOT DEFINED FXC_EXECUTABLE)
    find_program(FXC_EXECUTABLE NAMES fxc.exe)
endif()
if(NOT FXC_EXECUTABLE)
    file(GLOB FXC_CANDIDATES "C:/Program Files (x86)/Windows Kits/10/bin/*/x64/fxc.exe")
    list(SORT FXC_CANDIDATES ORDER DESCENDING)
    list(LENGTH FXC_CANDIDATES FXC_CANDIDATE_COUNT)
    if(FXC_CANDIDATE_COUNT GREATER 0)
        list(GET FXC_CANDIDATES 0 FXC_EXECUTABLE)
    endif()
endif()

if(NOT EXISTS "${FXC_EXECUTABLE}")
    message(FATAL_ERROR "fxc.exe is required to build the Gaussian USM cs_5_0 shader")
endif()

set(GENERATED_DIR "${CMAKE_CURRENT_BINARY_DIR}/generated")
set(GAUSSIAN_USM_SHADER_SOURCE "${CMAKE_CURRENT_SOURCE_DIR}/src/gaussian_usm_cs.hlsl")
set(GAUSSIAN_USM_SHADER_HEADER "${GENERATED_DIR}/gaussian_usm_cs.h")
add_custom_command(
    OUTPUT "${GAUSSIAN_USM_SHADER_HEADER}"
    COMMAND "${CMAKE_COMMAND}" -E make_directory "${GENERATED_DIR}"
    COMMAND "${FXC_EXECUTABLE}"
        /nologo /T cs_5_0 /E main /O3
        /Fh "${GAUSSIAN_USM_SHADER_HEADER}"
        /Vn g_gaussian_usm_cs
        "${GAUSSIAN_USM_SHADER_SOURCE}"
    DEPENDS "${GAUSSIAN_USM_SHADER_SOURCE}"
    COMMENT "Compiling optimized 3x3 Gaussian USM shader (cs_5_0)"
    VERBATIM
)

add_library(DSPDLSSSharpen SHARED
    src/dspdlss_sharpen.cpp
    "${GAUSSIAN_USM_SHADER_HEADER}"
)
target_compile_features(DSPDLSSSharpen PRIVATE cxx_std_17)
target_compile_definitions(DSPDLSSSharpen PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX)
target_compile_options(DSPDLSSSharpen PRIVATE $<$<CXX_COMPILER_ID:MSVC>:/Brepro> $<$<CXX_COMPILER_ID:MSVC>:/utf-8>)
target_link_options(DSPDLSSSharpen PRIVATE $<$<CXX_COMPILER_ID:MSVC>:/Brepro>)
target_include_directories(DSPDLSSSharpen PRIVATE
    "${UNITY_NATIVE_PLUGIN_ROOT}"
    "${GENERATED_DIR}"
)
target_link_libraries(DSPDLSSSharpen PRIVATE d3d11)
set_target_properties(DSPDLSSSharpen PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin"
)
