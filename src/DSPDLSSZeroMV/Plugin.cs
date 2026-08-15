using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.NVIDIA;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;
using NvidiaGraphicsDevice = UnityEngine.NVIDIA.GraphicsDevice;

namespace DSPDLSSZeroMV;

public enum DSPDLSSMode
{
    UltraPerformance = 0,
    Performance = 1,
    Balanced = 2,
    Quality = 3,
    DLAA = 4
}

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "local.dsp.dlss";
    public const string PluginName = "DSP DLSS";
    public const string PluginVersion = "0.5.2";

    internal static Plugin Instance { get; private set; }
    internal static ManualLogSource LogSource { get; private set; }

    private ConfigEntry<bool> _startEnabledConfig;
    private ConfigEntry<DSPDLSSMode> _modeConfig;
    private ConfigEntry<bool> _showInfoConfig;
    private ConfigEntry<KeyCode> _panelKeyConfig;

    private Harmony _harmony;
    private NvidiaGraphicsDevice _device;
    private DLSSContext _feature;
    private GraphicsDeviceDebugView _debugView;

    private RenderTexture _preparedColor;
    private RenderTexture _preparedDepth;
    private RenderTexture _preparedMotion;
    private RenderTexture _dlssOutput;

    private PostEffectController _postController;
    private PostProcessingProfile _armedProfile;
    private AntialiasingModel.Settings _originalAaSettings;
    private bool _originalAaEnabled;
    private bool _originalAaOn;
    private int _originalMsaa;
    private bool _profileArmed;

    private bool _deviceReady;
    private bool _featureDebugValid;
    private bool _runtimeEnabled;
    private bool _showInfo;
    private bool _sessionFaulted;
    private bool _resetHistory = true;
    private bool _updateObserved;
    private bool _inputDescriptionLogged;
    private bool _hardDisabled;
    private bool _panelVisible;
    private int _jitterSamples;
    private int _jitterFiniteSamples;
    private int _jitterNonZeroSamples;
    private int _jitterProjectionMatches;
    private int _jitterSubmissionMatches;
    private int _jitterNativeSamples;
    private int _jitterNativeMatches;
    private int _jitterSequenceChanges;
    private int _jitterPositiveX;
    private int _jitterNegativeX;
    private int _jitterPositiveY;
    private int _jitterNegativeY;
    private bool _previousJitterValid;
    private bool _jitterAuditLogged;
    private bool _jitterNativeMismatchLogged;
    private Vector2 _previousJitter;
    private Vector2 _lastJitterPixels;
    private int _projectionAuditFrame = -1;
    private Vector2 _projectionAuditJitter;
    private bool _projectionAuditMatches;
    private readonly Vector2[] _jitterSubmissionHistory = new Vector2[32];
    private readonly int[] _jitterSubmissionSequenceHistory = new int[32];
    private int _jitterSubmissionSequence;
    private int _lastNativeMatchedSequence;
    private Vector2 _lastNativeJitterPixels;
    private bool _lastNativeJitterValid;
    private string _jitterStatus = "waiting for DLSS frames";
    private string _jitterProjectionSignature = string.Empty;
    private CursorLockMode _cursorLockBeforePanel;
    private bool _cursorVisibleBeforePanel;
    private bool _cursorStateSaved;

    private const int ControlWindowId = 0x445350;
    private Rect _controlWindow = new Rect(28f, 84f, 560f, 430f);

    private DSPDLSSMode _runtimeMode;
    private DSPDLSSMode _featureMode;
    private DLSSQuality _featureQuality;
    private RenderTextureFormat _featureFormat;
    private int _inputWidth;
    private int _inputHeight;
    private int _outputWidth;
    private int _outputHeight;
    private float _featureSharpness;
    private int _featureSubmitFrame = -1;
    private int _executeCount;
    private int _fallbackCount;
    private int _lastSceneIndex = -1;
    private int _lastDebugUpdateFrame;
    private int _lastRuntimeTickFrame = -1;

    private string _status = "initializing";
    private string _inputFailure = string.Empty;
    private string _toast = string.Empty;
    private float _toastUntil;
    private GUIStyle _labelStyle;
    private GUIStyle _warningStyle;
    private GUIStyle _toastStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _activeButtonStyle;

    internal bool PanelVisible => _panelVisible;

    private bool RequestedEnabled => _runtimeEnabled && !_hardDisabled && !_sessionFaulted;

    private static readonly System.Reflection.FieldInfo TaaJitterVectorField =
        AccessTools.Field(typeof(TaaComponent), "<jitterVector>k__BackingField");

    private readonly struct ModeSpec
    {
        internal readonly DSPDLSSMode mode;
        internal readonly DLSSQuality quality;
        internal readonly int inputWidth;
        internal readonly int inputHeight;
        internal readonly int outputWidth;
        internal readonly int outputHeight;
        internal readonly float sharpness;

        internal ModeSpec(
            DSPDLSSMode mode,
            DLSSQuality quality,
            int inputWidth,
            int inputHeight,
            int outputWidth,
            int outputHeight,
            float sharpness)
        {
            this.mode = mode;
            this.quality = quality;
            this.inputWidth = inputWidth;
            this.inputHeight = inputHeight;
            this.outputWidth = outputWidth;
            this.outputHeight = outputHeight;
            this.sharpness = sharpness;
        }

        internal bool requiresPreparedInputs => inputWidth != outputWidth || inputHeight != outputHeight;
    }

    private void Awake()
    {
        Instance = this;
        LogSource = Logger;

        _startEnabledConfig = Config.Bind("General", "StartEnabled", false,
            "Start DLSS Super Resolution automatically. Keep false for fail-safe startup.");
        _modeConfig = Config.Bind("General", "Mode", DSPDLSSMode.Quality,
            "DLSS Super Resolution mode selected in the F8 control panel.");
        _showInfoConfig = Config.Bind("General", "ShowInfo", true,
            "Show the compact runtime information overlay.");
        _panelKeyConfig = Config.Bind("Hotkeys", "PanelToggle", KeyCode.F8,
            "Open or close the mouse-operated DSP DLSS control panel.");
        _runtimeEnabled = _startEnabledConfig.Value;
        _runtimeMode = _modeConfig.Value;
        _showInfo = _showInfoConfig.Value;

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(Plugin).Assembly);

        Logger.LogWarning("DSP DLSS 0.5.2 loaded. F8 opens the mouse control panel.");
        Logger.LogInfo($"Initial state: SR={_runtimeEnabled}, mode={GetModeLabel(_runtimeMode)}, info={_showInfo}");
        InitializeNvidiaNow();
    }

    private void InitializeNvidiaNow()
    {
        try
        {
            Logger.LogInfo($"Unity={Application.unityVersion}, API={SystemInfo.graphicsDeviceType}, GPU={SystemInfo.graphicsDeviceName}");
            bool loaded = NVUnityPlugin.IsLoaded() || NVUnityPlugin.Load();
            Logger.LogInfo($"NVUnityPlugin loaded={loaded}, IsLoaded={NVUnityPlugin.IsLoaded()}");
            if (!loaded && !NVUnityPlugin.IsLoaded())
                throw new InvalidOperationException("NVUnityPlugin.dll could not be loaded");

            if (!NvUnityNative.HasBaseEventIdExport())
            {
                _hardDisabled = true;
                _status = "disabled: incompatible NVUnityPlugin ABI";
                Logger.LogError("NVUnityPlugin is missing NVUP_GetBaseEventId. The unsafe legacy event-ID path is blocked.");
                return;
            }

            _device = NvidiaGraphicsDevice.CreateGraphicsDevice();
            if (_device == null)
                throw new InvalidOperationException("Unity NVIDIA GraphicsDevice creation returned null");
            if (!_device.IsFeatureAvailable(GraphicsDeviceFeature.DLSS))
                throw new InvalidOperationException("The current GPU/driver reports DLSS unavailable");

            _debugView = _device.CreateDebugView();
            _deviceReady = true;
            _status = "ready; open the F8 panel to configure DLSS";
            Logger.LogInfo($"NVIDIA device version={NvidiaGraphicsDevice.version}; DLSS is available");

            foreach (DLSSQuality quality in Enum.GetValues(typeof(DLSSQuality)))
            {
                if (_device.GetOptimalSettings(
                        (uint)Math.Max(Screen.width, 1),
                        (uint)Math.Max(Screen.height, 1),
                        quality,
                        out OptimalDLSSSettingsData data))
                {
                    Logger.LogInfo($"Optimal {quality}: {data.outRenderWidth}x{data.outRenderHeight}, " +
                                   $"range {data.minWidth}x{data.minHeight}-{data.maxWidth}x{data.maxHeight}, " +
                                   $"sharpness={data.sharpness:F3}");
                }
            }
        }
        catch (Exception ex)
        {
            _hardDisabled = true;
            _status = "NVIDIA initialization failed: " + ex.Message;
            Logger.LogError(ex);
        }
    }


    internal void RuntimeTick(PostEffectController controller)
    {
        if (!_updateObserved)
        {
            _updateObserved = true;
            Logger.LogInfo("Gameplay PostEffect heartbeat observed; the F8 DLSS panel is active");
        }

        if (controller != null)
            _postController = controller;

        if (_lastRuntimeTickFrame == Time.frameCount)
            return;
        _lastRuntimeTickFrame = Time.frameCount;

        HandleHotkeys();

        int sceneIndex = GameCamera.sceneIndex;
        if (sceneIndex != _lastSceneIndex)
        {
            _lastSceneIndex = sceneIndex;
            _resetHistory = true;
            ResetJitterAudit();
            if (sceneIndex != 1)
                RestoreGameAa();
        }

        if (RequestedEnabled && _deviceReady && IsNormalGameplayCameraAvailable())
            ArmTaaSlot();
        else
            RestoreGameAa();

        int debugInterval = _feature == null ? 120 :
            (!_featureDebugValid || _jitterNativeSamples < 16 ? 1 : 120);
        if (_deviceReady && _debugView != null && Time.frameCount - _lastDebugUpdateFrame >= debugInterval)
        {
            _lastDebugUpdateFrame = Time.frameCount;
            UpdateDebugState();
        }
    }

    private void HandleHotkeys()
    {
        if (_panelKeyConfig != null && Input.GetKeyDown(_panelKeyConfig.Value))
        {
            SetPanelVisible(!_panelVisible);
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (_panelVisible == visible)
            return;

        _panelVisible = visible;
        if (visible)
        {
            _cursorLockBeforePanel = Cursor.lockState;
            _cursorVisibleBeforePanel = Cursor.visible;
            _cursorStateSaved = true;
            UICursor.forceShowCursor = true;
            UICursor.SetLockCursorDirect(false);
            ConsumeGameMouseInput();
        }
        else if (_cursorStateSaved)
        {
            Cursor.visible = _cursorVisibleBeforePanel;
            Cursor.lockState = _cursorLockBeforePanel;
            _cursorStateSaved = false;
        }

        Logger.LogInfo($"DLSS control panel: {(visible ? "OPEN" : "CLOSED")}");
    }

    internal void CaptureGameInputForPanel()
    {
        if (!_panelVisible)
            return;

        VFInput.inFullscreenGUI = true;
        VFInput.onGUI = true;
        VFInput.onGUIOperate = true;
        VFInput.readyToBuild = false;
        VFInput.readyToEnterBlueprintMode = false;
        VFInput.readyToPlant = false;
        ConsumeGameMouseInput();
    }

    private static void ConsumeGameMouseInput()
    {
        if (VFInput.axis_button == null)
            return;
        VFInput.axis_button.Use(0);
        VFInput.axis_button.Use(1);
        VFInput.axis_button.Use(2);
    }

    private void SetSuperResolutionEnabled(bool enabled)
    {
        if (_runtimeEnabled == enabled)
            return;

        _runtimeEnabled = enabled;
        if (enabled)
        {
            _sessionFaulted = false;
            _resetHistory = true;
            ShowToast("DLSS SR ON - " + GetModeLabel(_runtimeMode));
        }
        else
        {
            RestoreGameAa();
            ShowToast("DLSS SR OFF");
        }

        Logger.LogWarning($"DLSS SR runtime toggle: {(enabled ? "ON" : "OFF")}");
    }

    private void SetMode(DSPDLSSMode mode)
    {
        if (_runtimeMode == mode)
            return;

        _runtimeMode = mode;
        _modeConfig.Value = mode;
        Config.Save();
        _resetHistory = true;
        _sessionFaulted = false;
        DestroyFeature();
        ShowToast("DLSS MODE - " + GetModeLabel(mode));
        Logger.LogWarning($"DLSS mode changed: {GetModeLabel(mode)}");
    }

    private static string GetModeLabel(DSPDLSSMode mode)
    {
        return mode switch
        {
            DSPDLSSMode.UltraPerformance => "Ultra Performance",
            DSPDLSSMode.Performance => "Performance",
            DSPDLSSMode.Balanced => "Balanced",
            DSPDLSSMode.Quality => "Quality",
            DSPDLSSMode.DLAA => "DLAA",
            _ => mode.ToString()
        };
    }

    private void ShowToast(string message)
    {
        _toast = message;
        _toastUntil = Time.unscaledTime + 2.5f;
    }

    private bool IsNormalGameplayCameraAvailable()
    {
        Camera camera = GameCamera.main;
        if (GameCamera.sceneIndex != 1 || camera == null || camera.orthographic || camera.targetTexture != null)
            return false;
        if (GameCamera.instance != null &&
            GameCamera.instance.hdScreenshot != null &&
            GameCamera.instance.hdScreenshot.preparing)
            return false;
        return true;
    }

    private void ArmTaaSlot()
    {
        if (_postController == null || _postController.postScript == null || _postController.postScript.profile == null)
        {
            _status = "waiting for gameplay post-processing profile";
            return;
        }

        PostProcessingProfile profile = _postController.postScript.profile;
        if (!_profileArmed || _armedProfile != profile)
        {
            RestoreGameAa();
            _armedProfile = profile;
            _originalAaSettings = profile.antialiasing.settings;
            _originalAaEnabled = profile.antialiasing.enabled;
            _originalAaOn = PostEffectController.aaOn;
            _originalMsaa = QualitySettings.antiAliasing;
            _profileArmed = true;
            _resetHistory = true;
            Logger.LogInfo($"Armed profile '{profile.name}'; saved AA state (aaOn={_originalAaOn}, MSAA={_originalMsaa})");
        }

        AntialiasingModel.Settings settings = profile.antialiasing.settings;
        if (settings.method != AntialiasingModel.Method.Taa)
        {
            settings.method = AntialiasingModel.Method.Taa;
            profile.antialiasing.settings = settings;
        }
        profile.antialiasing.enabled = true;
        PostEffectController.aaOn = true;
        if (QualitySettings.antiAliasing != 0)
            QualitySettings.antiAliasing = 0;
    }

    private void RestoreGameAa()
    {
        if (!_profileArmed)
            return;

        if (_armedProfile != null)
        {
            _armedProfile.antialiasing.settings = _originalAaSettings;
            _armedProfile.antialiasing.enabled = _originalAaEnabled;
        }
        PostEffectController.aaOn = _originalAaOn;
        QualitySettings.antiAliasing = _originalMsaa;
        _profileArmed = false;
        _armedProfile = null;
        DestroyFeature();
        _status = RequestedEnabled ? "waiting for normal gameplay camera" : "DLSS OFF; game AA restored";
        Logger.LogInfo("Restored the game's post AA and MSAA state");
    }

    internal void CaptureJitterProjection(TaaComponent taa)
    {
        Camera camera = taa?.context?.camera;
        if (!RequestedEnabled || camera == null || camera != GameCamera.main)
            return;

        int outputWidth = taa.context.width;
        int outputHeight = taa.context.height;
        if (outputWidth <= 0 || outputHeight <= 0 || TaaJitterVectorField == null)
            return;

        int inputWidth = _inputWidth > 0 && _outputWidth == outputWidth && _outputHeight == outputHeight
            ? _inputWidth
            : outputWidth;
        int inputHeight = _inputHeight > 0 && _outputWidth == outputWidth && _outputHeight == outputHeight
            ? _inputHeight
            : outputHeight;
        float gameJitterSpread = taa.model.settings.taaSettings.jitterSpread;
        if (!IsFinite(gameJitterSpread) || gameJitterSpread <= 0.000001f)
            return;

        // DSP's legacy TAA produces Halton * spread in output-pixel space: it is
        // positive-only, and downsampling also shrinks its phase coverage. DLSS
        // expects a zero-centred, full-pixel phase measured in render-input pixels.
        // Recover the unscaled Halton phase, matching Unity HDRP's DLSS path which
        // deliberately bypasses the normal TAA jitter-spread multiplier.
        Vector2 gameNormalizedJitter = taa.jitterVector;
        Vector2 haltonPhase = new Vector2(
            gameNormalizedJitter.x * outputWidth / gameJitterSpread,
            gameNormalizedJitter.y * outputHeight / gameJitterSpread);
        // DSP includes Halton index 0. Unity's DLSS path skips it; map that slot to
        // index 8 so the repeating set is the same eight non-zero phases (1..8).
        if (Mathf.Abs(haltonPhase.x) < 0.000001f && Mathf.Abs(haltonPhase.y) < 0.000001f)
            haltonPhase = new Vector2(0.0625f, 0.8888889f);
        Vector2 normalizedJitter = new Vector2(
            (haltonPhase.x - 0.5f) / inputWidth,
            (haltonPhase.y - 0.5f) / inputHeight);

        Matrix4x4 correctedProjection = camera.projectionMatrix;
        float deltaX = normalizedJitter.x - gameNormalizedJitter.x;
        float deltaY = normalizedJitter.y - gameNormalizedJitter.y;
        if (camera.orthographic)
        {
            correctedProjection.m03 -= 2f * deltaX;
            correctedProjection.m13 -= 2f * deltaY;
        }
        else
        {
            correctedProjection.m02 += 2f * deltaX;
            correctedProjection.m12 += 2f * deltaY;
        }
        TaaJitterVectorField.SetValue(taa, normalizedJitter);
        camera.projectionMatrix = correctedProjection;

        if (_inputWidth > 0)
        {
            string signature = $"{inputWidth}x{inputHeight}->{outputWidth}x{outputHeight}";
            if (_jitterProjectionSignature != signature)
            {
                _jitterProjectionSignature = signature;
                Logger.LogWarning($"DLSS input-grid jitter active: {signature}, " +
                                  $"projection scale={outputWidth / (float)inputWidth:F3}x{outputHeight / (float)inputHeight:F3}, " +
                                  $"source TAA spread={gameJitterSpread:F3}, DLSS spread=1.000");
            }
        }

        bool finite = IsFinite(normalizedJitter.x) && IsFinite(normalizedJitter.y);
        Matrix4x4 jittered = correctedProjection;
        Matrix4x4 nonJittered = camera.nonJitteredProjectionMatrix;
        float expectedDeltaX = 2f * normalizedJitter.x;
        float expectedDeltaY = 2f * normalizedJitter.y;

        _projectionAuditFrame = Time.frameCount;
        _projectionAuditJitter = normalizedJitter;
        _projectionAuditMatches = finite &&
            Mathf.Abs((jittered.m02 - nonJittered.m02) - expectedDeltaX) < 0.00005f &&
            Mathf.Abs((jittered.m12 - nonJittered.m12) - expectedDeltaY) < 0.00005f;
    }

    internal bool TryExecute(TaaComponent taa, RenderTexture source, RenderTexture destination)
    {
        if (!RequestedEnabled || !_deviceReady || !_profileArmed || taa == null || source == null || destination == null)
            return false;

        Camera camera = taa.context?.camera;
        if (camera == null || camera != GameCamera.main || !IsNormalGameplayCameraAvailable())
            return false;

        Texture depth = Shader.GetGlobalTexture("_CameraDepthTexture");
        Texture motion = Shader.GetGlobalTexture("_CameraMotionVectorsTexture");
        if (!ValidateFrameInputs(source, depth, motion))
            return false;

        try
        {
            EnsureFeature(source.width, source.height, source.format);
            if (_feature == null || _dlssOutput == null)
                return false;

            if (!_featureDebugValid)
            {
                _status = $"creating {GetModeLabel(_runtimeMode)} feature; game TAA active";
                return false;
            }

            ref DLSSCommandExecutionData execute = ref _feature.executeData;
            Vector2 normalizedJitter = taa.jitterVector;
            Vector2 jitterPixels = new Vector2(
                -normalizedJitter.x * _inputWidth,
                -normalizedJitter.y * _inputHeight);
            execute.reset = _resetHistory ? 1 : 0;
            execute.sharpness = _featureSharpness;
            execute.mvScaleX = -_inputWidth;
            execute.mvScaleY = -_inputHeight;
            execute.jitterOffsetX = jitterPixels.x;
            execute.jitterOffsetY = jitterPixels.y;
            execute.preExposure = 1f;
            execute.subrectOffsetX = 0;
            execute.subrectOffsetY = 0;
            execute.subrectWidth = (uint)_inputWidth;
            execute.subrectHeight = (uint)_inputHeight;
            execute.invertXAxis = 0;
            execute.invertYAxis = 1;

            Texture colorInput = source;
            Texture depthInput = depth;
            Texture motionInput = motion;

            CommandBuffer command = new CommandBuffer { name = "DSP DLSS Execute" };
            try
            {
                if (_preparedColor != null)
                {
                    command.Blit(source, _preparedColor);
                    command.Blit(depth, _preparedDepth);
                    command.Blit(motion, _preparedMotion);
                    colorInput = _preparedColor;
                    depthInput = _preparedDepth;
                    motionInput = _preparedMotion;
                }

                DLSSTextureTable textures = new DLSSTextureTable
                {
                    colorInput = colorInput,
                    colorOutput = _dlssOutput,
                    depth = depthInput,
                    motionVectors = motionInput,
                    transparencyMask = null,
                    exposureTexture = null,
                    biasColorMask = null
                };

                _device.ExecuteDLSS(command, _feature, textures);
                command.Blit(_dlssOutput, destination);
                Graphics.ExecuteCommandBuffer(command);
            }
            finally
            {
                command.Release();
            }

            if (!_inputDescriptionLogged)
            {
                Logger.LogWarning($"DLSS inputs: mode={GetModeLabel(_featureMode)}, " +
                                  $"source={source.width}x{source.height} {source.format}, " +
                                  $"prepared={_inputWidth}x{_inputHeight}, output={_outputWidth}x{_outputHeight}, " +
                                  $"depth={DescribeTexture(depth)}, motion={DescribeTexture(motion)}");
                _inputDescriptionLogged = true;
            }

            _executeCount++;
            RecordJitterAudit(
                camera,
                normalizedJitter,
                jitterPixels,
                execute.jitterOffsetX,
                execute.jitterOffsetY);
            _resetHistory = false;
            _inputFailure = string.Empty;
            _status = $"ACTIVE · {GetModeLabel(_featureMode)} · {_inputWidth}x{_inputHeight} → {_outputWidth}x{_outputHeight}";
            if (_executeCount == 1 || _executeCount % 600 == 0)
                Logger.LogWarning($"DLSS Execute count={_executeCount}, mode={GetModeLabel(_featureMode)}, " +
                                  $"{_inputWidth}x{_inputHeight}->{_outputWidth}x{_outputHeight}");
            return true;
        }
        catch (Exception ex)
        {
            _fallbackCount++;
            _sessionFaulted = true;
            _status = "DLSS faulted; game TAA fallback: " + ex.Message;
            Logger.LogError(ex);
            DestroyFeature();
            return false;
        }
    }


    private void ResetJitterAudit()
    {
        _jitterSamples = 0;
        _jitterFiniteSamples = 0;
        _jitterNonZeroSamples = 0;
        _jitterProjectionMatches = 0;
        _jitterSubmissionMatches = 0;
        _jitterNativeSamples = 0;
        _jitterNativeMatches = 0;
        _jitterSequenceChanges = 0;
        _jitterPositiveX = 0;
        _jitterNegativeX = 0;
        _jitterPositiveY = 0;
        _jitterNegativeY = 0;
        _previousJitterValid = false;
        _jitterAuditLogged = false;
        _jitterNativeMismatchLogged = false;
        _previousJitter = Vector2.zero;
        _lastJitterPixels = Vector2.zero;
        _projectionAuditFrame = -1;
        _projectionAuditJitter = Vector2.zero;
        _projectionAuditMatches = false;
        Array.Clear(_jitterSubmissionHistory, 0, _jitterSubmissionHistory.Length);
        Array.Clear(_jitterSubmissionSequenceHistory, 0, _jitterSubmissionSequenceHistory.Length);
        _jitterSubmissionSequence = 0;
        _lastNativeMatchedSequence = 0;
        _lastNativeJitterPixels = Vector2.zero;
        _lastNativeJitterValid = false;
        _jitterStatus = "waiting for DLSS frames";
    }

    private void RecordJitterAudit(
        Camera camera,
        Vector2 normalizedJitter,
        Vector2 jitterPixels,
        float submittedJitterX,
        float submittedJitterY)
    {
        _jitterSamples++;
        _lastJitterPixels = jitterPixels;

        bool finite = IsFinite(normalizedJitter.x) && IsFinite(normalizedJitter.y) &&
                      IsFinite(jitterPixels.x) && IsFinite(jitterPixels.y);
        if (finite)
            _jitterFiniteSamples++;
        if (Mathf.Abs(normalizedJitter.x) > 0.0000001f || Mathf.Abs(normalizedJitter.y) > 0.0000001f)
            _jitterNonZeroSamples++;
        if (jitterPixels.x > 0.000001f)
            _jitterPositiveX++;
        else if (jitterPixels.x < -0.000001f)
            _jitterNegativeX++;
        if (jitterPixels.y > 0.000001f)
            _jitterPositiveY++;
        else if (jitterPixels.y < -0.000001f)
            _jitterNegativeY++;
        if (_previousJitterValid && (normalizedJitter - _previousJitter).sqrMagnitude > 0.000000000001f)
            _jitterSequenceChanges++;

        bool projectionMatches = camera != null && finite &&
                                 _projectionAuditFrame == Time.frameCount &&
                                 (_projectionAuditJitter - normalizedJitter).sqrMagnitude < 0.000000000001f &&
                                 _projectionAuditMatches;
        if (projectionMatches)
            _jitterProjectionMatches++;

        bool submissionMatches = Mathf.Abs(submittedJitterX - jitterPixels.x) < 0.000001f &&
                                 Mathf.Abs(submittedJitterY - jitterPixels.y) < 0.000001f;
        if (submissionMatches)
            _jitterSubmissionMatches++;

        _previousJitter = normalizedJitter;
        _previousJitterValid = true;
        int submissionSequence = ++_jitterSubmissionSequence;
        int historyIndex = submissionSequence % _jitterSubmissionHistory.Length;
        _jitterSubmissionHistory[historyIndex] = jitterPixels;
        _jitterSubmissionSequenceHistory[historyIndex] = submissionSequence;
        UpdateJitterAuditStatus();
    }

    private void RecordNativeJitterAudit(DLSSCommandExecutionData nativeExecute)
    {
        Vector2 nativeJitter = new Vector2(nativeExecute.jitterOffsetX, nativeExecute.jitterOffsetY);
        if (!IsFinite(nativeJitter.x) || !IsFinite(nativeJitter.y))
            return;

        if (_lastNativeJitterValid &&
            (nativeJitter - _lastNativeJitterPixels).sqrMagnitude < 0.000000000001f)
            return;

        int oldestSequence = Math.Max(_lastNativeMatchedSequence + 1,
            _jitterSubmissionSequence - _jitterSubmissionHistory.Length + 1);
        int matchedSequence = 0;
        for (int sequence = oldestSequence; sequence <= _jitterSubmissionSequence; sequence++)
        {
            int historyIndex = sequence % _jitterSubmissionHistory.Length;
            if (_jitterSubmissionSequenceHistory[historyIndex] == sequence &&
                (nativeJitter - _jitterSubmissionHistory[historyIndex]).sqrMagnitude < 0.000000000001f)
            {
                matchedSequence = sequence;
                break;
            }
        }

        if (matchedSequence == 0 && _jitterSubmissionSequence < 4)
            return;

        _jitterNativeSamples++;
        if (matchedSequence != 0)
        {
            _jitterNativeMatches++;
            _lastNativeMatchedSequence = matchedSequence;
        }
        else if (!_jitterNativeMismatchLogged)
        {
            _jitterNativeMismatchLogged = true;
            Logger.LogError($"DLSS native jitter mismatch: native=({nativeJitter.x:F6},{nativeJitter.y:F6}), " +
                            $"submittedSequence={_jitterSubmissionSequence}, lastMatched={_lastNativeMatchedSequence}");
        }
        _lastNativeJitterPixels = nativeJitter;
        _lastNativeJitterValid = true;
        UpdateJitterAuditStatus();
    }

    private void UpdateJitterAuditStatus()
    {
        int transitions = Math.Max(_jitterSamples - 1, 0);
        bool enoughSamples = _jitterSamples >= 16 && _jitterNativeSamples >= 16;
        bool passed = _jitterFiniteSamples == _jitterSamples &&
                      _jitterProjectionMatches == _jitterSamples &&
                      _jitterSubmissionMatches == _jitterSamples &&
                       _jitterNativeMatches == _jitterNativeSamples &&
                       _jitterNonZeroSamples >= (_jitterSamples * 3) / 4 &&
                       _jitterPositiveX > 0 && _jitterNegativeX > 0 &&
                       _jitterPositiveY > 0 && _jitterNegativeY > 0 &&
                       _jitterSequenceChanges >= Math.Max(transitions - 2, 0);
        string verdict = enoughSamples ? (passed ? "PASS" : "CHECK") :
            $"checking {Math.Min(_jitterSamples, 16)}/16 native {Math.Min(_jitterNativeSamples, 16)}/16";
        _jitterStatus = $"{verdict}; projection={_jitterProjectionMatches}/{_jitterSamples}, " +
                        $"submit={_jitterSubmissionMatches}/{_jitterSamples}, " +
                         $"native={_jitterNativeMatches}/{_jitterNativeSamples}, " +
                         $"sequence={_jitterSequenceChanges}/{transitions}, " +
                         $"center=X+{_jitterPositiveX}/-{_jitterNegativeX} Y+{_jitterPositiveY}/-{_jitterNegativeY}, " +
                         $"DLSS px=({_lastJitterPixels.x:F3},{_lastJitterPixels.y:F3})";

        if (enoughSamples && !_jitterAuditLogged)
        {
            _jitterAuditLogged = true;
            string detail = "DLSS jitter audit: " + _jitterStatus;
            if (passed)
                Logger.LogWarning(detail);
            else
                Logger.LogError(detail);
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private bool ValidateFrameInputs(RenderTexture source, Texture depth, Texture motion)
    {
        string failure = string.Empty;
        if (depth == null)
            failure = "_CameraDepthTexture is unavailable";
        else if (motion == null)
            failure = "_CameraMotionVectorsTexture is unavailable";
        else if (motion.width != source.width || motion.height != source.height)
            failure = $"motion size {motion.width}x{motion.height} != source {source.width}x{source.height}";

        if (failure.Length == 0)
        {
            _inputFailure = string.Empty;
            return true;
        }

        _fallbackCount++;
        _status = "waiting for valid TAA inputs: " + failure;
        if (_inputFailure != failure)
        {
            _inputFailure = failure;
            Logger.LogWarning(_status);
        }
        return false;
    }

    private static string DescribeTexture(Texture texture)
    {
        if (texture == null)
            return "null";
        string format = texture is RenderTexture rt ? rt.format.ToString() : texture.GetType().Name;
        return $"{texture.width}x{texture.height} {format}";
    }

    private ModeSpec ResolveModeSpec(int outputWidth, int outputHeight)
    {
        if (_runtimeMode == DSPDLSSMode.DLAA)
        {
            return new ModeSpec(
                _runtimeMode,
                DLSSQuality.MaximumQuality,
                outputWidth,
                outputHeight,
                outputWidth,
                outputHeight,
                0f);
        }

        DLSSQuality quality = _runtimeMode switch
        {
            DSPDLSSMode.UltraPerformance => DLSSQuality.UltraPerformance,
            DSPDLSSMode.Performance => DLSSQuality.MaximumPerformance,
            DSPDLSSMode.Balanced => DLSSQuality.Balanced,
            _ => DLSSQuality.MaximumQuality
        };

        if (!_device.GetOptimalSettings((uint)outputWidth, (uint)outputHeight, quality, out OptimalDLSSSettingsData optimal))
            throw new InvalidOperationException($"No optimal DLSS settings for {quality} at {outputWidth}x{outputHeight}");

        int inputWidth = (int)optimal.outRenderWidth;
        int inputHeight = (int)optimal.outRenderHeight;
        if (inputWidth <= 0 || inputHeight <= 0 || inputWidth > outputWidth || inputHeight > outputHeight)
            throw new InvalidOperationException($"Invalid DLSS input size {inputWidth}x{inputHeight} for output {outputWidth}x{outputHeight}");

        return new ModeSpec(
            _runtimeMode,
            quality,
            inputWidth,
            inputHeight,
            outputWidth,
            outputHeight,
            optimal.sharpness);
    }

    private void EnsureFeature(int outputWidth, int outputHeight, RenderTextureFormat sourceFormat)
    {
        ModeSpec spec = ResolveModeSpec(outputWidth, outputHeight);
        bool resourcesReady = _dlssOutput != null && _dlssOutput.IsCreated();
        if (spec.requiresPreparedInputs)
        {
            resourcesReady = resourcesReady &&
                             _preparedColor != null && _preparedColor.IsCreated() &&
                             _preparedDepth != null && _preparedDepth.IsCreated() &&
                             _preparedMotion != null && _preparedMotion.IsCreated();
        }

        if (_feature != null &&
            _featureMode == spec.mode &&
            _inputWidth == spec.inputWidth &&
            _inputHeight == spec.inputHeight &&
            _outputWidth == spec.outputWidth &&
            _outputHeight == spec.outputHeight &&
            _featureFormat == sourceFormat &&
            resourcesReady)
            return;

        DestroyFeature();

        if (!SystemInfo.SupportsRenderTextureFormat(sourceFormat))
            throw new NotSupportedException($"Render texture format {sourceFormat} is unavailable");
        if (!SystemInfo.SupportsRandomWriteOnRenderTextureFormat(sourceFormat))
            throw new NotSupportedException($"Render texture format {sourceFormat} has no UAV support");
        if (spec.requiresPreparedInputs && !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
            throw new NotSupportedException("RFloat is unavailable for the prepared depth input");
        if (spec.requiresPreparedInputs && !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf))
            throw new NotSupportedException("RGHalf is unavailable for the prepared motion input");

        if (spec.requiresPreparedInputs)
        {
            _preparedColor = CreateRenderTexture(
                "DSP DLSS Prepared Color",
                spec.inputWidth,
                spec.inputHeight,
                sourceFormat,
                false,
                FilterMode.Bilinear);
            _preparedDepth = CreateRenderTexture(
                "DSP DLSS Prepared Depth",
                spec.inputWidth,
                spec.inputHeight,
                RenderTextureFormat.RFloat,
                false,
                FilterMode.Point);
            _preparedMotion = CreateRenderTexture(
                "DSP DLSS Prepared Motion",
                spec.inputWidth,
                spec.inputHeight,
                RenderTextureFormat.RGHalf,
                false,
                FilterMode.Point);
        }

        _dlssOutput = CreateRenderTexture(
            "DSP DLSS UAV Output",
            spec.outputWidth,
            spec.outputHeight,
            sourceFormat,
            true,
            FilterMode.Bilinear);

        DLSSFeatureFlags flags = DLSSFeatureFlags.MVLowRes | DLSSFeatureFlags.DoSharpening;
        if (GameCamera.main != null && GameCamera.main.allowHDR)
            flags |= DLSSFeatureFlags.IsHDR;
        if (SystemInfo.usesReversedZBuffer)
            flags |= DLSSFeatureFlags.DepthInverted;

        DLSSCommandInitializationData init = new DLSSCommandInitializationData
        {
            inputRTWidth = (uint)spec.inputWidth,
            inputRTHeight = (uint)spec.inputHeight,
            outputRTWidth = (uint)spec.outputWidth,
            outputRTHeight = (uint)spec.outputHeight,
            quality = spec.quality,
            featureFlags = flags
        };

        CommandBuffer command = new CommandBuffer { name = "DSP DLSS Create" };
        try
        {
            _feature = _device.CreateFeature(command, init);
            if (_feature == null)
                throw new InvalidOperationException("CreateFeature returned null");
            Graphics.ExecuteCommandBuffer(command);
        }
        finally
        {
            command.Release();
        }

        _featureMode = spec.mode;
        _featureQuality = spec.quality;
        _featureFormat = sourceFormat;
        _inputWidth = spec.inputWidth;
        _inputHeight = spec.inputHeight;
        _outputWidth = spec.outputWidth;
        _outputHeight = spec.outputHeight;
        _featureSharpness = spec.sharpness;
        _featureSubmitFrame = Time.frameCount;
        _featureDebugValid = false;
        _inputDescriptionLogged = false;
        _resetHistory = true;

        Logger.LogWarning($"DLSS Create submitted: mode={GetModeLabel(spec.mode)}, quality={spec.quality}, " +
                          $"{spec.inputWidth}x{spec.inputHeight}->{spec.outputWidth}x{spec.outputHeight}, " +
                          $"format={sourceFormat}, sharpness={spec.sharpness:F3}, flags={flags}");
    }

    private static RenderTexture CreateRenderTexture(
        string name,
        int width,
        int height,
        RenderTextureFormat format,
        bool enableRandomWrite,
        FilterMode filterMode)
    {
        RenderTexture texture = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear)
        {
            name = name,
            enableRandomWrite = enableRandomWrite,
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
            antiAliasing = 1
        };
        if (!texture.Create())
        {
            UnityEngine.Object.Destroy(texture);
            throw new InvalidOperationException($"Failed to create {name} ({width}x{height} {format})");
        }
        return texture;
    }

    private void UpdateDebugState()
    {
        try
        {
            _device.UpdateDebugView(_debugView);
            bool wasValid = _featureDebugValid;
            _featureDebugValid = false;

            foreach (DLSSDebugFeatureInfos info in _debugView.dlssFeatureInfos)
            {
                DLSSCommandInitializationData init = info.initData;
                if (info.validFeature &&
                    init.inputRTWidth == (uint)_inputWidth &&
                    init.inputRTHeight == (uint)_inputHeight &&
                    init.outputRTWidth == (uint)_outputWidth &&
                    init.outputRTHeight == (uint)_outputHeight &&
                    init.quality == _featureQuality)
                {
                    _featureDebugValid = true;
                    RecordNativeJitterAudit(info.execData);
                    break;
                }
            }

            int frameAge = Time.frameCount - _featureSubmitFrame;
            if (_feature != null && (_featureDebugValid != wasValid || frameAge == 10))
            {
                Logger.LogWarning($"DLSS debug: validFeature={_featureDebugValid}, mode={GetModeLabel(_featureMode)}, " +
                                  $"device={_debugView.deviceVersion}, NGX={_debugView.ngxVersion}, frameAge={frameAge}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("DLSS debug view update failed: " + ex.Message);
        }
    }

    private void DestroyFeature()
    {
        if (_feature != null && _device != null)
        {
            try
            {
                CommandBuffer command = new CommandBuffer { name = "DSP DLSS Destroy" };
                try
                {
                    _device.DestroyFeature(command, _feature);
                    Graphics.ExecuteCommandBuffer(command);
                }
                finally
                {
                    command.Release();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("DLSS feature destroy failed: " + ex.Message);
            }
        }

        _feature = null;
        _featureDebugValid = false;
        _featureSubmitFrame = -1;
        _inputWidth = 0;
        _inputHeight = 0;
        _outputWidth = 0;
        _outputHeight = 0;
        _inputDescriptionLogged = false;
        _resetHistory = true;
        ResetJitterAudit();

        ReleaseRenderTexture(ref _preparedColor);
        ReleaseRenderTexture(ref _preparedDepth);
        ReleaseRenderTexture(ref _preparedMotion);
        ReleaseRenderTexture(ref _dlssOutput);
    }

    private static void ReleaseRenderTexture(ref RenderTexture texture)
    {
        if (texture == null)
            return;
        texture.Release();
        UnityEngine.Object.Destroy(texture);
        texture = null;
    }

    private void OnGUI()
    {
        bool toastVisible = Time.unscaledTime < _toastUntil;
        if (!_panelVisible && !_showInfo && !toastVisible)
            return;

        EnsureGuiStyles();
        GUI.depth = -10000;

        if (_showInfo)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.84f);
            GUI.Box(new Rect(18f, 18f, 900f, 170f), GUIContent.none);
            GUI.color = Color.white;

            string state = RequestedEnabled ? "ON" : "OFF";
            string dimensions = _inputWidth > 0
                ? $"DLSS input {_inputWidth}x{_inputHeight}  ->  output {_outputWidth}x{_outputHeight}"
                : "DLSS input pending";

            GUI.Label(new Rect(30f, 25f, 870f, 150f),
                $"DSP DLSS {PluginVersion}   [F8 CONTROL PANEL]   SR {state} / {GetModeLabel(_runtimeMode)}\n" +
                $"{_status}\nJitter: {_jitterStatus}\n{dimensions}   Execute={_executeCount}   Fallback={_fallbackCount}",
                _labelStyle);
        }
        else if (toastVisible)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.84f);
            GUI.Box(new Rect(18f, 18f, 420f, 52f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(30f, 27f, 390f, 32f), _toast, _toastStyle);
        }

        if (!_panelVisible)
            return;

        UICursor.forceShowCursor = true;
        float maxX = Mathf.Max(0f, Screen.width - _controlWindow.width);
        float maxY = Mathf.Max(0f, Screen.height - _controlWindow.height);
        _controlWindow.x = Mathf.Clamp(_controlWindow.x, 0f, maxX);
        _controlWindow.y = Mathf.Clamp(_controlWindow.y, 0f, maxY);
        _controlWindow = GUI.Window(ControlWindowId, _controlWindow, DrawControlWindow,
            $"DSP DLSS {PluginVersion}  -  F8 closes this panel");
    }

    private void DrawControlWindow(int windowId)
    {
        GUILayout.Space(8f);

        GUILayout.BeginHorizontal();
        GUILayout.Label("DLSS Super Resolution", _sectionStyle, GUILayout.Width(330f));
        if (GUILayout.Button(_runtimeEnabled ? "SR: ON" : "SR: OFF",
                _runtimeEnabled ? _activeButtonStyle : GUI.skin.button,
                GUILayout.Width(180f), GUILayout.Height(32f)))
        {
            SetSuperResolutionEnabled(!_runtimeEnabled);
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("Quality mode", _labelStyle);
        GUILayout.BeginHorizontal();
        DrawModeButton(DSPDLSSMode.UltraPerformance, "Ultra Perf");
        DrawModeButton(DSPDLSSMode.Performance, "Performance");
        DrawModeButton(DSPDLSSMode.Balanced, "Balanced");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        DrawModeButton(DSPDLSSMode.Quality, "Quality");
        DrawModeButton(DSPDLSSMode.DLAA, "DLAA");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();


        GUILayout.Space(8f);
        bool showInfo = GUILayout.Toggle(_showInfo, " Show compact technical info overlay");
        if (showInfo != _showInfo)
            SetInfoVisible(showInfo);

        GUILayout.Space(8f);
        string dimensions = _inputWidth > 0
            ? $"{_inputWidth}x{_inputHeight} -> {_outputWidth}x{_outputHeight}"
            : "input pending";
        GUILayout.Label($"API: {SystemInfo.graphicsDeviceType}    SR: {(RequestedEnabled ? "active" : "off")}    " +
                        $"Mode: {GetModeLabel(_runtimeMode)}\n{_status}\n{dimensions}    " +
                        $"Execute={_executeCount}  Fallback={_fallbackCount}", _labelStyle);

        GUILayout.Space(5f);
        GUILayout.Label("Jitter audit verifies the TAA sequence, the camera projection offset, the values submitted " +
                        "to Unity's DLSS command and the values reported back by the native NVIDIA debug view.",
            _warningStyle);

        if (GUILayout.Button("Close panel", GUILayout.Height(30f)))
            SetPanelVisible(false);

        GUI.DragWindow(new Rect(0f, 0f, _controlWindow.width, 30f));
    }

    private void DrawModeButton(DSPDLSSMode mode, string label)
    {
        GUIStyle style = _runtimeMode == mode ? _activeButtonStyle : GUI.skin.button;
        if (GUILayout.Button((_runtimeMode == mode ? "● " : string.Empty) + label,
                style, GUILayout.Width(166f), GUILayout.Height(30f)))
        {
            SetMode(mode);
        }
    }


    private void SetInfoVisible(bool visible)
    {
        _showInfo = visible;
        _showInfoConfig.Value = visible;
        Config.Save();
        Logger.LogInfo($"DLSS info overlay: {(visible ? "ON" : "OFF")}");
    }

    private void EnsureGuiStyles()
    {
        if (_labelStyle != null)
            return;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        _warningStyle = new GUIStyle(_labelStyle)
        {
            fontSize = 14,
            normal = { textColor = new Color(1f, 0.65f, 0.25f) }
        };
        _toastStyle = new GUIStyle(_labelStyle)
        {
            fontSize = 18,
            normal = { textColor = Color.white }
        };
        _sectionStyle = new GUIStyle(_labelStyle)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.35f, 0.85f, 1f) }
        };
        _activeButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.25f, 1f, 0.45f) }
        };
    }

    private void OnDestroy()
    {
        SetPanelVisible(false);
        RestoreGameAa();
        DestroyFeature();
        if (_debugView != null && _device != null)
        {
            try
            {
                _device.DeleteDebugView(_debugView);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("DLSS debug view deletion failed: " + ex.Message);
            }
        }
        _debugView = null;
        _harmony?.UnpatchSelf();
        Instance = null;
    }
}

internal static class NvUnityNative
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
    private static extern IntPtr GetModuleHandle(string moduleName);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procedureName);

    internal static bool HasBaseEventIdExport()
    {
        IntPtr module = GetModuleHandle("NVUnityPlugin.dll");
        return module != IntPtr.Zero && GetProcAddress(module, "NVUP_GetBaseEventId") != IntPtr.Zero;
    }
}


[HarmonyPatch(typeof(PostEffectController), "Update")]
internal static class PostEffectUpdatePatch
{
    private static void Postfix(PostEffectController __instance)
    {
        Plugin.Instance?.RuntimeTick(__instance);
    }
}

[HarmonyPatch(typeof(VFInput), nameof(VFInput.UpdateGameStates))]
internal static class DlssPanelInputCapturePatch
{
    private static void Postfix()
    {
        Plugin.Instance?.CaptureGameInputForPanel();
    }
}

[HarmonyPatch(typeof(UICursor), nameof(UICursor.EndCursorDetermine))]
internal static class DlssPanelCursorPatch
{
    private static void Prefix()
    {
        if (Plugin.Instance?.PanelVisible == true)
            UICursor.forceShowCursor = true;
    }
}

[HarmonyPatch(typeof(TaaComponent), nameof(TaaComponent.SetProjectionMatrix))]
internal static class TaaProjectionAuditPatch
{
    private static void Postfix(TaaComponent __instance)
    {
        Plugin.Instance?.CaptureJitterProjection(__instance);
    }
}

[HarmonyPatch(typeof(TaaComponent), nameof(TaaComponent.Render))]
internal static class TaaRenderPatch
{
    private static bool Prefix(TaaComponent __instance, RenderTexture source, RenderTexture destination)
    {
        Plugin plugin = Plugin.Instance;
        if (plugin == null)
            return true;
        return !plugin.TryExecute(__instance, source, destination);
    }
}
