using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.Settings.Graphics;
using GPUInstancer;
using HarmonyLib;
using Newtonsoft.Json;
using Unity.Collections;
using UnityEngine;

namespace CineKit
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.hysocs.cinekit";
        public const string Name = "Hysocs-CineKit";
        public const string Version = "1.1.0";

        private ConfigEntry<KeyboardShortcut> _menuKey;
        private ConfigEntry<KeyboardShortcut> _addPointKey;
        private ConfigEntry<KeyboardShortcut> _removePointKey;
        private ConfigEntry<bool> _freecamEnabled;
        private ConfigEntry<float> _moveSpeed;
        private ConfigEntry<float> _sprintSpeed;
        private ConfigEntry<float> _lookSensitivity;
        private ConfigEntry<KeyCode> _moveForwardKey;
        private ConfigEntry<KeyCode> _moveBackwardKey;
        private ConfigEntry<KeyCode> _moveLeftKey;
        private ConfigEntry<KeyCode> _moveRightKey;
        private ConfigEntry<KeyCode> _moveUpKey;
        private ConfigEntry<KeyCode> _moveDownKey;
        private ConfigEntry<KeyCode> _sprintKey;
        private ConfigEntry<bool> _motionSmoothing;
        private ConfigEntry<float> _positionSmoothing;
        private ConfigEntry<float> _rotationSmoothing;
        private ConfigEntry<float> _lodDistanceMultiplier;
        private ConfigEntry<float> _grassRenderDistance;
        private ConfigEntry<bool> _extendedCrossSceneMeshes;
        private ConfigEntry<float> _crossSceneMeshDistance;
        private ConfigEntry<bool> _showCrosshair;
        private ConfigEntry<bool> _hideRaidHud;
        private ConfigEntry<bool> _playerFollowsFreecam;
        private ConfigEntry<bool> _controlPlayerFromFreecam;
        private ConfigEntry<bool> _disableAimFovChange;
        private ConfigEntry<FreecamAntialiasingMode> _freecamAntialiasing;
        private ConfigEntry<string> _accentColor;
        private ConfigEntry<float> _windowX;
        private ConfigEntry<float> _windowY;
        private ConfigEntry<string> _savedPathTemplatesJson;
        private readonly Dictionary<
            ConfigEntry<bool>, ConfigEntry<KeyboardShortcut>>
            _toggleHotkeys =
                new Dictionary<
                    ConfigEntry<bool>, ConfigEntry<KeyboardShortcut>>();
        private ConfigEntry<bool> _toggleAwaitingHotkey;
        private ConfigEntry<KeyboardShortcut> _standaloneAwaitingHotkey;
        private int _toggleHotkeyCaptureStartedFrame = -1;
        private bool _menuOpen;
        private bool _menuShortcutLatched;
        private bool _fieldKitConflictResolved;
        private Rect _windowRect = new Rect(
            30f, 30f, 1000f, 820f);
        private CursorLockMode _savedCursorLock;
        private bool _savedCursorVisible;
        private Texture2D _menuCursorTexture;
        private bool _menuCursorApplied;
        private Camera _gameCamera;
        private Camera _sourceCamera;
        private bool _sourceCameraWasEnabled;
        private float _freecamFieldOfView;
        private bool _cameraDetached;
        private Transform _sourceCameraParent;
        private Vector3 _sourceCameraLocalPosition;
        private Quaternion _sourceCameraLocalRotation;
        private Vector3 _sourceCameraLocalScale;
        private readonly List<BodyRenderer> _localBodyGroups =
            new List<BodyRenderer>(16);
        private readonly List<BodyRendererState> _localBodyStates =
            new List<BodyRendererState>(32);
        private readonly HashSet<int> _localBodyRendererIds =
            new HashSet<int>();
        private bool _localBodyRenderOverridden;
        private int _nativeCameraCullingMask;
        private Player _freecamPlayer;
        private EPointOfView _savedPlayerPointOfView;
        private bool _playerPointOfViewOverridden;
        private bool _freecamWeaponHidden;
        private float _freecamPlayerEyeHeight = 1.6f;
        private bool _smoothingExpanded;
        private bool _movementKeysExpanded;
        private ConfigEntry<KeyCode> _movementAwaitingKey;
        private int _movementKeyCaptureStartedFrame = -1;
        private readonly List<DisablerCullingObjectBase> _indoorCullers =
            new List<DisablerCullingObjectBase>();
        private readonly List<Terrain> _mapTerrains =
            new List<Terrain>();
        private readonly HashSet<int> _indoorCullingStates =
            new HashSet<int>();
        private readonly HashSet<
            Koenigz.PerfectCulling.EFT.PerfectCullingCrossSceneGroup>
            _crossSceneMeshGroups =
                new HashSet<
                    Koenigz.PerfectCulling.EFT
                        .PerfectCullingCrossSceneGroup>();
        private readonly Dictionary<
            Koenigz.PerfectCulling.EFT.PerfectCullingCrossSceneGroup,
            bool> _crossSceneGroupCullingStates =
                new Dictionary<
                    Koenigz.PerfectCulling.EFT
                        .PerfectCullingCrossSceneGroup, bool>();
        private bool _crossSceneMeshScanNeeded = true;
        private float _nextIndoorCullingEvaluation;
        private bool _indoorCullersScanned;
        private Vector3 _lastEnvironmentCullingPosition;
        private bool _hasEnvironmentCullingPosition;
        private int _lastEnvironmentCullingFrame = -1;
        private static readonly FieldInfo CullingCollidersField =
            AccessTools.Field(typeof(DisablerCullingObjectBase), "_colliders");
        private static readonly FieldInfo CullingInverseCollidersField =
            AccessTools.Field(typeof(DisablerCullingObjectBase), "_inverseColliders");
        private static readonly FieldInfo AlphaVersionLabelField =
            AccessTools.Field(
                typeof(EFT.UI.PreloaderUI), "_alphaVersionLabel");
        private GameObject _hiddenVersionLabel;
        private GameObject _hiddenClientWatermark;
        private readonly Dictionary<GameObject, bool>
            _hiddenBattleStancePanels =
                new Dictionary<GameObject, bool>();
        private readonly Dictionary<Canvas, bool>
            _hiddenBattleUiCanvases =
                new Dictionary<Canvas, bool>();
        private bool _battleUiCanvasesScanned;
        private bool _battleStancePanelsScanned;
        private bool _versionLabelWasActive;
        private bool _clientWatermarkWasActive;
        private bool _freecamWatermarksHidden;
        private static readonly int[] CheckboxCheckPixels =
        {
            5, 10, 6, 9, 7, 8, 7, 9, 8, 9, 8, 10, 9, 10,
            9, 11, 10, 11, 10, 12, 11, 12, 11, 13, 12, 13, 12, 14
        };
        private static readonly string[] PanelNames =
        {
            "CAMERA", "PATH", "SCHEMATICS",
            "RECORD / PLAY", "OTHER"
        };
        private static readonly string[] AntialiasingNames =
            { "Gameplay", "Off", "FXAA", "TAA Low", "TAA High" };
        private static readonly string[] RecordingFpsNames =
            { "Native FPS", "Custom FPS" };
        private static readonly GUIContent FreecamContent = new GUIContent(
            "Freecam — Enable in raid after loading finishes",
            "Freecam can only be enabled after the raid has fully started.");
        private Vector3 _freePosition;
        private float _yaw;
        private float _pitch;
        private Vector3 _renderPosition;
        private Quaternion _renderRotation;
        private bool _renderPoseInitialized;
        private int _renderPoseFrame = -1;
        private Harmony _harmony;
        private GUISkin _skin;
        private bool _skinRefreshPending;
        private GUIStyle _headingStyle;
        private GUIStyle _tabStyle;
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private readonly List<CameraPathGroup> _pathGroups =
            new List<CameraPathGroup>();
        private readonly List<SavedPathTemplate> _savedPathTemplates =
            new List<SavedPathTemplate>();
        private string _templateName = "New Path";
        private int _selectedTemplate = -1;
        private readonly List<CameraPathPoint> _emptyPathPoints =
            new List<CameraPathPoint>();
        private int _selectedPathGroup = -1;
        private int _selectedPathPoint = -1;
        private bool _connectionDropdownOpen;
        private bool _entityDropdownOpen;
        private bool _pointTypeDropdownOpen;
        private int _pathSegment;
        private bool _pathPlaying;
        private bool _hidePathElementsDuringPlayback;
        private bool _recordingPath;
        private bool _recordCustomFps;
        private string _recordFpsText = "120";
        private string _recordFpsLimitText = "90";
        private int _recordingFps;
        private int _recordedFrames;
        private int _savedCaptureFramerate;
        private int _savedTargetFrameRate;
        private int _savedVSyncCount;
        private Process _ffmpegProcess;
        private string _ffmpegPath;
        private string _recordingVideoPath;
        private string _recordingAudioPath;
        private FileStream _recordingAudioStream;
        private BinaryWriter _recordingAudioWriter;
        private int _recordingAudioSamples;
        private int _recordingAudioChannels;
        private int _recordingAudioRate;
        private Texture2D _recordFrameTexture;
        private string _recordingStatus =
            "Video and game audio. Requires ffmpeg.exe in the CineKit plugin folder.";
        private string _lastRecordingPath;
        private ConfigEntry<bool> _pathLoop;
        private Vector2 _groupScroll;
        private Vector2 _pathScroll;
        private Vector2 _templateScroll;
        private Vector2 _panelScroll;
        private Material _pathMaterial;
        private int _selectedPanel;
        private float _pathCurrentSpeed;
        private float _pathStartDelayRemaining;
        private float _pathStopDelayRemaining;
        private float _pathStayRemaining;
        private float _pathSegmentProgress;
        private float _pathTotalDurationScale = 1f;
        private float _pathDistanceCarry;
        private float _pathTimeCarry;
        private int _worldDragPoint = -1;
        private int _worldDragHandle = -1;
        private int _worldHoverPoint = -1;
        private int _worldHoverHandle = -1;
        private float _worldDragDistance;
        private Vector3 _worldDragOffset;
        private Rect _curveEditorRect =
            new Rect(70f, 70f, 1050f, 720f);
        private int _curveDragPoint = -1;
        private int _curveDragHandle;
        private int _curveDragView;
        private bool _graphicsProfileCaptured;
        private EAntialiasingMode _savedAntialiasing;
        private EDLSSMode _savedDlssMode;
        private EFSR2Mode _savedFsr2Mode;
        private EFSR3Mode _savedFsr3Mode;
        private readonly Dictionary<
            GPUInstancerTerrainSettings, GrassDistanceState>
            _grassDistanceStates =
                new Dictionary<GPUInstancerTerrainSettings, GrassDistanceState>();
        private readonly Dictionary<
            GPUInstancerPrototype, GrassPrototypeDistanceState>
            _grassPrototypeDistanceStates =
                new Dictionary<GPUInstancerPrototype, GrassPrototypeDistanceState>();
        private bool _grassRefreshPending;
        private float _grassRefreshTime;

        private List<CameraPathPoint> _pathPoints =>
            _selectedPathGroup >= 0 &&
            _selectedPathGroup < _pathGroups.Count
                ? _pathGroups[_selectedPathGroup].Points
                : _emptyPathPoints;

        private struct BodyRendererState
        {
            public Renderer Renderer;
            public bool Enabled;
            public UnityEngine.Rendering.ShadowCastingMode ShadowCastingMode;
        }

        private struct GrassDistanceState
        {
            public float Maximum;
            public float LegacyMaximum;
        }

        private struct GrassPrototypeDistanceState
        {
            public float Maximum;
            public float OpticMaximum;
        }

        private enum FreecamAntialiasingMode
        {
            Gameplay,
            Off,
            FXAA,
            TAA_Low,
            TAA_High,
            // Retained only to migrate configs written by earlier builds.
            DLSS_Quality,
            DLSS_Balanced,
            DLSS_Performance,
            DLSS_UltraPerformance
        }

        private enum SchematicSpace
        {
            CameraRelative,
            WorldCoordinates
        }

        private enum CameraPathPointType
        {
            World,
            Entity
        }

        private enum EntityAttachmentPoint
        {
            Head,
            Chest
        }

        private sealed class CameraPathPoint
        {
            public CameraPathPointType Type;
            public Vector3 Position;
            public Vector3 LookTarget;
            public Vector3 InTangent;
            public Vector3 OutTangent;
            public string EntityProfileId;
            public EntityAttachmentPoint EntityAttachment;
            public Vector3 EntityOffset;
            public bool FollowEntityLookDirection;
            public Vector3 EntityAimOffset;
            public bool SwivelPathToNext = true;
            public float AttachmentResponse = 8f;
            public float AttachmentSpeed = 20f;
            [JsonIgnore] public Vector3 ResolvedPosition;
            [JsonIgnore] public bool ResolvedPositionInitialized;
            public float Speed = 6f;
            public float SegmentDuration;
            public float StartDelay;
            public float StopDelay;
            public float StayDuration;
            public float Acceleration = 4f;
            public float Deceleration = 4f;
            public int NextPoint = -1;
            public bool StopHere;
        }

        private sealed class CameraPathGroup
        {
            public string Name;
            public readonly List<CameraPathPoint> Points =
                new List<CameraPathPoint>();
            public float TotalDuration;
            public bool SmoothTransitions;
        }

        [Serializable]
        private sealed class SavedPathTemplate
        {
            public string Name;
            public SchematicSpace Space;
            public float TotalDuration;
            public bool SmoothTransitions;
            public List<SavedPathPoint> Points =
                new List<SavedPathPoint>();
            [JsonIgnore]
            public string SourcePath;
        }

        [Serializable]
        private sealed class SavedPathPoint
        {
            public CameraPathPointType Type;
            public float[] Position;
            public float[] LookTarget;
            public float[] InTangent;
            public float[] OutTangent;
            public string EntityProfileId;
            public EntityAttachmentPoint EntityAttachment;
            public float[] EntityOffset;
            public bool FollowEntityLookDirection;
            public float[] EntityAimOffset;
            public bool SwivelPathToNext = true;
            public float AttachmentResponse = 8f;
            public float AttachmentSpeed = 20f;
            public float Speed;
            public float SegmentDuration;
            public float StartDelay;
            public float StopDelay;
            public float StayDuration;
            public float Acceleration;
            public float Deceleration;
            public int NextPoint = -1;
            public bool StopHere;
        }

        private void Awake()
        {
            _instance = this;
            _menuKey = Config.Bind("Hotkeys", "Toggle Menu",
                new KeyboardShortcut(KeyCode.Home), "Open or close the CineKit menu.");
            _addPointKey = Config.Bind(
                "Hotkeys", "Add Camera Point",
                new KeyboardShortcut(KeyCode.Plus),
                "Add a point to the selected path group while freecam is active.");
            _removePointKey = Config.Bind(
                "Hotkeys", "Remove Camera Point",
                new KeyboardShortcut(KeyCode.Minus),
                "Remove the selected camera point while freecam is active.");
            _freecamEnabled = Config.Bind("Free Camera", "Enabled", false,
                "Disconnect the camera from the player body. Automatically " +
                "turns off outside an active raid.");
            _moveSpeed = Config.Bind("Free Camera", "Move Speed", 6f,
                new ConfigDescription("Normal movement speed.",
                    new AcceptableValueRange<float>(0.5f, 50f)));
            _sprintSpeed = Config.Bind("Free Camera", "Sprint Speed", 24f,
                new ConfigDescription("Movement speed while the sprint key is held.",
                    new AcceptableValueRange<float>(0.5f, 200f)));
            _lookSensitivity = Config.Bind("Free Camera", "Look Sensitivity", 2f,
                new ConfigDescription("Mouse-look sensitivity.",
                    new AcceptableValueRange<float>(0.1f, 10f)));
            _moveForwardKey = BindMovementKey("Move Forward", KeyCode.W);
            _moveBackwardKey = BindMovementKey("Move Backward", KeyCode.S);
            _moveLeftKey = BindMovementKey("Move Left", KeyCode.A);
            _moveRightKey = BindMovementKey("Move Right", KeyCode.D);
            _moveUpKey = BindMovementKey("Move Up", KeyCode.Space);
            _moveDownKey = BindMovementKey("Move Down", KeyCode.LeftControl);
            _sprintKey = BindMovementKey("Sprint", KeyCode.LeftShift);
            _motionSmoothing = Config.Bind(
                "Free Camera", "Cinematic Motion Smoothing", false,
                "Smooth camera movement without accumulating rendered frames.");
            _positionSmoothing = Config.Bind(
                "Free Camera", "Position Response", 12f,
                new ConfigDescription(
                    "How quickly the camera catches its target position. Lower is smoother.",
                    new AcceptableValueRange<float>(1f, 30f)));
            _rotationSmoothing = Config.Bind(
                "Free Camera", "Rotation Response", 18f,
                new ConfigDescription(
                    "How quickly the camera catches its target rotation. Lower is smoother.",
                    new AcceptableValueRange<float>(1f, 30f)));
            _lodDistanceMultiplier = Config.Bind(
                "Free Camera", "LOD Distance Multiplier", 2f,
                new ConfigDescription(
                    "Multiplies EFT's camera-calculated object LOD distance.",
                    new AcceptableValueRange<float>(1f, 25f)));
            _grassRenderDistance = Config.Bind(
                "Free Camera", "Grass Render Distance", 200f,
                new ConfigDescription(
                    "Maximum freecam grass/detail distance in world meters.",
                    new AcceptableValueRange<float>(25f, 500f)));
            _extendedCrossSceneMeshes = Config.Bind(
                "Free Camera", "Extended Cross-Scene Meshes", true,
                "Keep nearby Perfect Culling cross-scene mesh groups visible.");
            _crossSceneMeshDistance = Config.Bind(
                "Free Camera", "Cross-Scene Mesh Distance", 250f,
                new ConfigDescription(
                    "Freecam range for cross-scene rocks and map mesh groups.",
                    new AcceptableValueRange<float>(100f, 3000f)));
            _showCrosshair = Config.Bind(
                "Free Camera", "Show Crosshair", true,
                "Show the center-dot crosshair while controlling freecam.");
            _hideRaidHud = Config.Bind(
                "Free Camera", "Hide Raid HUD", true,
                "Hide EFT's combat HUD while freecam is active.");
            _playerFollowsFreecam = Config.Bind(
                "Free Camera", "Player Follows Freecam", false,
                "Move the hidden local player with freecam.");
            _controlPlayerFromFreecam = Config.Bind(
                "Free Camera", "Control Player From Freecam", false,
                "Keep the freecam view fixed while routing controls to the player.");
            _disableAimFovChange = Config.Bind(
                "Free Camera", "Disable Aim FOV Change", true,
                "Keep aiming the controlled player from changing the freecam field of view.");
            _freecamAntialiasing = Config.Bind(
                "Free Camera", "Antialiasing", FreecamAntialiasingMode.Gameplay,
                "Temporary basic antialiasing mode used only while freecam is active.");
            if (_freecamAntialiasing.Value >
                FreecamAntialiasingMode.TAA_High)
                _freecamAntialiasing.Value =
                    FreecamAntialiasingMode.Gameplay;
            _accentColor = Config.Bind("GUI Appearance", "Primary Color",
                "#78CFF5FF", "Primary RGBA accent color.");
            _windowX = Config.Bind("GUI Layout", "Main Window X", 30f);
            _windowY = Config.Bind("GUI Layout", "Main Window Y", 30f);
            _savedPathTemplatesJson = Config.Bind(
                "Saved Paths", "Templates JSON", "[]",
                "Portable camera paths stored relative to the free camera.");
            _pathLoop = Config.Bind(
                "Camera Path", "Loop Playback", false,
                "Loop camera path preview and playback.");
            if (_controlPlayerFromFreecam.Value)
                _playerFollowsFreecam.Value = false;
            ConfigureToggleHotkeys();
            _windowRect.x = _windowX.Value;
            _windowRect.y = _windowY.Value;
            AddPathGroup();
            LoadPathTemplatesFromConfig();
            _freecamEnabled.SettingChanged += OnFreecamSettingChanged;
            _playerFollowsFreecam.SettingChanged +=
                OnPlayerFollowsFreecamChanged;
            _controlPlayerFromFreecam.SettingChanged +=
                OnControlPlayerFromFreecamChanged;
            _freecamAntialiasing.SettingChanged +=
                OnFreecamAntialiasingChanged;
            _hideRaidHud.SettingChanged +=
                OnHideRaidHudChanged;
            _grassRenderDistance.SettingChanged +=
                OnGrassRenderDistanceChanged;
            _accentColor.SettingChanged += OnAccentChanged;
            _harmony = new Harmony(Guid);
            InstallPatches();
            _fieldKitConflictResolved = ResolveFieldKitHomeConflict();
            Logger.LogInfo("CineKit 1.1.0 loaded. Press HOME to open the menu.");
        }

        private void Update()
        {
            DisableFreecamOutsideRaid();
            UpdateToggleHotkeys();
            CaptureAwaitingMovementKey();
            UpdatePathEditingHotkeys();
            UpdateEntityPathPoints();
            UpdateGrassDistanceRefresh();
            if (!_fieldKitConflictResolved)
                _fieldKitConflictResolved = ResolveFieldKitHomeConflict();
            if (_recordingPath &&
                Input.GetKeyDown(KeyCode.Escape))
            {
                StopPathPlayback();
                _recordingStatus =
                    "Finalizing the partial recording...";
                FinishPathRecording(true);
                return;
            }
            HandleMenuShortcutUpdate();
            MaintainMenuCursor();
            if (!_freecamEnabled.Value || _menuOpen)
                return;
            if (!_cameraDetached)
                return;

            if (_pathPlaying)
            {
                UpdatePathPlayback();
                SyncPlayerToFreeCamera();
                ApplyFreeCameraPose();
                return;
            }
            if (_controlPlayerFromFreecam.Value)
            {
                ApplyFreeCameraPose();
                return;
            }
            float speed = Input.GetKey(_sprintKey.Value)
                ? _sprintSpeed.Value
                : _moveSpeed.Value;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 movement = Vector3.zero;
            if (Input.GetKey(_moveForwardKey.Value))
                movement += Vector3.forward;
            if (Input.GetKey(_moveBackwardKey.Value))
                movement += Vector3.back;
            if (Input.GetKey(_moveLeftKey.Value))
                movement += Vector3.left;
            if (Input.GetKey(_moveRightKey.Value))
                movement += Vector3.right;
            if (Input.GetKey(_moveUpKey.Value))
                movement += Vector3.up;
            if (Input.GetKey(_moveDownKey.Value))
                movement += Vector3.down;
            if (movement.sqrMagnitude > 1f) movement.Normalize();
            _freePosition += rotation * movement * speed * Time.unscaledDeltaTime;
            _yaw += Input.GetAxisRaw("Mouse X") * _lookSensitivity.Value;
            _pitch = Mathf.Clamp(
                _pitch -
                Input.GetAxisRaw("Mouse Y") * _lookSensitivity.Value,
                -89f, 89f);
            SyncPlayerToFreeCamera();
            ApplyFreeCameraPose();
            HandleWorldPathEditing();
        }

        private void LateUpdate()
        {
            MaintainMenuCursor();
            if (!_freecamEnabled.Value || !EnsureFreeCamera())
                return;

            HideFreecamWatermarks();
            // Keep EFT's non-Unity visibility observers on the same position
            // as its real raid camera. Rendering settings remain game-owned.
            SyncPlayerToFreeCamera();
            UpdateEnvironmentCullingPosition();
            UpdateIndoorCullingPosition();
            UpdateCrossSceneMeshVisibility();
            ApplyFreeCameraPose();
        }

        private bool EnsureFreeCamera()
        {
            if (_cameraDetached && _gameCamera != null)
                return true;
            if (!IsRaidRunning())
                return false;

            GameWorld world = Singleton<GameWorld>.Instance;
            if (world == null || world.MainPlayer == null)
                return false;

            EFT.CameraControl.CameraManager cameraManager =
                EFT.CameraControl.CameraManager.Instance;
            Camera source = cameraManager != null
                ? cameraManager.Camera
                : null;
            if (source == null)
                return false;

            Transform sourceTransform = source.transform;
            _sourceCamera = source;
            _sourceCameraWasEnabled = source.enabled;
            _sourceCameraParent = sourceTransform.parent;
            _sourceCameraLocalPosition = sourceTransform.localPosition;
            _sourceCameraLocalRotation = sourceTransform.localRotation;
            _sourceCameraLocalScale = sourceTransform.localScale;
            // Keep EFT's native FPS camera and its complete render stack.
            // Do not clone it, detach it, or copy selected settings: those
            // approaches lose parent-driven effects and game-owned state.
            _gameCamera = source;
            _freecamFieldOfView = source.fieldOfView;
            _freePosition = sourceTransform.position;
            Vector3 angles = sourceTransform.rotation.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x > 180f ? angles.x - 360f : angles.x;
            _renderPosition = _freePosition;
            _renderRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _renderPoseInitialized = true;
            _renderPoseFrame = -1;
            _freecamPlayer = world.MainPlayer;
            if (!_playerFollowsFreecam.Value)
                ShowLocalPlayerThirdPerson();
            _freecamPlayerEyeHeight = Mathf.Clamp(
                sourceTransform.position.y -
                _freecamPlayer.Position.y,
                0.5f, 2.5f);
            _cameraDetached = true;
            HideFreecamWatermarks();
            SyncPlayerToFreeCamera();
            ApplyFreecamGraphicsProfile();
            ApplyGrassRenderDistance();
            ScheduleGrassSpatialRefresh();
            ApplyFreeCameraPose();
            Camera.onPreCull += OnCameraPreCull;
            Camera.onPreRender += OnCameraPreRender;
            Camera.onPostRender += OnCameraPostRender;
            Logger.LogInfo("Free camera enabled.");
            return true;
        }

        private void OnCameraPreCull(Camera renderingCamera)
        {
            if (_freecamEnabled.Value && renderingCamera == _gameCamera)
            {
                ApplyFreeCameraPose();
                if (_playerFollowsFreecam.Value)
                    HideLocalPlayerBody();
                else
                    ShowLocalThirdPersonBody();
                UpdateEnvironmentCullingPosition();
                UpdateIndoorCullingPosition();
            }
        }

        private void OnCameraPostRender(Camera renderingCamera)
        {
            if (renderingCamera != _gameCamera)
                return;
            if (!_playerFollowsFreecam.Value)
                RestoreLocalBodyRenderers();
            DrawCameraPath(renderingCamera);
        }

        private void OnCameraPreRender(Camera renderingCamera)
        {
            if (renderingCamera != _gameCamera ||
                !IsFreecamActive)
                return;
            ApplyFreeCameraPose();
        }

        private void HideLocalPlayerBody()
        {
            RestoreLocalPlayerPointOfView();
            if (_localBodyRenderOverridden)
            {
                if (_freecamPlayer != null)
                    _freecamPlayer.HideWeapon();
                for (int i = 0; i < _localBodyStates.Count; i++)
                {
                    Renderer renderer =
                        _localBodyStates[i].Renderer;
                    if (renderer == null)
                        continue;
                    renderer.enabled = false;
                    renderer.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                return;
            }

            GameWorld world = Singleton<GameWorld>.Instance;
            Player player = world != null ? world.MainPlayer : null;
            if (player == null || player.PlayerBody == null ||
                _gameCamera == null)
                return;

            _localBodyGroups.Clear();
            _localBodyRendererIds.Clear();
            _nativeCameraCullingMask = _gameCamera.cullingMask;
            _localBodyRenderOverridden = true;
            player.PlayerBody.GetBodyRenderersNonAlloc(_localBodyGroups);
            for (int i = 0; i < _localBodyGroups.Count; i++)
            {
                Renderer[] renderers = _localBodyGroups[i].Renderers;
                if (renderers == null)
                    continue;
                for (int j = 0; j < renderers.Length; j++)
                    SetLocalRendererState(renderers[j], false);
            }
            Player.AbstractHandsController hands =
                player.HandsController;
            Renderer[] handsRenderers = hands != null
                ? hands.GetComponentsInChildren<Renderer>(true)
                : null;
            if (handsRenderers != null)
                for (int i = 0; i < handsRenderers.Length; i++)
                    SetLocalRendererState(handsRenderers[i], false);
            if (!_freecamWeaponHidden)
            {
                player.HideWeapon();
                _freecamWeaponHidden = true;
            }
        }

        private void ShowLocalThirdPersonBody()
        {
            RestoreLocalBodyRenderers();
            ShowLocalPlayerThirdPerson();

            Player player = _freecamPlayer;
            if (player == null || player.PlayerBody == null ||
                _gameCamera == null)
                return;

            _localBodyGroups.Clear();
            _localBodyRendererIds.Clear();
            _nativeCameraCullingMask = _gameCamera.cullingMask;
            _localBodyRenderOverridden = true;
            player.PlayerBody.GetBodyRenderersNonAlloc(_localBodyGroups);

            int bodyLayerMask = 0;
            for (int i = 0; i < _localBodyGroups.Count; i++)
            {
                Renderer[] renderers = _localBodyGroups[i].Renderers;
                if (renderers == null)
                    continue;
                for (int j = 0; j < renderers.Length; j++)
                {
                    Renderer renderer = renderers[j];
                    // Third-person POV disables the first-person arms/body
                    // renderers. Do not resurrect those while overriding
                    // the freecam's otherwise-excluded player layers.
                    if (renderer == null || !renderer.enabled)
                        continue;
                    if (!SetLocalRendererState(renderer, true))
                        continue;
                    bodyLayerMask |= 1 << renderer.gameObject.layer;
                }
            }
            _gameCamera.cullingMask |= bodyLayerMask;
        }

        private bool SetLocalRendererState(Renderer renderer, bool visible)
        {
            if (renderer == null ||
                !_localBodyRendererIds.Add(renderer.GetInstanceID()))
                return false;
            _localBodyStates.Add(new BodyRendererState
            {
                Renderer = renderer,
                Enabled = renderer.enabled,
                ShadowCastingMode = renderer.shadowCastingMode
            });
            renderer.enabled = visible;
            renderer.shadowCastingMode = visible
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            return true;
        }

        private void RestoreLocalBodyRenderers()
        {
            if (_freecamWeaponHidden)
            {
                if (_freecamPlayer != null)
                    _freecamPlayer.RevealWeapon();
                _freecamWeaponHidden = false;
            }
            if (!_localBodyRenderOverridden)
                return;

            for (int i = 0; i < _localBodyStates.Count; i++)
            {
                BodyRendererState state = _localBodyStates[i];
                if (state.Renderer == null)
                    continue;
                state.Renderer.enabled = state.Enabled;
                state.Renderer.shadowCastingMode =
                    state.ShadowCastingMode;
            }

            if (_gameCamera != null)
                _gameCamera.cullingMask = _nativeCameraCullingMask;
            _localBodyStates.Clear();
            _localBodyRendererIds.Clear();
            _localBodyRenderOverridden = false;
        }

        private void ShowLocalPlayerThirdPerson()
        {
            if (_freecamPlayer == null)
                return;
            if (!_playerPointOfViewOverridden)
            {
                _savedPlayerPointOfView = _freecamPlayer.PointOfView;
                _playerPointOfViewOverridden = true;
            }
            if (_freecamPlayer.PointOfView != EPointOfView.ThirdPerson)
                _freecamPlayer.PointOfView = EPointOfView.ThirdPerson;
        }

        private void RestoreLocalPlayerPointOfView()
        {
            if (!_playerPointOfViewOverridden)
                return;
            if (_freecamPlayer != null)
                _freecamPlayer.PointOfView = _savedPlayerPointOfView;
            _playerPointOfViewOverridden = false;
        }

        private void SyncPlayerToFreeCamera()
        {
            if (!_cameraDetached ||
                _freecamPlayer == null ||
                !_playerFollowsFreecam.Value)
                return;
            TeleportPlayerToFreeCamera();
        }

        private void TeleportPlayerToFreeCamera()
        {
            if (!_cameraDetached || _freecamPlayer == null)
                return;
            Vector3 playerPosition =
                CurrentCameraPosition -
                Vector3.up * _freecamPlayerEyeHeight;
            _freecamPlayer.Teleport(playerPosition, false);
            MovementContext movement =
                _freecamPlayer.MovementContext;
            SimpleCharacterController controller =
                movement != null
                    ? movement.CharacterController
                        as SimpleCharacterController
                    : null;
            if (controller != null)
                controller.isGrounded = true;
        }

        private void ApplyFreeCameraPose()
        {
            if (!_cameraDetached || _gameCamera == null)
                return;

            Vector3 targetPosition = _freePosition;
            Quaternion targetRotation =
                Quaternion.Euler(_pitch, _yaw, 0f);
            if (!_renderPoseInitialized || !_motionSmoothing.Value)
            {
                _renderPosition = targetPosition;
                _renderRotation = targetRotation;
                _renderPoseInitialized = true;
            }
            else if (_renderPoseFrame != Time.frameCount)
            {
                float delta = _recordingPath
                    ? 1f / Mathf.Max(1, _recordingFps)
                    : Mathf.Min(
                        Time.unscaledDeltaTime, 1f / 15f);
                float positionBlend = 1f - Mathf.Exp(
                    -_positionSmoothing.Value * delta);
                float rotationBlend = 1f - Mathf.Exp(
                    -_rotationSmoothing.Value * delta);
                _renderPosition = Vector3.Lerp(
                    _renderPosition, targetPosition, positionBlend);
                _renderRotation = Quaternion.Slerp(
                    _renderRotation, targetRotation, rotationBlend);
                _renderPoseFrame = Time.frameCount;
            }
            _gameCamera.transform.SetPositionAndRotation(
                _renderPosition, _renderRotation);
            if (_controlPlayerFromFreecam.Value &&
                _disableAimFovChange.Value)
                _gameCamera.fieldOfView = _freecamFieldOfView;
        }

        private void AddPathPoint()
        {
            CameraPathPoint point = new CameraPathPoint
            {
                Position = _freePosition,
                LookTarget =
                    _freePosition +
                    Quaternion.Euler(_pitch, _yaw, 0f) *
                    Vector3.forward * 10f,
                Speed = _moveSpeed.Value
            };
            _pathPoints.Add(point);
            if (_pathPoints.Count > 1)
            {
                CameraPathPoint previous =
                    _pathPoints[_pathPoints.Count - 2];
                Vector3 third =
                    (point.Position -
                     GetPointPosition(previous)) / 3f;
                previous.OutTangent = third;
                point.InTangent = -third;
                if (previous.NextPoint < 0)
                    previous.NextPoint =
                        _pathPoints.Count - 1;
            }
            _selectedPathPoint = _pathPoints.Count - 1;
        }

        private void AddPathGroup()
        {
            _pathGroups.Add(new CameraPathGroup
            {
                Name = "Path " + (_pathGroups.Count + 1)
            });
            _selectedPathGroup = _pathGroups.Count - 1;
            _selectedPathPoint = -1;
            StopPathPlayback();
        }

        private void UpdateEntityPathPoints()
        {
            float delta = Mathf.Min(
                Time.unscaledDeltaTime, 1f / 15f);
            for (int i = 0; i < _pathPoints.Count; i++)
            {
                CameraPathPoint point = _pathPoints[i];
                if (point.Type != CameraPathPointType.Entity)
                    continue;
                Player target = FindPathPointEntity(point);
                if (target == null)
                    continue;
                Quaternion targetRotation =
                    point.FollowEntityLookDirection
                        ? GetEntityLookRotation(target)
                        : GetEntityBodyRotation(target);
                Vector3 desired =
                    GetEntityAttachmentPosition(
                        target, point.EntityAttachment) +
                    targetRotation * point.EntityOffset;
                if (!point.ResolvedPositionInitialized)
                {
                    point.ResolvedPosition = desired;
                    point.ResolvedPositionInitialized = true;
                    continue;
                }
                Vector3 softened = point.AttachmentResponse <= 0f
                    ? desired
                    : Vector3.Lerp(
                        point.ResolvedPosition,
                        desired,
                        1f - Mathf.Exp(
                            -point.AttachmentResponse * delta));
                Vector3 previousPosition =
                    point.ResolvedPosition;
                Vector3 nextPosition =
                    point.AttachmentSpeed <= 0f
                        ? softened
                        : Vector3.MoveTowards(
                            point.ResolvedPosition,
                            softened,
                            point.AttachmentSpeed * delta);
                RotateConnectedTangentsForMovedPoint(
                    point, previousPosition, nextPosition);
                point.ResolvedPosition = nextPosition;
                UpdateEntityOutgoingConnection(point);
            }
            // A moving entity can also be the destination of another
            // attached segment, so resolve every swivel after all anchors
            // have reached their final position for this frame.
            for (int i = 0; i < _pathPoints.Count; i++)
                UpdateEntityOutgoingConnection(_pathPoints[i]);
        }

        private Player FindPathPointEntity(CameraPathPoint point)
        {
            GameWorld world = Singleton<GameWorld>.Instance;
            if (world == null)
                return null;
            if (string.IsNullOrEmpty(point.EntityProfileId))
                return world.MainPlayer;
            foreach (IPlayer candidate in world.RegisteredPlayers)
            {
                Player player = candidate as Player;
                if (player != null &&
                    player.ProfileId == point.EntityProfileId)
                    return player;
            }
            return null;
        }

        private static Quaternion GetEntityBodyRotation(Player player) =>
            Quaternion.Euler(
                0f, player.Transform.eulerAngles.y, 0f);

        private static Quaternion GetEntityLookRotation(Player player)
        {
            Vector3 direction = player.LookDirection;
            return direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(
                    direction.normalized, Vector3.up)
                : GetEntityBodyRotation(player);
        }

        private static Vector3 GetEntityAttachmentPosition(
            Player player, EntityAttachmentPoint attachment)
        {
            if (player.PlayerBones != null)
            {
                if (attachment == EntityAttachmentPoint.Head &&
                    player.PlayerBones.Head != null)
                    return player.PlayerBones.Head.position;
                if (attachment == EntityAttachmentPoint.Chest &&
                    player.PlayerBones.Ribcage != null)
                    return player.PlayerBones.Ribcage.position;
            }
            return player.Position + Vector3.up *
                (attachment == EntityAttachmentPoint.Head
                    ? 1.6f
                    : 1.15f);
        }

        private Vector3 GetPointPosition(CameraPathPoint point) =>
            point.Type == CameraPathPointType.Entity &&
            point.ResolvedPositionInitialized
                ? point.ResolvedPosition
                : point.Position;

        private Vector3 GetPointLookTarget(CameraPathPoint point)
        {
            Vector3 pointPosition = GetPointPosition(point);
            if (point.Type != CameraPathPointType.Entity ||
                !point.FollowEntityLookDirection)
                return pointPosition +
                    (point.LookTarget - point.Position);
            Player target = FindPathPointEntity(point);
            return target == null
                ? pointPosition +
                  (point.LookTarget - point.Position)
                : pointPosition +
                  GetEntityLookRotation(target) *
                  point.EntityAimOffset;
        }

        private void UpdateEntityOutgoingConnection(
            CameraPathPoint point)
        {
            if (point.Type != CameraPathPointType.Entity ||
                !point.SwivelPathToNext)
                return;
            int pointIndex = _pathPoints.IndexOf(point);
            int targetIndex = point.NextPoint;
            if (pointIndex < 0 ||
                targetIndex < 0 ||
                targetIndex >= _pathPoints.Count ||
                targetIndex == pointIndex)
                return;
            CameraPathPoint target = _pathPoints[targetIndex];
            Vector3 direction =
                GetPointPosition(target) -
                GetPointPosition(point);
            float distance = direction.magnitude;
            if (distance <= 0.0001f)
            {
                point.OutTangent = Vector3.zero;
                target.InTangent = Vector3.zero;
                return;
            }
            direction /= distance;
            float maximumLength = distance / 3f;
            float outgoingLength = point.OutTangent.magnitude;
            float incomingLength = target.InTangent.magnitude;
            if (outgoingLength <= 0.0001f)
                outgoingLength = maximumLength;
            if (incomingLength <= 0.0001f)
                incomingLength = maximumLength;
            point.OutTangent =
                direction *
                Mathf.Min(outgoingLength, maximumLength);
            target.InTangent =
                -direction *
                Mathf.Min(incomingLength, maximumLength);
        }

        private void DeleteSelectedPathGroup()
        {
            if (_selectedPathGroup < 0 ||
                _selectedPathGroup >= _pathGroups.Count)
                return;
            StopPathPlayback();
            _pathGroups.RemoveAt(_selectedPathGroup);
            if (_pathGroups.Count == 0)
                AddPathGroup();
            else
                _selectedPathGroup = Mathf.Clamp(
                    _selectedPathGroup, 0,
                    _pathGroups.Count - 1);
            _selectedPathPoint = -1;
        }

        private void LoadPathTemplatesFromConfig()
        {
            _savedPathTemplates.Clear();
            try
            {
                List<SavedPathTemplate> loaded =
                    JsonConvert.DeserializeObject<
                        List<SavedPathTemplate>>(
                        _savedPathTemplatesJson.Value);
                if (loaded != null)
                    foreach (SavedPathTemplate template in loaded)
                        if (template != null &&
                            template.Points != null)
                        {
                            bool hasConnections =
                                template.Points.Exists(
                                    point =>
                                        point.NextPoint >= 0);
                            if (!hasConnections)
                                for (int i = 0;
                                     i <
                                     template.Points.Count - 1;
                                     i++)
                                    template.Points[i].NextPoint =
                                        i + 1;
                            _savedPathTemplates.Add(template);
                        }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Saved path templates could not be loaded: " +
                    exception.Message);
            }
            LoadPathTemplatesFromDirectory();
            _selectedTemplate = _savedPathTemplates.Count > 0
                ? 0
                : -1;
        }

        private static string SchematicsDirectory =>
            Path.Combine(
                BepInEx.Paths.PluginPath,
                "Hysocs-CineKit", "Schematics");

        private void LoadPathTemplatesFromDirectory()
        {
            try
            {
                Directory.CreateDirectory(SchematicsDirectory);
                string[] files = Directory.GetFiles(
                    SchematicsDirectory, "*.json");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    SavedPathTemplate template =
                        JsonConvert.DeserializeObject<SavedPathTemplate>(
                            File.ReadAllText(file));
                    if (template == null ||
                        template.Points == null ||
                        template.Points.Count == 0)
                        continue;
                    template.SourcePath = file;
                    int existing = _savedPathTemplates.FindIndex(
                        saved => string.Equals(
                            saved.Name, template.Name,
                            StringComparison.OrdinalIgnoreCase));
                    if (existing >= 0)
                        _savedPathTemplates[existing] = template;
                    else
                        _savedPathTemplates.Add(template);
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Schematic files could not be loaded: " +
                    exception.Message);
            }
        }

        private void SavePathTemplateFile(SavedPathTemplate template)
        {
            try
            {
                Directory.CreateDirectory(SchematicsDirectory);
                string safeName = string.Join(
                    "_", template.Name.Split(
                        Path.GetInvalidFileNameChars(),
                        StringSplitOptions.RemoveEmptyEntries)).Trim();
                if (string.IsNullOrWhiteSpace(safeName))
                    safeName = "Path";
                template.SourcePath = Path.Combine(
                    SchematicsDirectory, safeName + ".json");
                File.WriteAllText(
                    template.SourcePath,
                    JsonConvert.SerializeObject(
                        template, Formatting.Indented));
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Schematic file could not be saved: " +
                    exception.Message);
            }
        }

        private void SavePathTemplatesToConfig()
        {
            _savedPathTemplatesJson.Value =
                JsonConvert.SerializeObject(
                    _savedPathTemplates, Formatting.None);
            Config.Save();
        }

        private bool TryGetCameraAnchor(
            out Vector3 origin, out Quaternion rotation)
        {
            if (!_cameraDetached || _gameCamera == null)
            {
                origin = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }
            origin = _freePosition;
            rotation = Quaternion.Euler(0f, _yaw, 0f);
            return true;
        }

        private void SaveCurrentPathTemplate(SchematicSpace space)
        {
            if (_pathPoints.Count == 0)
                return;
            bool relative =
                space == SchematicSpace.CameraRelative;
            Vector3 origin = Vector3.zero;
            Quaternion anchorRotation = Quaternion.identity;
            if (relative &&
                !TryGetCameraAnchor(
                    out origin, out anchorRotation))
                return;

            string name = string.IsNullOrWhiteSpace(_templateName)
                ? "Path " + (_savedPathTemplates.Count + 1)
                : _templateName.Trim();
            SavedPathTemplate template =
                new SavedPathTemplate
                {
                    Name = name,
                    Space = space,
                    TotalDuration =
                        _pathGroups[_selectedPathGroup].TotalDuration,
                    SmoothTransitions =
                        _pathGroups[_selectedPathGroup].SmoothTransitions
                };
            Quaternion inverse = Quaternion.Inverse(anchorRotation);
            foreach (CameraPathPoint point in _pathPoints)
            {
                template.Points.Add(new SavedPathPoint
                {
                    Type = point.Type,
                    Position = ToArray(
                        inverse * (point.Position - origin)),
                    LookTarget = ToArray(
                        inverse * (point.LookTarget - origin)),
                    InTangent = ToArray(
                        inverse * point.InTangent),
                    OutTangent = ToArray(
                        inverse * point.OutTangent),
                    EntityProfileId = point.EntityProfileId,
                    EntityAttachment = point.EntityAttachment,
                    EntityOffset = ToArray(point.EntityOffset),
                    FollowEntityLookDirection =
                        point.FollowEntityLookDirection,
                    EntityAimOffset =
                        ToArray(point.EntityAimOffset),
                    SwivelPathToNext =
                        point.SwivelPathToNext,
                    AttachmentResponse = point.AttachmentResponse,
                    AttachmentSpeed = point.AttachmentSpeed,
                    Speed = point.Speed,
                    SegmentDuration = point.SegmentDuration,
                    StartDelay = point.StartDelay,
                    StopDelay = point.StopDelay,
                    StayDuration = point.StayDuration,
                    Acceleration = point.Acceleration,
                    Deceleration = point.Deceleration,
                    NextPoint = point.NextPoint,
                    StopHere = point.StopHere
                });
            }

            int existing = _savedPathTemplates.FindIndex(
                saved => string.Equals(
                    saved.Name, name,
                    StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                _savedPathTemplates[existing] = template;
                _selectedTemplate = existing;
            }
            else
            {
                _savedPathTemplates.Add(template);
                _selectedTemplate =
                    _savedPathTemplates.Count - 1;
            }
            SavePathTemplateFile(template);
            SavePathTemplatesToConfig();
        }

        private void LoadSelectedPathTemplate()
        {
            if (_selectedTemplate < 0 ||
                _selectedTemplate >= _savedPathTemplates.Count)
                return;

            SavedPathTemplate template =
                _savedPathTemplates[_selectedTemplate];
            Vector3 origin = Vector3.zero;
            Quaternion anchorRotation = Quaternion.identity;
            if (template.Space == SchematicSpace.CameraRelative &&
                !TryGetCameraAnchor(
                    out origin, out anchorRotation))
                return;
            AddPathGroup();
            CameraPathGroup group =
                _pathGroups[_selectedPathGroup];
            group.Name = template.Name;
            group.TotalDuration =
                Mathf.Max(0f, template.TotalDuration);
            group.SmoothTransitions =
                template.SmoothTransitions;
            group.Points.Clear();
            foreach (SavedPathPoint saved in template.Points)
            {
                if (!IsVector(saved.Position) ||
                    !IsVector(saved.LookTarget) ||
                    !IsVector(saved.InTangent) ||
                    !IsVector(saved.OutTangent))
                    continue;
                group.Points.Add(new CameraPathPoint
                {
                    Type = saved.Type,
                    Position = origin +
                        anchorRotation * FromArray(saved.Position),
                    LookTarget = origin +
                        anchorRotation * FromArray(saved.LookTarget),
                    InTangent =
                        anchorRotation * FromArray(saved.InTangent),
                    OutTangent =
                        anchorRotation * FromArray(saved.OutTangent),
                    EntityProfileId = saved.EntityProfileId,
                    EntityAttachment = saved.EntityAttachment,
                    EntityOffset = IsVector(saved.EntityOffset)
                        ? FromArray(saved.EntityOffset)
                        : Vector3.zero,
                    FollowEntityLookDirection =
                        saved.FollowEntityLookDirection,
                    EntityAimOffset =
                        IsVector(saved.EntityAimOffset)
                            ? FromArray(saved.EntityAimOffset)
                            : Vector3.forward * 10f,
                    SwivelPathToNext =
                        saved.SwivelPathToNext,
                    AttachmentResponse =
                        Mathf.Max(0f, saved.AttachmentResponse),
                    AttachmentSpeed =
                        Mathf.Max(0f, saved.AttachmentSpeed),
                    Speed = Mathf.Max(0.1f, saved.Speed),
                    SegmentDuration =
                        Mathf.Max(0f, saved.SegmentDuration),
                    StartDelay = Mathf.Max(0f, saved.StartDelay),
                    StopDelay = Mathf.Max(0f, saved.StopDelay),
                    StayDuration =
                        Mathf.Max(0f, saved.StayDuration),
                    Acceleration =
                        Mathf.Max(0.1f, saved.Acceleration),
                    Deceleration =
                        Mathf.Max(0.1f, saved.Deceleration),
                    NextPoint = saved.NextPoint,
                    StopHere = saved.StopHere
                });
            }
            for (int i = 0; i < group.Points.Count; i++)
                if (group.Points[i].NextPoint < 0 ||
                    group.Points[i].NextPoint >=
                    group.Points.Count ||
                    group.Points[i].NextPoint == i)
                    group.Points[i].NextPoint = -1;
            _selectedPathPoint =
                group.Points.Count > 0 ? 0 : -1;
        }

        private void DeleteSelectedPathTemplate()
        {
            if (_selectedTemplate < 0 ||
                _selectedTemplate >= _savedPathTemplates.Count)
                return;
            SavedPathTemplate template =
                _savedPathTemplates[_selectedTemplate];
            if (!string.IsNullOrEmpty(template.SourcePath) &&
                File.Exists(template.SourcePath))
                File.Delete(template.SourcePath);
            _savedPathTemplates.RemoveAt(_selectedTemplate);
            _selectedTemplate = _savedPathTemplates.Count == 0
                ? -1
                : Mathf.Clamp(
                    _selectedTemplate, 0,
                    _savedPathTemplates.Count - 1);
            SavePathTemplatesToConfig();
        }

        private static float[] ToArray(Vector3 value) =>
            new[] { value.x, value.y, value.z };

        private static Vector3 FromArray(float[] value) =>
            new Vector3(value[0], value[1], value[2]);

        private static bool IsVector(float[] value) =>
            value != null && value.Length == 3;

        private void StartPathPlayback(
            bool hidePathElements = false)
        {
            if (_pathPoints.Count < 2)
                return;
            _pathPlaying = true;
            _hidePathElementsDuringPlayback =
                hidePathElements;
            _pathSegment = 0;
            _pathCurrentSpeed = 0f;
            _pathSegmentProgress = 0f;
            _pathDistanceCarry = 0f;
            _pathTimeCarry = 0f;
            _pathTotalDurationScale =
                CalculateTotalDurationScale();
            _pathStartDelayRemaining =
                _pathPoints[0].StartDelay;
            _pathStopDelayRemaining = 0f;
            _pathStayRemaining =
                _pathPoints[0].StayDuration;
            SetFreePose(_pathPoints[0]);
        }

        private void StopPathPlayback()
        {
            _pathPlaying = false;
            _hidePathElementsDuringPlayback = false;
            _pathCurrentSpeed = 0f;
            _pathDistanceCarry = 0f;
            _pathTimeCarry = 0f;
            _pathTotalDurationScale = 1f;
            _pathStartDelayRemaining = 0f;
            _pathStopDelayRemaining = 0f;
            _pathStayRemaining = 0f;
        }

        private void HideFreecamWatermarks()
        {
            if (_freecamWatermarksHidden)
            {
                if (_hideRaidHud.Value)
                {
                    HideBattleUiCanvases();
                    HideBattleStancePanels();
                }
                else
                    RestoreBattleHud();
                if (_hiddenVersionLabel != null &&
                    _hiddenVersionLabel.activeSelf)
                    _hiddenVersionLabel.SetActive(false);
                if (_hiddenClientWatermark != null &&
                    _hiddenClientWatermark.activeSelf)
                    _hiddenClientWatermark.SetActive(false);
                return;
            }
            EFT.UI.PreloaderUI preloader =
                EFT.UI.PreloaderUI.Instance;
            if (preloader == null)
                return;
            Component versionLabel =
                AlphaVersionLabelField != null
                    ? AlphaVersionLabelField.GetValue(preloader)
                        as Component
                    : null;
            _hiddenVersionLabel =
                versionLabel != null ? versionLabel.gameObject : null;
            _hiddenClientWatermark =
                preloader.ClientWatermark != null
                    ? preloader.ClientWatermark.gameObject
                    : null;
            _versionLabelWasActive =
                _hiddenVersionLabel != null &&
                _hiddenVersionLabel.activeSelf;
            _clientWatermarkWasActive =
                _hiddenClientWatermark != null &&
                _hiddenClientWatermark.activeSelf;
            if (_hiddenVersionLabel != null)
                _hiddenVersionLabel.SetActive(false);
            if (_hiddenClientWatermark != null)
                _hiddenClientWatermark.SetActive(false);
            if (_hideRaidHud.Value)
            {
                HideBattleUiCanvases(true);
                HideBattleStancePanels(true);
            }
            _freecamWatermarksHidden = true;
        }

        private void HideBattleStancePanels(bool forceScan = false)
        {
            foreach (GameObject hidden
                         in _hiddenBattleStancePanels.Keys)
                if (hidden != null && hidden.activeSelf)
                    hidden.SetActive(false);
            if (!forceScan && _battleStancePanelsScanned)
                return;
            _battleStancePanelsScanned = true;
            EFT.UI.BattleStancePanel[] stancePanels =
                Resources.FindObjectsOfTypeAll<
                    EFT.UI.BattleStancePanel>();
            for (int i = 0; i < stancePanels.Length; i++)
            {
                EFT.UI.BattleStancePanel panel = stancePanels[i];
                if (panel == null ||
                    !panel.gameObject.scene.IsValid())
                    continue;
                GameObject panelObject = panel.gameObject;
                _hiddenBattleStancePanels[panelObject] =
                    panelObject.activeSelf;
                panelObject.SetActive(false);
            }
        }

        private void HideBattleUiCanvases(bool forceScan = false)
        {
            foreach (Canvas canvas in
                     _hiddenBattleUiCanvases.Keys)
                if (canvas != null && canvas.enabled)
                    canvas.enabled = false;

            if (!forceScan && _battleUiCanvasesScanned)
                return;
            _battleUiCanvasesScanned = true;
            Canvas[] allCanvases =
                Resources.FindObjectsOfTypeAll<Canvas>();
            for (int i = 0; i < allCanvases.Length; i++)
            {
                Canvas canvas = allCanvases[i];
                if (canvas == null ||
                    !canvas.gameObject.scene.IsValid() ||
                    _hiddenBattleUiCanvases.ContainsKey(canvas))
                    continue;
                MonoBehaviour[] parents =
                    canvas.GetComponentsInParent<
                        MonoBehaviour>(true);
                bool belongsToBattleScreen = false;
                for (int j = 0; j < parents.Length; j++)
                {
                    Type type = parents[j].GetType();
                    while (type != null)
                    {
                        if (type.Name.StartsWith(
                                "BattleUIScreen",
                                StringComparison.Ordinal))
                        {
                            belongsToBattleScreen = true;
                            break;
                        }
                        type = type.BaseType;
                    }
                    if (belongsToBattleScreen)
                        break;
                }
                if (!belongsToBattleScreen)
                    continue;
                _hiddenBattleUiCanvases[canvas] =
                    canvas.enabled;
                canvas.enabled = false;
            }
        }

        private void RestoreFreecamWatermarks()
        {
            if (!_freecamWatermarksHidden)
                return;
            if (_hiddenVersionLabel != null)
                _hiddenVersionLabel.SetActive(
                    _versionLabelWasActive);
            if (_hiddenClientWatermark != null)
                _hiddenClientWatermark.SetActive(
                    _clientWatermarkWasActive);
            RestoreBattleHud();
            _hiddenVersionLabel = null;
            _hiddenClientWatermark = null;
            _freecamWatermarksHidden = false;
        }

        private void RestoreBattleHud()
        {
            foreach (KeyValuePair<GameObject, bool> entry
                         in _hiddenBattleStancePanels)
                if (entry.Key != null)
                    entry.Key.SetActive(entry.Value);
            foreach (KeyValuePair<Canvas, bool> entry
                         in _hiddenBattleUiCanvases)
                if (entry.Key != null)
                    entry.Key.enabled = entry.Value;
            _hiddenBattleStancePanels.Clear();
            _hiddenBattleUiCanvases.Clear();
            _battleUiCanvasesScanned = false;
            _battleStancePanelsScanned = false;
        }

        private void StartPathRecording()
        {
            if (_recordingPath || _pathPoints.Count < 2)
                return;

            int fps;
            if (_recordCustomFps)
            {
                if (!int.TryParse(_recordFpsText, out fps))
                    fps = 120;
                fps = Mathf.Clamp(fps, 1, 240);
                _recordFpsText = fps.ToString();
            }
            else
            {
                fps = Application.targetFrameRate > 0
                    ? Application.targetFrameRate
                    : Mathf.RoundToInt(
                        (float)Screen.currentResolution
                            .refreshRateRatio.value);
                if (fps <= 0)
                    fps = 60;
            }
            int fpsLimit;
            if (!int.TryParse(_recordFpsLimitText, out fpsLimit))
                fpsLimit = fps;
            fpsLimit = Mathf.Clamp(fpsLimit, 15, 240);
            _recordFpsLimitText = fpsLimit.ToString();

            string pluginDirectory = Path.Combine(
                BepInEx.Paths.PluginPath, "Hysocs-CineKit");
            string bundledFfmpeg = Path.Combine(
                pluginDirectory, "ffmpeg.exe");
            string ffmpeg = File.Exists(bundledFfmpeg)
                ? bundledFfmpeg
                : "ffmpeg.exe";
            string recordingsDirectory = Path.Combine(
                pluginDirectory, "Recordings");
            Directory.CreateDirectory(recordingsDirectory);
            string outputPath = Path.Combine(
                recordingsDirectory,
                "CineKit_" +
                DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") +
                ".mp4");
            string videoPath = outputPath + ".video.mp4";
            string audioPath = outputPath + ".audio.wav";
            int width = Mathf.Max(2, Screen.width & ~1);
            int height = Mathf.Max(2, Screen.height & ~1);

            try
            {
                ProcessStartInfo startInfo =
                    new ProcessStartInfo
                    {
                        FileName = ffmpeg,
                        Arguments =
                            "-y -f rawvideo -pixel_format rgba " +
                            "-video_size " + width + "x" + height +
                            " -framerate " + fps +
                            " -i pipe:0 -vf vflip -an " +
                            "-c:v libx264 -preset medium -crf 18 " +
                            "-pix_fmt yuv420p \"" + videoPath + "\"",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                _ffmpegProcess = Process.Start(startInfo);
                _ffmpegProcess.ErrorDataReceived +=
                    (_, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                            _recordingStatus = args.Data;
                    };
                _ffmpegProcess.BeginErrorReadLine();
            }
            catch (Exception exception)
            {
                _ffmpegProcess = null;
                _recordingStatus =
                    "FFmpeg could not start. Place ffmpeg.exe in " +
                    pluginDirectory + ". " + exception.Message;
                Logger.LogError(_recordingStatus);
                return;
            }

            _recordingFps = fps;
            _recordedFrames = 0;
            _savedCaptureFramerate = Time.captureFramerate;
            _savedTargetFrameRate = Application.targetFrameRate;
            _savedVSyncCount = QualitySettings.vSyncCount;
            Time.captureFramerate = fps;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fpsLimit;
            _recordFrameTexture = new Texture2D(
                width, height, TextureFormat.RGBA32, false);
            _lastRecordingPath = outputPath;
            _ffmpegPath = ffmpeg;
            _recordingVideoPath = videoPath;
            _recordingAudioPath = audioPath;
            StartAudioCapture();
            _recordingStatus =
                "Recording " + width + "x" + height +
                " at " + fps + " FPS...";
            _recordingPath = true;
            _pathLoop.Value = false;
            StartPathPlayback(true);
            if (_menuOpen)
                SetMenuOpen(false);
            StartCoroutine(CapturePathFrames());
        }

        private void StartAudioCapture()
        {
            try
            {
                _recordingAudioRate = AudioSettings.outputSampleRate;
                _recordingAudioChannels =
                    GetAudioChannelCount(AudioSettings.speakerMode);
                _recordingAudioSamples = 0;
                _recordingAudioStream = new FileStream(
                    _recordingAudioPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read);
                _recordingAudioWriter =
                    new BinaryWriter(_recordingAudioStream);
                WriteWaveHeader(
                    _recordingAudioWriter,
                    _recordingAudioRate,
                    _recordingAudioChannels,
                    0);
                AudioRenderer.Start();
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Audio capture could not start: " +
                    exception.Message);
                CloseAudioCapture(false);
            }
        }

        private void CaptureAudioFrame()
        {
            if (_recordingAudioWriter == null)
                return;
            int sampleCount =
                AudioRenderer.GetSampleCountForCaptureFrame();
            if (sampleCount <= 0)
                return;
            NativeArray<float> samples =
                new NativeArray<float>(
                    sampleCount, Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);
            try
            {
                if (!AudioRenderer.Render(samples))
                    return;
                for (int i = 0; i < samples.Length; i++)
                    _recordingAudioWriter.Write(samples[i]);
                _recordingAudioSamples += samples.Length;
            }
            finally
            {
                samples.Dispose();
            }
        }

        private void CloseAudioCapture(bool finalize)
        {
            try
            {
                AudioRenderer.Stop();
            }
            catch
            {
            }
            if (_recordingAudioWriter == null)
                return;
            try
            {
                if (finalize)
                {
                    _recordingAudioWriter.Flush();
                    _recordingAudioStream.Position = 0;
                    WriteWaveHeader(
                        _recordingAudioWriter,
                        _recordingAudioRate,
                        _recordingAudioChannels,
                        _recordingAudioSamples * sizeof(float));
                }
            }
            finally
            {
                _recordingAudioWriter.Dispose();
                _recordingAudioWriter = null;
                _recordingAudioStream = null;
            }
        }

        private static int GetAudioChannelCount(AudioSpeakerMode mode) =>
            mode == AudioSpeakerMode.Mono ? 1 :
            mode == AudioSpeakerMode.Quad ? 4 :
            mode == AudioSpeakerMode.Surround ? 5 :
            mode == AudioSpeakerMode.Mode5point1 ? 6 :
            mode == AudioSpeakerMode.Mode7point1 ? 8 : 2;

        private static void WriteWaveHeader(
            BinaryWriter writer, int sampleRate,
            int channels, int dataBytes)
        {
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)3);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * sizeof(float));
            writer.Write((short)(channels * sizeof(float)));
            writer.Write((short)32);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);
        }

        private IEnumerator CapturePathFrames()
        {
            WaitForEndOfFrame waitForEndOfFrame =
                new WaitForEndOfFrame();
            while (_recordingPath)
            {
                yield return waitForEndOfFrame;
                if (_recordFrameTexture == null ||
                    _ffmpegProcess == null ||
                    _ffmpegProcess.HasExited)
                {
                    FinishPathRecording(false);
                    yield break;
                }

                _recordFrameTexture.ReadPixels(
                    new Rect(
                        0f, 0f,
                        _recordFrameTexture.width,
                        _recordFrameTexture.height),
                    0, 0, false);
                _recordFrameTexture.Apply(false, false);
                byte[] frameData =
                    _recordFrameTexture.GetRawTextureData();
                try
                {
                    _ffmpegProcess.StandardInput.BaseStream.Write(
                        frameData, 0, frameData.Length);
                    CaptureAudioFrame();
                    _recordedFrames++;
                }
                catch (Exception exception)
                {
                    _recordingStatus =
                        "Recording failed: " + exception.Message;
                    FinishPathRecording(false);
                    yield break;
                }

                if (!_pathPlaying)
                {
                    FinishPathRecording(true);
                    yield break;
                }
            }
        }

        private void FinishPathRecording(bool completed)
        {
            if (!_recordingPath && _ffmpegProcess == null)
                return;
            _recordingPath = false;
            Time.captureFramerate = _savedCaptureFramerate;
            Application.targetFrameRate = _savedTargetFrameRate;
            QualitySettings.vSyncCount = _savedVSyncCount;
            CloseAudioCapture(true);
            try
            {
                if (_ffmpegProcess != null &&
                    !_ffmpegProcess.HasExited)
                {
                    _ffmpegProcess.StandardInput.Close();
                    _ffmpegProcess.WaitForExit(15000);
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not finalize recording: " +
                    exception.Message);
                completed = false;
            }
            finally
            {
                if (_ffmpegProcess != null)
                    _ffmpegProcess.Dispose();
                _ffmpegProcess = null;
                if (_recordFrameTexture != null)
                    Destroy(_recordFrameTexture);
                _recordFrameTexture = null;
            }
            if (completed)
                completed = MuxRecordingAudio();
            _recordingStatus = completed
                ? "Saved " + _recordedFrames +
                  " frames to " + _lastRecordingPath
                : "Recording stopped before completion.";
        }

        private bool MuxRecordingAudio()
        {
            if (string.IsNullOrEmpty(_recordingVideoPath) ||
                !File.Exists(_recordingVideoPath))
                return false;
            try
            {
                if (string.IsNullOrEmpty(_recordingAudioPath) ||
                    !File.Exists(_recordingAudioPath) ||
                    _recordingAudioSamples <= 0)
                {
                    File.Move(
                        _recordingVideoPath,
                        _lastRecordingPath);
                    return true;
                }

                ProcessStartInfo muxInfo =
                    new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments =
                            "-y -i \"" + _recordingVideoPath +
                            "\" -i \"" + _recordingAudioPath +
                            "\" -c:v copy -c:a aac -b:a 192k " +
                            "-shortest \"" + _lastRecordingPath + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                using (Process mux = Process.Start(muxInfo))
                {
                    mux.WaitForExit(30000);
                    if (!mux.HasExited || mux.ExitCode != 0)
                        return false;
                }
                File.Delete(_recordingVideoPath);
                File.Delete(_recordingAudioPath);
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Could not mux recording audio: " +
                    exception.Message);
                return false;
            }
        }

        private void UpdatePathPlayback()
        {
            if (_pathPoints.Count < 2 ||
                _pathSegment < 0 ||
                _pathSegment >= _pathPoints.Count)
            {
                StopPathPlayback();
                return;
            }

            CameraPathPoint from = _pathPoints[_pathSegment];
            float delta = _recordingPath
                ? 1f / Mathf.Max(1, _recordingFps)
                : Time.unscaledDeltaTime;
            if (_pathStopDelayRemaining > 0f)
            {
                _pathStopDelayRemaining -= delta;
                if (from.Type == CameraPathPointType.Entity)
                    SetFreePose(from);
                return;
            }
            if (_pathStartDelayRemaining > 0f)
            {
                _pathStartDelayRemaining -= delta;
                if (from.Type == CameraPathPointType.Entity)
                    SetFreePose(from);
                return;
            }
            if (_pathStayRemaining > 0f)
            {
                _pathStayRemaining -= delta;
                SetFreePose(from);
                return;
            }
            int nextIndex = from.NextPoint;
            if (nextIndex < 0 ||
                nextIndex >= _pathPoints.Count ||
                nextIndex == _pathSegment)
            {
                if (!_pathLoop.Value)
                {
                    StopPathPlayback();
                    return;
                }
                nextIndex = 0;
            }
            CameraPathPoint to = _pathPoints[nextIndex];
            float segmentLength =
                EstimatePlaybackSegmentLength(
                    _pathSegment, nextIndex);
            bool finalSegment =
                !_pathLoop.Value &&
                (to.NextPoint < 0 ||
                 to.NextPoint >= _pathPoints.Count ||
                 to.NextPoint == nextIndex);
            bool mustStopAtPoint =
                finalSegment ||
                to.StopHere ||
                to.StartDelay > 0f ||
                to.StopDelay > 0f ||
                to.StayDuration > 0f;
            float timedDuration = GetTimedSegmentDuration(
                from, to, segmentLength);
            if (timedDuration > 0f)
            {
                float rawProgress =
                    _pathSegmentProgress +
                    (delta + _pathTimeCarry) / timedDuration;
                _pathTimeCarry = rawProgress > 1f
                    ? (rawProgress - 1f) * timedDuration
                    : 0f;
                _pathSegmentProgress =
                    Mathf.Clamp01(rawProgress);
            }
            else
            {
                float remaining =
                    segmentLength * (1f - _pathSegmentProgress);
                float maxSpeed = Mathf.Max(0.1f, from.Speed);
                float acceleration =
                    Mathf.Max(0.1f, from.Acceleration);
                float deceleration =
                    Mathf.Max(0.1f, from.Deceleration);
                float desiredSpeed = maxSpeed;
                if (mustStopAtPoint)
                {
                    float brakingSpeed = Mathf.Sqrt(
                        2f * deceleration * remaining);
                    desiredSpeed = Mathf.Min(
                        maxSpeed, brakingSpeed);
                }
                _pathCurrentSpeed = Mathf.MoveTowards(
                    _pathCurrentSpeed, desiredSpeed,
                    acceleration * delta);
                float distance =
                    Mathf.Max(0.05f, _pathCurrentSpeed) *
                    delta + _pathDistanceCarry;
                float rawProgress =
                    _pathSegmentProgress +
                    distance / Mathf.Max(0.001f, segmentLength);
                _pathDistanceCarry = rawProgress > 1f
                    ? (rawProgress - 1f) * segmentLength
                    : 0f;
                _pathSegmentProgress =
                    Mathf.Clamp01(rawProgress);
            }
            _freePosition = EvaluatePlaybackSegment(
                _pathSegment, nextIndex,
                _pathSegmentProgress);

            Quaternion rotation = Quaternion.Slerp(
                GetPointRotation(from),
                GetPointRotation(to),
                _pathSegmentProgress);
            ApplyRotation(rotation);

            if (_pathSegmentProgress < 1f)
                return;

            SetFreePose(to);
            if (to.StopHere)
            {
                StopPathPlayback();
                return;
            }
            _pathSegment = nextIndex;
            if (mustStopAtPoint)
                _pathCurrentSpeed = 0f;
            _pathSegmentProgress = 0f;
            if (timedDuration > 0f)
                _pathDistanceCarry = 0f;
            else
                _pathTimeCarry = 0f;
            _pathStopDelayRemaining = to.StopDelay;
            _pathStayRemaining = to.StayDuration;
            _pathStartDelayRemaining =
                finalSegment
                    ? 0f
                    : to.StartDelay;
            return;
        }

        private void SetFreePose(CameraPathPoint point)
        {
            _freePosition = GetPointPosition(point);
            ApplyRotation(GetPointRotation(point));
            ApplyFreeCameraPose();
        }

        private Quaternion GetPointRotation(
            CameraPathPoint point)
        {
            Vector3 direction =
                GetPointLookTarget(point) -
                GetPointPosition(point);
            return direction.sqrMagnitude <= 0.000001f
                ? Quaternion.identity
                : Quaternion.LookRotation(direction, Vector3.up);
        }

        private Vector3 EvaluateBezier(
            CameraPathPoint from,
            CameraPathPoint to,
            float t)
        {
            Vector3 p0 = GetPointPosition(from);
            Vector3 p1 = p0 + from.OutTangent;
            Vector3 p3 = GetPointPosition(to);
            Vector3 p2 = p3 + to.InTangent;
            return EvaluateCubic(p0, p1, p2, p3, t);
        }

        private static Vector3 EvaluateCubic(
            Vector3 p0, Vector3 p1,
            Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 +
                   3f * u * u * t * p1 +
                   3f * u * t * t * p2 +
                   t * t * t * p3;
        }

        private Vector3 EvaluatePlaybackSegment(
            int fromIndex, int toIndex, float t)
        {
            CameraPathPoint from = _pathPoints[fromIndex];
            CameraPathPoint to = _pathPoints[toIndex];
            if (_selectedPathGroup < 0 ||
                _selectedPathGroup >= _pathGroups.Count ||
                !_pathGroups[_selectedPathGroup].SmoothTransitions)
                return EvaluateBezier(from, to, t);

            Vector3 fromPosition = GetPointPosition(from);
            Vector3 toPosition = GetPointPosition(to);
            Vector3 previous = fromPosition;
            for (int i = 0; i < _pathPoints.Count; i++)
                if (_pathPoints[i].NextPoint == fromIndex)
                {
                    previous = GetPointPosition(_pathPoints[i]);
                    break;
                }
            if (previous == fromPosition && fromIndex > 0)
                previous = GetPointPosition(
                    _pathPoints[fromIndex - 1]);

            Vector3 following = toPosition;
            int afterIndex = to.NextPoint;
            if (afterIndex >= 0 &&
                afterIndex < _pathPoints.Count &&
                afterIndex != toIndex)
                following = GetPointPosition(
                    _pathPoints[afterIndex]);
            else if (toIndex + 1 < _pathPoints.Count)
                following = GetPointPosition(
                    _pathPoints[toIndex + 1]);

            Vector3 p0 = fromPosition;
            Vector3 p1 =
                p0 + (toPosition - previous) / 6f;
            Vector3 p3 = toPosition;
            Vector3 p2 =
                p3 + (fromPosition - following) / 6f;
            return EvaluateCubic(p0, p1, p2, p3, t);
        }

        private float EstimatePlaybackSegmentLength(
            int fromIndex, int toIndex)
        {
            const int samples = 16;
            float length = 0f;
            Vector3 previous =
                GetPointPosition(_pathPoints[fromIndex]);
            for (int i = 1; i <= samples; i++)
            {
                Vector3 current = EvaluatePlaybackSegment(
                    fromIndex, toIndex,
                    i / (float)samples);
                length += Vector3.Distance(
                    previous, current);
                previous = current;
            }
            return Mathf.Max(0.001f, length);
        }

        private float EstimateBezierLength(
            CameraPathPoint from,
            CameraPathPoint to)
        {
            const int samples = 16;
            float length = 0f;
            Vector3 previous = GetPointPosition(from);
            for (int i = 1; i <= samples; i++)
            {
                Vector3 current = EvaluateBezier(
                    from, to, i / (float)samples);
                length += Vector3.Distance(previous, current);
                previous = current;
            }
            return Mathf.Max(0.001f, length);
        }

        private float CalculateTotalDurationScale()
        {
            if (_selectedPathGroup < 0 ||
                _selectedPathGroup >= _pathGroups.Count)
                return 1f;
            CameraPathGroup group =
                _pathGroups[_selectedPathGroup];
            if (group.TotalDuration <= 0f ||
                group.Points.Count < 2)
                return 1f;

            float naturalTotal = 0f;
            HashSet<int> visited = new HashSet<int>();
            int index = 0;
            while (index >= 0 &&
                   index < group.Points.Count &&
                   visited.Add(index))
            {
                CameraPathPoint from = group.Points[index];
                int next = from.NextPoint;
                if (next < 0 ||
                    next >= group.Points.Count ||
                    next == index)
                {
                    if (!_pathLoop.Value)
                        break;
                    next = 0;
                }
                CameraPathPoint to = group.Points[next];
                naturalTotal += GetNaturalSegmentDuration(
                    from, to,
                    EstimatePlaybackSegmentLength(
                        index, next));
                index = next;
            }
            return group.TotalDuration /
                   Mathf.Max(0.001f, naturalTotal);
        }

        private float GetTimedSegmentDuration(
            CameraPathPoint from,
            CameraPathPoint to,
            float length)
        {
            CameraPathGroup group =
                _selectedPathGroup >= 0 &&
                _selectedPathGroup < _pathGroups.Count
                    ? _pathGroups[_selectedPathGroup]
                    : null;
            if (group != null && group.TotalDuration > 0f)
                return GetNaturalSegmentDuration(
                           from, to, length) *
                       _pathTotalDurationScale;
            return from.SegmentDuration > 0f
                ? from.SegmentDuration
                : 0f;
        }

        private static float GetNaturalSegmentDuration(
            CameraPathPoint from,
            CameraPathPoint to,
            float length)
        {
            if (from.SegmentDuration > 0f)
                return from.SegmentDuration;
            float movementTime =
                length / Mathf.Max(0.1f, from.Speed);
            return Mathf.Max(0.001f, movementTime);
        }

        private void ApplyRotation(Quaternion rotation)
        {
            Vector3 angles = rotation.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x > 180f
                ? angles.x - 360f
                : angles.x;
        }

        private void SetSelectedLookTarget()
        {
            if (_selectedPathPoint < 0 ||
                _selectedPathPoint >= _pathPoints.Count)
                return;
            CameraPathPoint point =
                _pathPoints[_selectedPathPoint];
            SetPointLookTargetWorld(point, _freePosition);
        }

        private void SetPointLookTargetWorld(
            CameraPathPoint point, Vector3 worldTarget)
        {
            Vector3 pointPosition = GetPointPosition(point);
            if (point.Type == CameraPathPointType.Entity &&
                point.FollowEntityLookDirection)
            {
                Player target = FindPathPointEntity(point);
                if (target != null)
                {
                    point.EntityAimOffset =
                        Quaternion.Inverse(
                            GetEntityLookRotation(target)) *
                        (worldTarget - pointPosition);
                    return;
                }
            }
            point.LookTarget = point.Position +
                (worldTarget - pointPosition);
        }

        private void SetPointWorldPosition(
            CameraPathPoint point, Vector3 worldPosition)
        {
            Vector3 current = GetPointPosition(point);
            RotateConnectedTangentsForMovedPoint(
                point, current, worldPosition);
            if (point.Type == CameraPathPointType.Entity)
            {
                Player target = FindPathPointEntity(point);
                if (target == null)
                    return;
                Quaternion rotation =
                    point.FollowEntityLookDirection
                        ? GetEntityLookRotation(target)
                        : GetEntityBodyRotation(target);
                point.EntityOffset =
                    Quaternion.Inverse(rotation) *
                    (worldPosition -
                     GetEntityAttachmentPosition(
                         target, point.EntityAttachment));
                point.ResolvedPosition = worldPosition;
                point.ResolvedPositionInitialized = true;
                UpdateEntityOutgoingConnection(point);
                return;
            }
            Vector3 delta = worldPosition - current;
            point.Position = worldPosition;
            point.LookTarget += delta;
            for (int i = 0; i < _pathPoints.Count; i++)
            {
                CameraPathPoint source = _pathPoints[i];
                if (source.NextPoint ==
                    _pathPoints.IndexOf(point))
                    UpdateEntityOutgoingConnection(source);
            }
        }

        private void RotateConnectedTangentsForMovedPoint(
            CameraPathPoint moved,
            Vector3 oldPosition,
            Vector3 newPosition)
        {
            if ((newPosition - oldPosition).sqrMagnitude <=
                0.0000001f)
                return;
            int movedIndex = _pathPoints.IndexOf(moved);
            if (movedIndex < 0)
                return;

            int outgoingIndex = moved.NextPoint;
            if (outgoingIndex >= 0 &&
                outgoingIndex < _pathPoints.Count &&
                outgoingIndex != movedIndex)
            {
                CameraPathPoint target =
                    _pathPoints[outgoingIndex];
                RotateSegmentTangents(
                    moved,
                    target,
                    GetPointPosition(target) - oldPosition,
                    GetPointPosition(target) - newPosition);
            }

            for (int i = 0; i < _pathPoints.Count; i++)
            {
                CameraPathPoint source = _pathPoints[i];
                if (i == movedIndex ||
                    source.NextPoint != movedIndex)
                    continue;
                Vector3 sourcePosition =
                    GetPointPosition(source);
                RotateSegmentTangents(
                    source,
                    moved,
                    oldPosition - sourcePosition,
                    newPosition - sourcePosition);
            }
        }

        private static void RotateSegmentTangents(
            CameraPathPoint from,
            CameraPathPoint to,
            Vector3 oldDirection,
            Vector3 newDirection)
        {
            if (oldDirection.sqrMagnitude <= 0.000001f ||
                newDirection.sqrMagnitude <= 0.000001f)
                return;
            Quaternion rotation = Quaternion.FromToRotation(
                oldDirection, newDirection);
            from.OutTangent = rotation * from.OutTangent;
            to.InTangent = rotation * to.InTangent;
        }

        private void EnsurePathMaterial()
        {
            if (_pathMaterial != null)
                return;
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return;
            _pathMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _pathMaterial.SetInt(
                "_SrcBlend",
                (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _pathMaterial.SetInt(
                "_DstBlend",
                (int)UnityEngine.Rendering.BlendMode
                    .OneMinusSrcAlpha);
            _pathMaterial.SetInt("_Cull", 0);
            _pathMaterial.SetInt("_ZWrite", 0);
        }

        private void DrawCameraPath(Camera camera)
        {
            if (!_freecamEnabled.Value ||
                _pathPoints.Count == 0 ||
                camera == null ||
                (_pathPlaying &&
                 _hidePathElementsDuringPlayback) ||
                _recordingPath)
                return;
            EnsurePathMaterial();
            if (_pathMaterial == null ||
                !_pathMaterial.SetPass(0))
                return;

            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;
            GL.Begin(GL.LINES);

            Color pathColor = new Color(0.3f, 0.8f, 1f, 0.9f);
            GL.Color(pathColor);
            for (int i = 0; i < _pathPoints.Count; i++)
            {
                int target = _pathPoints[i].NextPoint;
                if (target < 0 || target >= _pathPoints.Count ||
                    target == i)
                {
                    if (!_pathLoop.Value || _pathPoints.Count < 2)
                        continue;
                    target = 0;
                    if (target == i)
                        continue;
                }
                Vector3 previous =
                    GetPointPosition(_pathPoints[i]);
                for (int sample = 1; sample <= 24; sample++)
                {
                    Vector3 current = EvaluatePlaybackSegment(
                        i, target, sample / 24f);
                    GL.Vertex(previous);
                    GL.Vertex(current);
                    previous = current;
                }
            }
            const int segments = 32;
            for (int i = 0; i < _pathPoints.Count; i++)
            {
                CameraPathPoint point = _pathPoints[i];
                Vector3 pointPosition = GetPointPosition(point);
                Vector3 pointLookTarget = GetPointLookTarget(point);
                if (point.Type == CameraPathPointType.Entity)
                {
                    Player entity = FindPathPointEntity(point);
                    if (entity != null)
                    {
                        Vector3 anchor =
                            GetEntityAttachmentPosition(
                                entity, point.EntityAttachment);
                        GL.Color(new Color(
                            1f, 0.55f, 0.15f, 0.9f));
                        const int dashCount = 16;
                        for (int dash = 0;
                             dash < dashCount;
                             dash += 2)
                        {
                            GL.Vertex(Vector3.Lerp(
                                anchor,
                                pointPosition,
                                dash / (float)dashCount));
                            GL.Vertex(Vector3.Lerp(
                                anchor,
                                pointPosition,
                                (dash + 1f) / dashCount));
                        }
                        Vector3 markerRight =
                            camera.transform.right * 0.18f;
                        Vector3 markerUp =
                            camera.transform.up * 0.18f;
                        GL.Vertex(
                            anchor - markerRight - markerUp);
                        GL.Vertex(
                            anchor + markerRight + markerUp);
                        GL.Vertex(
                            anchor - markerRight + markerUp);
                        GL.Vertex(
                            anchor + markerRight - markerUp);
                    }
                }
                bool positionHovered =
                    i == _worldHoverPoint &&
                    _worldHoverHandle == 0;
                GL.Color(positionHovered
                    ? Color.white
                    : i == _selectedPathPoint
                        ? new Color(1f, 0.7f, 0.15f, 1f)
                        : pathColor);
                float radius = 0.35f;
                for (int j = 0; j < segments; j++)
                {
                    float a = j * Mathf.PI * 2f / segments;
                    float b = (j + 1) * Mathf.PI * 2f /
                        segments;
                    GL.Vertex(pointPosition + new Vector3(
                        Mathf.Cos(a) * radius, 0f,
                        Mathf.Sin(a) * radius));
                    GL.Vertex(pointPosition + new Vector3(
                        Mathf.Cos(b) * radius, 0f,
                        Mathf.Sin(b) * radius));
                }
                Vector3 positionHandle =
                    GetWorldHandlePosition(i, 0);
                GL.Vertex(pointPosition);
                GL.Vertex(positionHandle);
                DrawWireBall(positionHandle, 0.16f, segments);
                Quaternion pointRotation =
                    GetPointRotation(point);
                Vector3 forward =
                    pointRotation * Vector3.forward;
                Vector3 right =
                    pointRotation * Vector3.right;
                Vector3 nose =
                    pointPosition + forward * 0.45f;
                Vector3 rearLeft =
                    pointPosition - forward * 0.25f -
                    right * 0.28f;
                Vector3 rearRight =
                    pointPosition - forward * 0.25f +
                    right * 0.28f;
                GL.Vertex(nose);
                GL.Vertex(rearLeft);
                GL.Vertex(rearLeft);
                GL.Vertex(rearRight);
                GL.Vertex(rearRight);
                GL.Vertex(nose);
                GL.Vertex(pointPosition);
                GL.Vertex(pointLookTarget);

                GL.Color(
                    i == _worldHoverPoint &&
                    _worldHoverHandle == 3
                        ? Color.white
                        : new Color(1f, 0.3f, 0.75f, 1f));
                DrawWireBall(pointLookTarget, 0.18f, segments);
            }

            for (int i = 0; i < _pathPoints.Count; i++)
            {
                int target = GetConnectionTarget(i);
                if (target < 0)
                    continue;
                GL.Color(
                    i == _worldHoverPoint &&
                    _worldHoverHandle == 4
                        ? Color.white
                        : new Color(1f, 0.75f, 0.1f, 1f));
                Vector3 pathMiddle = EvaluateBezier(
                    _pathPoints[i], _pathPoints[target], 0.5f);
                Vector3 control =
                    GetWorldHandlePosition(i, 4);
                GL.Vertex(pathMiddle);
                GL.Vertex(control);
                DrawWireBall(control, 0.18f, segments);
            }
            GL.End();
            GL.PopMatrix();
        }

        private static void DrawWireBall(
            Vector3 center, float radius, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                GL.Vertex(center + new Vector3(
                    Mathf.Cos(a) * radius,
                    Mathf.Sin(a) * radius, 0f));
                GL.Vertex(center + new Vector3(
                    Mathf.Cos(b) * radius,
                    Mathf.Sin(b) * radius, 0f));
                GL.Vertex(center + new Vector3(
                    Mathf.Cos(a) * radius, 0f,
                    Mathf.Sin(a) * radius));
                GL.Vertex(center + new Vector3(
                    Mathf.Cos(b) * radius, 0f,
                    Mathf.Sin(b) * radius));
                GL.Vertex(center + new Vector3(
                    0f, Mathf.Cos(a) * radius,
                    Mathf.Sin(a) * radius));
                GL.Vertex(center + new Vector3(
                    0f, Mathf.Cos(b) * radius,
                    Mathf.Sin(b) * radius));
            }
        }

        private void UpdateEnvironmentCullingPosition()
        {
            if (_lastEnvironmentCullingFrame == Time.frameCount)
                return;
            Vector3 cameraPosition = CurrentCameraPosition;
            bool positionChanged =
                !_hasEnvironmentCullingPosition ||
                (cameraPosition - _lastEnvironmentCullingPosition)
                    .sqrMagnitude >= 0.00000001f;
            if (!positionChanged)
                return;
            _lastEnvironmentCullingFrame = Time.frameCount;
            _lastEnvironmentCullingPosition = cameraPosition;
            _hasEnvironmentCullingPosition = true;

            if (Koenigz.PerfectCulling.EFT
                    .PerfectCullingCrossSceneSampler.Exists)
            {
                Koenigz.PerfectCulling.EFT
                    .PerfectCullingCrossSceneSampler sampler =
                    Koenigz.PerfectCulling.EFT
                        .PerfectCullingCrossSceneSampler.Instance;
                if (sampler != null && sampler.CullingCamera != null)
                    sampler.CullingCamera.ObservePosition = cameraPosition;
            }
        }

        private void UpdateIndoorCullingPosition()
        {
            float now = Time.unscaledTime;
            if (now < _nextIndoorCullingEvaluation)
                return;
            _nextIndoorCullingEvaluation = now + 0.02f;

            UpdateFreecamEnvironment();

            if (!_indoorCullersScanned)
            {
                _indoorCullersScanned = true;
                _indoorCullers.Clear();
                _mapTerrains.Clear();
                DisablerCullingObjectBase[] found =
                    Resources.FindObjectsOfTypeAll<DisablerCullingObjectBase>();
                foreach (DisablerCullingObjectBase culler in found)
                {
                    if (culler != null &&
                        culler.gameObject.activeInHierarchy &&
                        culler.isActiveAndEnabled)
                    {
                        _indoorCullers.Add(culler);
                        DisablerTerrainCullingObject terrainCuller =
                            culler as DisablerTerrainCullingObject;
                        if (terrainCuller != null &&
                            terrainCuller.Terrain != null &&
                            !_mapTerrains.Contains(terrainCuller.Terrain))
                            _mapTerrains.Add(terrainCuller.Terrain);
                    }
                }
            }

            Vector3 cameraPosition = CurrentCameraPosition;
            foreach (DisablerCullingObjectBase culler in _indoorCullers)
            {
                if (culler == null)
                    continue;
                bool cameraInside = IsInsideCullingVolume(
                    culler, CullingCollidersField, cameraPosition);
                bool cameraExcluded = IsInsideCullingVolume(
                    culler, CullingInverseCollidersField, cameraPosition);
                int id = culler.GetInstanceID();
                bool cameraOverride = cameraInside && !cameraExcluded;
                bool wasOverridden = _indoorCullingStates.Contains(id);
                if (cameraOverride && !wasOverridden)
                {
                    _indoorCullingStates.Add(id);
                    culler.SetComponentsEnabled(true);
                }
                else if (!cameraOverride && wasOverridden)
                {
                    _indoorCullingStates.Remove(id);
                    culler.SetComponentsEnabled(culler.HasEntered);
                }
            }
        }

        private bool GetFreecamCullingTriggerState(
            DisablerCullingObjectBase culler)
        {
            Vector3 position = CurrentCameraPosition;
            return IsInsideCullingVolume(
                       culler, CullingCollidersField, position) &&
                   !IsInsideCullingVolume(
                       culler, CullingInverseCollidersField, position);
        }

        private void RegisterIndoorCuller(DisablerCullingObjectBase culler)
        {
            if (culler != null && !_indoorCullers.Contains(culler))
                _indoorCullers.Add(culler);
        }

        private void UpdateFreecamEnvironment()
        {
            EFT.EnvironmentEffect.EnvironmentManager manager =
                EFT.EnvironmentEffect.EnvironmentManager.Instance;
            GameWorld world = Singleton<GameWorld>.Instance;
            Player player = world != null ? world.MainPlayer : null;
            if (manager == null || player == null)
                return;

            EFT.EnvironmentEffect.IndoorTrigger trigger =
                manager.TryFindTriggerByPos(CurrentCameraPosition);
            manager.SetTriggerForPlayer(player, trigger);
        }

        private bool IsInsideCullingVolume(
            DisablerCullingObjectBase culler,
            FieldInfo field,
            Vector3 cameraPosition)
        {
            if (field == null)
                return false;
            List<Collider> colliders =
                field.GetValue(culler) as List<Collider>;
            if (colliders == null)
                return false;
            foreach (Collider collider in colliders)
            {
                if (collider == null || !collider.enabled)
                    continue;
                Bounds bounds = collider.bounds;
                if (culler is DisablerTerrainCullingObject &&
                    collider is TerrainCollider &&
                    cameraPosition.x >= bounds.min.x &&
                    cameraPosition.x <= bounds.max.x &&
                    cameraPosition.z >= bounds.min.z &&
                    cameraPosition.z <= bounds.max.z)
                    return true;
                Vector3 samplePosition = cameraPosition;
                if (samplePosition.y > bounds.max.y)
                {
                    if (IsTerrainBetween(
                            samplePosition, bounds))
                        continue;
                    samplePosition.y = Mathf.Max(
                        bounds.min.y,
                        bounds.max.y - 0.01f);
                }
                if ((collider.ClosestPoint(samplePosition) - samplePosition)
                        .sqrMagnitude <= 0.000001f)
                    return true;
            }
            return false;
        }

        private bool IsTerrainBetween(
            Vector3 cameraPosition, Bounds volumeBounds)
        {
            for (int i = 0; i < _mapTerrains.Count; i++)
            {
                Terrain terrain = _mapTerrains[i];
                if (terrain == null || terrain.terrainData == null)
                    continue;
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (cameraPosition.x < origin.x ||
                    cameraPosition.x > origin.x + size.x ||
                    cameraPosition.z < origin.z ||
                    cameraPosition.z > origin.z + size.z)
                    continue;
                float surfaceHeight =
                    terrain.SampleHeight(cameraPosition) + origin.y;
                if (surfaceHeight > volumeBounds.center.y + 0.5f &&
                    surfaceHeight < cameraPosition.y)
                    return true;
            }
            return false;
        }

        private void RestoreIndoorCulling()
        {
            foreach (DisablerCullingObjectBase culler in _indoorCullers)
                if (culler != null &&
                    _indoorCullingStates.Contains(culler.GetInstanceID()))
                    culler.SetComponentsEnabled(culler.HasEntered);
            _indoorCullers.Clear();
            _mapTerrains.Clear();
            _indoorCullingStates.Clear();
            _hasEnvironmentCullingPosition = false;
            _lastEnvironmentCullingFrame = -1;
            _nextIndoorCullingEvaluation = 0f;
            _indoorCullersScanned = false;
        }

        private void UpdateCrossSceneMeshVisibility()
        {
            if (!_extendedCrossSceneMeshes.Value)
            {
                RestoreCrossSceneMeshVisibility();
                return;
            }
            if (_crossSceneMeshScanNeeded)
            {
                _crossSceneMeshScanNeeded = false;
                Koenigz.PerfectCulling.EFT
                    .PerfectCullingCrossSceneContentMeshes[] contents =
                    Resources.FindObjectsOfTypeAll<
                        Koenigz.PerfectCulling.EFT
                            .PerfectCullingCrossSceneContentMeshes>();
                for (int i = 0; i < contents.Length; i++)
                {
                    Koenigz.PerfectCulling.EFT
                        .PerfectCullingCrossSceneContentMeshes content =
                        contents[i];
                    if (content != null &&
                        content.RuntimeCullingGroup != null)
                        _crossSceneMeshGroups.Add(
                            content.RuntimeCullingGroup);
                }
            }

            Vector3 cameraPosition = CurrentCameraPosition;
            float rangeSqr =
                _crossSceneMeshDistance.Value *
                _crossSceneMeshDistance.Value;
            foreach (Koenigz.PerfectCulling.EFT
                         .PerfectCullingCrossSceneGroup group
                     in _crossSceneMeshGroups)
            {
                if (group == null)
                    continue;
                Bounds bounds = group.GroupBoundingBox;
                Vector3 sample = cameraPosition;
                sample.y = Mathf.Clamp(
                    sample.y, bounds.min.y, bounds.max.y);
                if (bounds.SqrDistance(sample) <= rangeSqr)
                {
                    if (!_crossSceneGroupCullingStates
                            .ContainsKey(group))
                        ForceCrossSceneGroupVisible(group);
                }
                else
                    RestoreCrossSceneGroup(group);
            }
        }

        private void AddCrossSceneMeshContent(
            Koenigz.PerfectCulling.EFT
                .PerfectCullingCrossSceneContentMeshes content)
        {
            if (content != null &&
                content.RuntimeCullingGroup != null)
                _crossSceneMeshGroups.Add(
                    content.RuntimeCullingGroup);
        }

        private void ForceCrossSceneGroupVisible(
            Koenigz.PerfectCulling.EFT
                .PerfectCullingCrossSceneGroup group)
        {
            if (!_crossSceneGroupCullingStates.ContainsKey(group))
                _crossSceneGroupCullingStates.Add(
                    group, group.allowGroupCulling);
            group.allowGroupCulling = false;
            Koenigz.PerfectCulling.PerfectCullingBakeGroup[] bakeGroups =
                group.bakeGroups;
            if (bakeGroups == null)
                return;
            for (int i = 0; i < bakeGroups.Length; i++)
            {
                Koenigz.PerfectCulling.PerfectCullingBakeGroup bakeGroup =
                    bakeGroups[i];
                if (bakeGroup == null)
                    continue;
                bakeGroup.RuntimeSetForceRenderingOff(false);
                bakeGroup.Toggle(true);
            }
        }

        private static bool IsCrossSceneGroupVisible(
            Koenigz.PerfectCulling.EFT
                .PerfectCullingCrossSceneGroup group)
        {
            if (group.allowGroupCulling)
                return false;
            Koenigz.PerfectCulling.PerfectCullingBakeGroup[] bakeGroups =
                group.bakeGroups;
            if (bakeGroups == null)
                return true;
            for (int i = 0; i < bakeGroups.Length; i++)
                if (bakeGroups[i] != null &&
                    !bakeGroups[i].IsEnabled)
                    return false;
            return true;
        }

        private void RestoreCrossSceneGroup(
            Koenigz.PerfectCulling.EFT
                .PerfectCullingCrossSceneGroup group)
        {
            if (group == null ||
                !_crossSceneGroupCullingStates.TryGetValue(
                    group, out bool allowCulling))
                return;
            group.allowGroupCulling = allowCulling;
            _crossSceneGroupCullingStates.Remove(group);
        }

        private void RestoreCrossSceneMeshVisibility()
        {
            foreach (KeyValuePair<
                         Koenigz.PerfectCulling.EFT
                             .PerfectCullingCrossSceneGroup, bool> entry
                     in _crossSceneGroupCullingStates)
                if (entry.Key != null)
                    entry.Key.allowGroupCulling = entry.Value;
            _crossSceneGroupCullingStates.Clear();
            _crossSceneMeshGroups.Clear();
            _crossSceneMeshScanNeeded = true;
        }

        private void DisableFreecamOutsideRaid()
        {
            if (!_freecamEnabled.Value)
                return;
            if (!IsRaidRunning())
                _freecamEnabled.Value = false;
        }

        private static bool IsRaidRunning()
        {
            AbstractGame game =
                Singleton<AbstractGame>.Instance;
            return game != null &&
                   game.InRaid &&
                   game.GameTimer != null &&
                   game.GameTimer.Status ==
                   EFT.GameTimer.EGameTimerStatus.Started;
        }

        private void OnFreecamSettingChanged(object sender, EventArgs args)
        {
            if (_freecamEnabled.Value)
            {
                EnsureFreeCamera();
            }
            else
            {
                RestoreCamera();
            }
        }

        private void OnPlayerFollowsFreecamChanged(
            object sender, EventArgs args)
        {
            if (_playerFollowsFreecam.Value &&
                _controlPlayerFromFreecam.Value)
                _controlPlayerFromFreecam.Value = false;
            if (!_cameraDetached)
                return;
            if (_playerFollowsFreecam.Value)
            {
                RestoreLocalPlayerPointOfView();
                SyncPlayerToFreeCamera();
                HideLocalPlayerBody();
            }
            else
            {
                ShowLocalPlayerThirdPerson();
                RestoreLocalBodyRenderers();
            }
        }

        private void OnControlPlayerFromFreecamChanged(
            object sender, EventArgs args)
        {
            if (_controlPlayerFromFreecam.Value &&
                _playerFollowsFreecam.Value)
                _playerFollowsFreecam.Value = false;
            if (_cameraDetached)
                ApplyFreeCameraPose();
        }

        private void OnFreecamAntialiasingChanged(
            object sender, EventArgs args)
        {
            if (_cameraDetached)
                ApplyFreecamGraphicsProfile();
        }

        private void OnHideRaidHudChanged(
            object sender, EventArgs args)
        {
            if (!IsFreecamActive)
                return;
            if (_hideRaidHud.Value)
            {
                HideBattleUiCanvases(true);
                HideBattleStancePanels();
            }
            else
            {
                RestoreBattleHud();
            }
        }

        private void OnGrassRenderDistanceChanged(
            object sender, EventArgs args)
        {
            if (!_cameraDetached)
                return;
            ApplyGrassRenderDistance();
            ScheduleGrassSpatialRefresh();
        }

        private void ScheduleGrassSpatialRefresh()
        {
            _grassRefreshPending = true;
            _grassRefreshTime = Time.unscaledTime + 1f;
        }

        private void UpdateGrassDistanceRefresh()
        {
            if (!_grassRefreshPending ||
                Time.unscaledTime < _grassRefreshTime)
                return;
            _grassRefreshPending = false;
            RefreshGrassSpatialPartitioning();
        }

        private void ApplyGrassRenderDistance()
        {
            GPUInstancerDetailManager[] managers =
                Resources.FindObjectsOfTypeAll<GPUInstancerDetailManager>();
            for (int i = 0; i < managers.Length; i++)
                ApplyGrassRenderDistance(managers[i]);
        }

        private void ApplyGrassRenderDistance(
            GPUInstancerDetailManager manager)
        {
            if (!_cameraDetached || manager == null)
                return;
            GPUInstancerTerrainSettings settings = manager.terrainSettings;
            if (settings == null)
                return;
            if (!_grassDistanceStates.ContainsKey(settings))
                _grassDistanceStates.Add(settings, new GrassDistanceState
                {
                    Maximum = settings.maxDetailDistance,
                    LegacyMaximum = settings.maxDetailDistanceLegacy
                });
            float distance = _grassRenderDistance.Value;
            settings.maxDetailDistance = distance;
            settings.maxDetailDistanceLegacy = distance;
            List<GPUInstancerPrototype> prototypes = manager.prototypeList;
            if (prototypes == null)
                return;
            for (int i = 0; i < prototypes.Count; i++)
            {
                GPUInstancerPrototype prototype = prototypes[i];
                if (prototype == null)
                    continue;
                if (!_grassPrototypeDistanceStates.ContainsKey(prototype))
                    _grassPrototypeDistanceStates.Add(
                        prototype, new GrassPrototypeDistanceState
                        {
                            Maximum = prototype.maxDistance,
                            OpticMaximum = prototype.maxDistanceOptic
                        });
                prototype.maxDistance = distance;
                prototype.maxDistanceOptic = distance;
            }
        }

        private static void RefreshGrassSpatialPartitioning()
        {
            GPUInstancerDetailManager[] managers =
                Resources.FindObjectsOfTypeAll<GPUInstancerDetailManager>();
            for (int i = 0; i < managers.Length; i++)
            {
                GPUInstancerDetailManager manager = managers[i];
                if (manager != null && manager.isInitialized)
                    manager.InitializeSpatialPartitioning();
            }
        }

        private void RestoreGrassRenderDistance()
        {
            foreach (KeyValuePair<
                         GPUInstancerTerrainSettings, GrassDistanceState>
                     entry in _grassDistanceStates)
            {
                if (entry.Key == null)
                    continue;
                entry.Key.maxDetailDistance = entry.Value.Maximum;
                entry.Key.maxDetailDistanceLegacy =
                    entry.Value.LegacyMaximum;
            }
            _grassDistanceStates.Clear();
            foreach (KeyValuePair<
                         GPUInstancerPrototype, GrassPrototypeDistanceState>
                     entry in _grassPrototypeDistanceStates)
            {
                if (entry.Key == null)
                    continue;
                entry.Key.maxDistance = entry.Value.Maximum;
                entry.Key.maxDistanceOptic = entry.Value.OpticMaximum;
            }
            _grassPrototypeDistanceStates.Clear();
            ScheduleGrassSpatialRefresh();
        }

        private void ApplyFreecamGraphicsProfile()
        {
            EFT.Settings.SettingsManager manager =
                Singleton<EFT.Settings.SettingsManager>.Instance;
            var settings = manager?.Graphics?.Settings;
            if (settings == null)
            {
                Logger.LogWarning(
                    "Freecam graphics profile could not access EFT settings.");
                return;
            }

            if (!_graphicsProfileCaptured)
            {
                _savedAntialiasing = settings.AntiAliasing.Value;
                _savedDlssMode = settings.DLSSMode.Value;
                _savedFsr2Mode = settings.FSR2Mode.Value;
                _savedFsr3Mode = settings.FSR3Mode.Value;
                _graphicsProfileCaptured = true;
            }

            FreecamAntialiasingMode mode =
                _freecamAntialiasing.Value;
            if (mode == FreecamAntialiasingMode.Gameplay)
            {
                RestoreGameplayGraphicsProfile();
                return;
            }

            EAntialiasingMode antialiasing =
                EAntialiasingMode.None;
            EDLSSMode dlss = EDLSSMode.Off;
            switch (mode)
            {
                case FreecamAntialiasingMode.FXAA:
                    antialiasing = EAntialiasingMode.FXAA;
                    break;
                case FreecamAntialiasingMode.TAA_Low:
                    antialiasing = EAntialiasingMode.TAA_Low;
                    break;
                case FreecamAntialiasingMode.TAA_High:
                    antialiasing = EAntialiasingMode.TAA_High;
                    break;
            }

            settings.FSR2Mode.Value = EFSR2Mode.Off;
            settings.FSR2Mode.ForceApply();
            settings.FSR3Mode.Value = EFSR3Mode.Off;
            settings.FSR3Mode.ForceApply();
            settings.AntiAliasing.Value = antialiasing;
            settings.AntiAliasing.ForceApply();
            settings.DLSSMode.Value = dlss;
            settings.DLSSMode.ForceApply();
            ApplyAntialiasingTuple(
                antialiasing, dlss,
                EFSR2Mode.Off, EFSR3Mode.Off);
        }

        private static void ApplyAntialiasingTuple(
            EAntialiasingMode antialiasing,
            EDLSSMode dlss,
            EFSR2Mode fsr2,
            EFSR3Mode fsr3)
        {
            EFT.CameraControl.CameraManager cameraService =
                EFT.CameraControl.CameraManager.Instance;
            if (cameraService != null)
                cameraService.SetAntiAliasing(
                    antialiasing, dlss, fsr2, fsr3);
        }

        private void RestoreGameplayGraphicsProfile()
        {
            if (!_graphicsProfileCaptured)
                return;

            EFT.Settings.SettingsManager manager =
                Singleton<EFT.Settings.SettingsManager>.Instance;
            var settings = manager?.Graphics?.Settings;
            if (settings != null)
            {
                settings.AntiAliasing.Value = _savedAntialiasing;
                settings.AntiAliasing.ForceApply();
                settings.DLSSMode.Value = _savedDlssMode;
                settings.DLSSMode.ForceApply();
                settings.FSR2Mode.Value = _savedFsr2Mode;
                settings.FSR2Mode.ForceApply();
                settings.FSR3Mode.Value = _savedFsr3Mode;
                settings.FSR3Mode.ForceApply();
                ApplyAntialiasingTuple(
                    _savedAntialiasing, _savedDlssMode,
                    _savedFsr2Mode, _savedFsr3Mode);
            }
            _graphicsProfileCaptured = false;
        }

        private void RestoreCamera()
        {
            SyncPlayerToFreeCamera();
            StopPathPlayback();
            RestoreFreecamWatermarks();
            RestoreGameplayGraphicsProfile();
            RestoreGrassRenderDistance();
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPreRender -= OnCameraPreRender;
            Camera.onPostRender -= OnCameraPostRender;
            RestoreLocalBodyRenderers();
            if (_cameraDetached)
            {
                if (_sourceCamera != null)
                {
                    Transform sourceTransform = _sourceCamera.transform;
                    if (sourceTransform.parent != _sourceCameraParent)
                        sourceTransform.SetParent(_sourceCameraParent, false);
                    sourceTransform.localPosition =
                        _sourceCameraLocalPosition;
                    sourceTransform.localRotation =
                        _sourceCameraLocalRotation;
                    sourceTransform.localScale =
                        _sourceCameraLocalScale;
                    _sourceCamera.enabled = _sourceCameraWasEnabled;
                }
            }
            // Move EFT's real camera and its visibility observers back to
            // the player before changing POV. The POV setter immediately
            // refreshes several render systems using the camera position.
            RestorePlayerCullingPosition();
            RestoreLocalPlayerPointOfView();
            RestorePlayerCullingPosition();
            RestoreIndoorCulling();
            RestoreCrossSceneMeshVisibility();
            bool wasActive = _cameraDetached;
            _cameraDetached = false;
            _gameCamera = null;
            _sourceCamera = null;
            _sourceCameraParent = null;
            _freecamPlayer = null;
            if (wasActive)
                Logger.LogInfo("Free camera disabled; player camera restored.");
        }

        private void RestorePlayerCullingPosition()
        {
            GameWorld world = Singleton<GameWorld>.Instance;
            Player player = world != null ? world.MainPlayer : null;
            Vector3 position = _sourceCamera != null
                ? _sourceCamera.transform.position
                : player != null
                    ? player.Position
                    : Vector3.zero;

            if (Koenigz.PerfectCulling.EFT
                    .PerfectCullingCrossSceneSampler.Exists)
            {
                Koenigz.PerfectCulling.EFT
                    .PerfectCullingCrossSceneSampler sampler =
                    Koenigz.PerfectCulling.EFT
                        .PerfectCullingCrossSceneSampler.Instance;
                if (sampler != null && sampler.CullingCamera != null)
                {
                    sampler.CullingCamera.ObservePosition = position;
                    sampler.CullingCamera.SetDirty();
                }
            }

            EFT.EnvironmentEffect.EnvironmentManager manager =
                EFT.EnvironmentEffect.EnvironmentManager.Instance;
            if (manager != null && player != null)
                manager.SetTriggerForPlayer(
                    player, manager.TryFindTriggerByPos(position));
        }

        private ConfigEntry<KeyCode> BindMovementKey(
            string name, KeyCode defaultKey) =>
            Config.Bind(
                "Free Camera Movement Keys", name, defaultKey,
                "Key used for " + name.ToLowerInvariant() + ".");

        private void CaptureAwaitingMovementKey()
        {
            if (_movementAwaitingKey == null ||
                Time.frameCount <= _movementKeyCaptureStartedFrame ||
                !Input.anyKeyDown)
                return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                FinishMovementKeyCapture();
                return;
            }
            Array values = Enum.GetValues(typeof(KeyCode));
            for (int i = 0; i < values.Length; i++)
            {
                KeyCode key = (KeyCode)values.GetValue(i);
                string name = key.ToString();
                if (key == KeyCode.None ||
                    name.StartsWith("Mouse", StringComparison.Ordinal) ||
                    name.StartsWith("Joystick", StringComparison.Ordinal) ||
                    !Input.GetKeyDown(key))
                    continue;
                _movementAwaitingKey.Value = key;
                FinishMovementKeyCapture();
                return;
            }
        }

        private void FinishMovementKeyCapture()
        {
            _movementAwaitingKey = null;
            _movementKeyCaptureStartedFrame = -1;
        }

        private void DrawMovementKey(
            string label, ConfigEntry<KeyCode> binding)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            GUILayout.FlexibleSpace();
            string key = ReferenceEquals(_movementAwaitingKey, binding)
                ? "[Press key]"
                : "[" + binding.Value + "]";
            if (GUILayout.Button(
                    key, GUI.skin.label, GUILayout.Width(110f)))
            {
                FinishToggleHotkeyCapture();
                _movementAwaitingKey = binding;
                _movementKeyCaptureStartedFrame = Time.frameCount;
            }
            GUILayout.EndHorizontal();
        }

        private void ConfigureToggleHotkeys()
        {
            List<ConfigEntry<bool>> toggles =
                new List<ConfigEntry<bool>>();
            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> pair
                     in Config)
                if (pair.Value is ConfigEntry<bool> toggle)
                    toggles.Add(toggle);

            for (int i = 0; i < toggles.Count; i++)
            {
                ConfigEntry<bool> toggle = toggles[i];
                string name = toggle.Definition.Section + " - " +
                    toggle.Definition.Key;
                _toggleHotkeys.Add(toggle, Config.Bind(
                    "Toggle Hotkeys", name, KeyboardShortcut.Empty,
                    "Toggle " + toggle.Definition.Section + " / " +
                    toggle.Definition.Key + "."));
            }
        }

        private void UpdatePathEditingHotkeys()
        {
            if (_menuOpen || !IsFreecamActive ||
                _pathPlaying || _recordingPath)
                return;
            if (_addPointKey.Value.IsDown())
                AddPathPoint();
            if (_removePointKey.Value.IsDown() &&
                _selectedPathPoint >= 0 &&
                _selectedPathPoint < _pathPoints.Count)
                DeleteSelectedPathPoint();
        }

        private void UpdateToggleHotkeys()
        {
            CaptureAwaitingToggleHotkey();
            foreach (KeyValuePair<
                         ConfigEntry<bool>,
                         ConfigEntry<KeyboardShortcut>> pair
                     in _toggleHotkeys)
            {
                KeyboardShortcut shortcut = pair.Value.Value;
                if (!IsBindableKey(shortcut.MainKey))
                {
                    if (shortcut.MainKey != KeyCode.None)
                        pair.Value.Value = KeyboardShortcut.Empty;
                    continue;
                }
                if (shortcut.IsDown())
                    pair.Key.Value = !pair.Key.Value;
            }
        }

        private void CaptureAwaitingToggleHotkey()
        {
            if ((_toggleAwaitingHotkey == null &&
                 _standaloneAwaitingHotkey == null) ||
                Time.frameCount <= _toggleHotkeyCaptureStartedFrame ||
                !Input.anyKeyDown)
                return;
            ConfigEntry<KeyboardShortcut> hotkey;
            if (_standaloneAwaitingHotkey != null)
                hotkey = _standaloneAwaitingHotkey;
            else if (!_toggleHotkeys.TryGetValue(
                         _toggleAwaitingHotkey, out hotkey))
            {
                FinishToggleHotkeyCapture();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                hotkey.Value = KeyboardShortcut.Empty;
                FinishToggleHotkeyCapture();
                return;
            }
            Array values = Enum.GetValues(typeof(KeyCode));
            for (int i = 0; i < values.Length; i++)
            {
                KeyCode key = (KeyCode)values.GetValue(i);
                if (!IsBindableKey(key) || !Input.GetKeyDown(key))
                    continue;
                hotkey.Value = new KeyboardShortcut(
                    key, CurrentHotkeyModifiers());
                if (ReferenceEquals(hotkey, _menuKey))
                    _menuShortcutLatched = true;
                FinishToggleHotkeyCapture();
                return;
            }
        }

        private void FinishToggleHotkeyCapture()
        {
            _toggleAwaitingHotkey = null;
            _standaloneAwaitingHotkey = null;
            _toggleHotkeyCaptureStartedFrame = -1;
        }

        private void DrawStandaloneHotkey(
            ConfigEntry<KeyboardShortcut> hotkey)
        {
            string label = ReferenceEquals(
                    _standaloneAwaitingHotkey, hotkey)
                ? "[Press key]"
                : FormatToggleHotkey(hotkey.Value);
            if (GUILayout.Button(
                    label, GUI.skin.label,
                    GUILayout.Width(110f)))
            {
                FinishMovementKeyCapture();
                _toggleAwaitingHotkey = null;
                _standaloneAwaitingHotkey = hotkey;
                _toggleHotkeyCaptureStartedFrame =
                    Time.frameCount;
            }
        }

        private void DrawToggleWithHotkey(
            ConfigEntry<bool> toggle, GUIContent content)
        {
            GUILayout.BeginHorizontal();
            toggle.Value = GUILayout.Toggle(toggle.Value, content);
            GUILayout.FlexibleSpace();
            string label = ReferenceEquals(_toggleAwaitingHotkey, toggle)
                ? "[Press key]"
                : FormatToggleHotkey(_toggleHotkeys[toggle].Value);
            if (GUILayout.Button(
                    label, GUI.skin.label, GUILayout.Width(100f)))
            {
                FinishMovementKeyCapture();
                _standaloneAwaitingHotkey = null;
                _toggleAwaitingHotkey = toggle;
                _toggleHotkeyCaptureStartedFrame = Time.frameCount;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawToggleWithHotkey(
            ConfigEntry<bool> toggle, string label) =>
            DrawToggleWithHotkey(toggle, new GUIContent(
                label, toggle.Description.Description));

        private static bool IsBindableKey(KeyCode key)
        {
            if (key == KeyCode.None ||
                key == KeyCode.F13 ||
                key == KeyCode.F14 ||
                key == KeyCode.F15 ||
                IsModifierKey(key))
                return false;
            string name = key.ToString();
            return !name.StartsWith("Mouse", StringComparison.Ordinal) &&
                   !name.StartsWith("Joystick", StringComparison.Ordinal);
        }

        private static bool IsModifierKey(KeyCode key) =>
            key == KeyCode.LeftControl ||
            key == KeyCode.RightControl ||
            key == KeyCode.LeftShift ||
            key == KeyCode.RightShift ||
            key == KeyCode.LeftAlt ||
            key == KeyCode.RightAlt ||
            key == KeyCode.LeftCommand ||
            key == KeyCode.RightCommand;

        private static KeyCode[] CurrentHotkeyModifiers()
        {
            List<KeyCode> modifiers = new List<KeyCode>(3);
            if (Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl))
                modifiers.Add(KeyCode.LeftControl);
            if (Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift))
                modifiers.Add(KeyCode.LeftShift);
            if (Input.GetKey(KeyCode.LeftAlt) ||
                Input.GetKey(KeyCode.RightAlt))
                modifiers.Add(KeyCode.LeftAlt);
            return modifiers.ToArray();
        }

        private static string FormatToggleHotkey(
            KeyboardShortcut shortcut) =>
            shortcut.MainKey == KeyCode.None
                ? "[--]"
                : "[" + shortcut + "]";

        private void OnGUI()
        {
            if (IsFreecamActive &&
                !_menuOpen &&
                !_pathPlaying &&
                !_recordingPath &&
                _showCrosshair.Value)
                DrawCenterPathReticle();
            if (!_menuOpen)
                return;
            MaintainMenuCursor();
            HandleMenuShortcutGuiEvent();
            EnsureSkin();
            DrawPathLabels();
            GUISkin previous = GUI.skin;
            Color previousColor = GUI.color;
            Color previousBackground = GUI.backgroundColor;
            Color previousContent = GUI.contentColor;
            int previousDepth = GUI.depth;
            try
            {
                GUI.depth = -50;
                GUI.skin = _skin;
                GUI.color = Color.white;
                GUI.backgroundColor = Color.white;
                GUI.contentColor = Color.white;
                ClampWindowToScreen();
                _windowRect = GUI.Window(
                    731905, _windowRect, DrawWindow,
                    "CineKit — Free Camera for SPT");
            }
            finally
            {
                GUI.skin = previous;
                GUI.color = previousColor;
                GUI.backgroundColor = previousBackground;
                GUI.contentColor = previousContent;
                GUI.depth = previousDepth;
            }
        }

        private void DrawCenterPathReticle()
        {
            Color previous = GUI.color;
            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(
                new Rect(x - 4f, y - 4f, 8f, 8f),
                Texture2D.whiteTexture);
            GUI.color = _worldHoverPoint >= 0
                ? Color.white
                : new Color(0.3f, 0.85f, 1f, 1f);
            GUI.DrawTexture(
                new Rect(x - 2f, y - 2f, 4f, 4f),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("FREE CAMERA", _headingStyle);
            GUILayout.Space(6f);
            DrawToggleWithHotkey(_freecamEnabled, FreecamContent);
            GUILayout.Space(6f);
            _selectedPanel = GUILayout.Toolbar(
                _selectedPanel, PanelNames, _tabStyle);
            GUILayout.Space(8f);
            _panelScroll = GUILayout.BeginScrollView(
                _panelScroll,
                false,
                true,
                GUILayout.Height(Mathf.Max(
                    120f, _windowRect.height - 205f)));
            if (_selectedPanel == 0)
                DrawCameraPanel();
            else if (_selectedPanel == 1)
                DrawPathPanel();
            else if (_selectedPanel == 2)
                DrawSchematicsPanel();
            else if (_selectedPanel == 3)
                DrawRecordingPanel();
            else
                DrawOtherPanel();
            GUILayout.Space(28f);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 28f));
        }

        private void DrawCameraPanel()
        {
            DrawFreecamPanel();
        }

        private void DrawFreecamPanel()
        {
            GUILayout.Label(
                _freecamEnabled.Value
                    ? "STATUS: FREE CAMERA ACTIVE"
                    : "STATUS: ATTACHED TO PLAYER",
                _headingStyle);
            GUILayout.Space(8f);
            DrawFloatSlider(
                "Camera Speed", _moveSpeed, 0.5f, 50f);
            DrawFloatSlider(
                "Camera Sprint Speed", _sprintSpeed, 0.5f, 200f);
            GUILayout.Label(
                "Manual freecam speeds only. Camera points use their own speed.");
            GUILayout.Space(8f);
            if (DrawSectionHeader(
                    "MOVEMENT KEYS", ref _movementKeysExpanded))
            {
                DrawMovementKey("Move Forward", _moveForwardKey);
                DrawMovementKey("Move Backward", _moveBackwardKey);
                DrawMovementKey("Move Left", _moveLeftKey);
                DrawMovementKey("Move Right", _moveRightKey);
                DrawMovementKey("Move Up", _moveUpKey);
                DrawMovementKey("Move Down", _moveDownKey);
                DrawMovementKey("Sprint", _sprintKey);
                GUILayout.Label(
                    "Click a binding and press a key. Escape cancels.");
            }
            GUILayout.Space(8f);
            DrawToggleWithHotkey(_showCrosshair, "Show Crosshair");
            GUILayout.Space(4f);
            DrawToggleWithHotkey(_hideRaidHud, "Hide Raid HUD");
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            _playerFollowsFreecam.Value = GUILayout.Toggle(
                _playerFollowsFreecam.Value,
                new GUIContent(
                    "NoClip mode",
                    _playerFollowsFreecam.Description.Description));
            GUILayout.FlexibleSpace();
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && IsFreecamActive;
            if (GUILayout.Button(
                    "Teleport Player to Cam",
                    GUILayout.Width(165f)))
                TeleportPlayerToFreeCamera();
            GUI.enabled = wasEnabled;
            string noClipHotkey = ReferenceEquals(
                    _toggleAwaitingHotkey, _playerFollowsFreecam)
                ? "[Press key]"
                : FormatToggleHotkey(
                    _toggleHotkeys[_playerFollowsFreecam].Value);
            if (GUILayout.Button(
                    noClipHotkey,
                    GUI.skin.label,
                    GUILayout.Width(100f)))
            {
                FinishMovementKeyCapture();
                _standaloneAwaitingHotkey = null;
                _toggleAwaitingHotkey = _playerFollowsFreecam;
                _toggleHotkeyCaptureStartedFrame = Time.frameCount;
            }
            GUILayout.EndHorizontal();
            if (_playerFollowsFreecam.Value)
                GUILayout.Label(
                    "Moves the hidden player with the camera. " +
                    "Disabling freecam leaves the player at the final " +
                    "camera location.");
            GUILayout.Space(4f);
            DrawToggleWithHotkey(
                _controlPlayerFromFreecam,
                "Control Player From Freecam");
            if (_controlPlayerFromFreecam.Value)
            {
                GUILayout.Label(
                    "Freezes the freecam view and sends movement, mouse, " +
                    "weapon, and interaction controls to the player. " +
                    "The freecam view remains fixed.");
                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                _disableAimFovChange.Value = GUILayout.Toggle(
                    _disableAimFovChange.Value,
                    new GUIContent(
                        "Disable aim FOV change",
                        _disableAimFovChange.Description.Description));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(4f);
            GUILayout.Label(
                "LOD Distance: " +
                _lodDistanceMultiplier.Value.ToString("0.0") + "x");
            _lodDistanceMultiplier.Value = GUILayout.HorizontalSlider(
                _lodDistanceMultiplier.Value, 1f, 25f);
            GUILayout.Label(
                "LOD distance changes how far higher-detail mesh levels remain active.");
            GUILayout.Space(4f);
            GUILayout.Label(
                "Grass Render Distance: " +
                Mathf.RoundToInt(_grassRenderDistance.Value) + " m");
            _grassRenderDistance.Value = GUILayout.HorizontalSlider(
                _grassRenderDistance.Value, 25f, 500f);
            GUILayout.Label(
                "Freecam-only GPU-instanced grass and terrain detail range.");
            GUILayout.Space(8f);
            DrawToggleWithHotkey(
                _extendedCrossSceneMeshes,
                "Extended Cross-Scene Meshes");
            if (_extendedCrossSceneMeshes.Value)
            {
                GUILayout.Label(
                    "Cross-Scene Mesh Range: " +
                    Mathf.RoundToInt(
                        _crossSceneMeshDistance.Value) + " m");
                _crossSceneMeshDistance.Value =
                    GUILayout.HorizontalSlider(
                        _crossSceneMeshDistance.Value,
                        100f, 3000f);
                GUILayout.Label(
                    "Keeps nearby Perfect Culling rock and map-mesh " +
                    "groups visible outside their baked visibility cells.");
            }
            GUILayout.Space(8f);
            GUILayout.Label("FREECAM ANTIALIASING", _headingStyle);
            _freecamAntialiasing.Value =
                (FreecamAntialiasingMode)GUILayout.SelectionGrid(
                    (int)_freecamAntialiasing.Value,
                    AntialiasingNames,
                    3);
            GUILayout.Label(
                "Applied only while freecam is active. Gameplay restores " +
                "the exact AA/upscaling settings that were active before.");
            GUILayout.Space(8f);
            if (DrawSectionHeader(
                    "MOTION SMOOTHING", ref _smoothingExpanded))
            {
            DrawToggleWithHotkey(
                _motionSmoothing,
                "Cinematic Motion Smoothing (no frame history)");
            if (_motionSmoothing.Value)
            {
                DrawFloatSlider(
                    "Position Response",
                    _positionSmoothing, 1f, 30f);
                DrawFloatSlider(
                    "Rotation Response",
                    _rotationSmoothing, 1f, 30f);
                GUILayout.Label(
                    "Lower response is smoother but adds more camera lag.");
            }
            }
            GUILayout.Space(6f);
            GUILayout.Label(
                "Textures, shadows, post-processing, " +
                "and distance culling are controlled by EFT's current settings.");
            GUILayout.Space(6f);
            GUILayout.Label(
                "Movement controls can be changed above. Mouse controls camera look.");
            GUILayout.Label(
                "Press HOME to close the menu and take control of the camera.");
        }

        private static bool DrawSectionHeader(
            string title, ref bool expanded)
        {
            if (GUILayout.Button(
                    (expanded ? "▼  " : "▶  ") + title,
                    GUILayout.Height(28f)))
                expanded = !expanded;
            return expanded;
        }

        private static void DrawFloatSlider(
            string label, ConfigEntry<float> setting,
            float minimum, float maximum)
        {
            GUILayout.Label(
                label + ": " + setting.Value.ToString("0.00"));
            setting.Value = GUILayout.HorizontalSlider(
                setting.Value, minimum, maximum);
        }

        private void DrawPathPanel()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(
                GUI.skin.box, GUILayout.Width(220f));
            GUILayout.Label("GROUPS", _headingStyle);
            _groupScroll = GUILayout.BeginScrollView(
                _groupScroll, GUILayout.Height(260f));
            for (int i = 0; i < _pathGroups.Count; i++)
                if (GUILayout.Toggle(
                        i == _selectedPathGroup,
                        _pathGroups[i].Name,
                        GUI.skin.button))
                {
                    if (_selectedPathGroup != i)
                    {
                        StopPathPlayback();
                        _selectedPathGroup = i;
                        _selectedPathPoint = -1;
                    }
                }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(
                    new GUIContent("+", "Add group"),
                    GUILayout.Width(42f)))
                AddPathGroup();
            GUI.enabled = _pathGroups.Count > 1;
            if (GUILayout.Button(
                    new GUIContent("−", "Remove selected group"),
                    GUILayout.Width(42f)))
                DeleteSelectedPathGroup();
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("POINTS", _headingStyle);
            if (_selectedPathGroup >= 0 &&
                _selectedPathGroup < _pathGroups.Count)
                _pathGroups[_selectedPathGroup].Name =
                    GUILayout.TextField(
                        _pathGroups[_selectedPathGroup].Name);
            _pathScroll = GUILayout.BeginScrollView(
                _pathScroll, GUILayout.Height(260f));
            for (int i = 0; i < _pathPoints.Count; i++)
                DrawPathPointRow(i);
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(
                    new GUIContent("+", "Add point at current camera"),
                    GUILayout.Width(42f)))
                AddPathPoint();
            GUI.enabled = _selectedPathPoint >= 0 &&
                          _selectedPathPoint < _pathPoints.Count;
            if (GUILayout.Button(
                    new GUIContent("−", "Remove selected point"),
                    GUILayout.Width(42f)))
                DeleteSelectedPathPoint();
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            if (_selectedPathGroup >= 0 &&
                _selectedPathGroup < _pathGroups.Count)
            {
                CameraPathGroup timingGroup =
                    _pathGroups[_selectedPathGroup];
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                DrawPointSlider(
                    "Total Path Duration",
                    ref timingGroup.TotalDuration,
                    0f, 600f, " s");
                GUILayout.EndVertical();
                timingGroup.SmoothTransitions = GUILayout.Toggle(
                    timingGroup.SmoothTransitions,
                    "Smooth Transitions",
                    GUILayout.Width(190f));
                GUILayout.EndHorizontal();
                GUILayout.Label(
                    timingGroup.TotalDuration > 0f
                        ? "Overrides individual segment timing and scales " +
                          "the complete connected chain to this duration."
                        : "0 seconds uses each segment's duration or speed.");
                if (timingGroup.SmoothTransitions)
                    GUILayout.Label(
                        "Blends connected points into one continuous curve.");
            }

            DrawSelectedPointInspector();
            DrawSelectedPointStartStop();

            GUILayout.BeginHorizontal();
            GUI.enabled = _selectedPathPoint >= 0 &&
                          _selectedPathPoint <
                          _pathPoints.Count;
            bool canMoveSelectedPoint =
                GUI.enabled;
            GUI.enabled = canMoveSelectedPoint;
            if (GUILayout.Button(
                    "Move Point to Current location"))
            {
                CameraPathPoint point =
                    _pathPoints[_selectedPathPoint];
                SetPointWorldPosition(
                    point, _freePosition);
            }
            GUI.enabled = _selectedPathPoint >= 0 &&
                          _selectedPathPoint <
                          _pathPoints.Count;
            if (GUILayout.Button(
                    "Set Point looking direction to location"))
                SetSelectedLookTarget();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(
                "Close the menu and aim the center dot at a handle. " +
                "Hold left click to grab it; fly/look to move it and " +
                "use the wheel for depth.");
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(220f));
            GUI.enabled = _pathPoints.Count >= 2;
            if (!_pathPlaying)
            {
                if (GUILayout.Button(
                        "Preview Path",
                        GUILayout.Width(220f)))
                    StartPathPlayback(false);
            }
            else if (GUILayout.Button(
                         "Stop Preview",
                         GUILayout.Width(220f)))
            {
                StopPathPlayback();
            }
            GUI.enabled = true;
            if (_pathPlaying)
                GUILayout.Label(
                    "Close the UI to begin pathing.",
                    GUILayout.Width(220f));
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(250f));
            DrawToggleWithHotkey(_pathLoop, "Loop");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawSelectedPointStartStop()
        {
            if (_selectedPathPoint < 0 ||
                _selectedPathPoint >= _pathPoints.Count)
                return;
            CameraPathPoint point =
                _pathPoints[_selectedPathPoint];
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(
                "POINT " + (_selectedPathPoint + 1) +
                " — START / STOP", _headingStyle);
            DrawPointSlider(
                "Stay at Point", ref point.StayDuration,
                0f, 120f, " s");
            GUILayout.Label(
                "Holds the camera here before continuing. Entity Points " +
                "keep following their target while held.");
            DrawPointSlider(
                "Start Delay", ref point.StartDelay,
                0f, 10f, " s");
            DrawPointSlider(
                "Stop Delay", ref point.StopDelay,
                0f, 10f, " s");
            GUILayout.EndVertical();
        }

        private void DrawRecordingPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(
                "PLAY PATH AND RECORD TO MP4", _headingStyle);
            GUILayout.BeginHorizontal();
            int fpsMode = _recordCustomFps ? 1 : 0;
            fpsMode = GUILayout.SelectionGrid(
                fpsMode, RecordingFpsNames,
                2,
                GUILayout.Width(280f));
            _recordCustomFps = fpsMode == 1;
            if (_recordCustomFps)
            {
                GUILayout.Label(
                    "FPS", GUILayout.Width(30f));
                _recordFpsText = GUILayout.TextField(
                    _recordFpsText, GUILayout.Width(70f));
            }
            else
            {
                int nativeFps =
                    Application.targetFrameRate > 0
                        ? Application.targetFrameRate
                        : Mathf.RoundToInt(
                            (float)Screen.currentResolution
                                .refreshRateRatio.value);
                GUILayout.Label(
                    "Current: " +
                    (nativeFps > 0 ? nativeFps : 60) +
                    " FPS");
            }
            GUILayout.Label(
                "FPS Limit", GUILayout.Width(58f));
            _recordFpsLimitText = GUILayout.TextField(
                _recordFpsLimitText, GUILayout.Width(55f));
            GUILayout.FlexibleSpace();
            GUI.enabled = !_recordingPath &&
                          _pathPoints.Count >= 2;
            if (GUILayout.Button(
                    "Record Path and Save MP4",
                    GUILayout.Width(220f),
                    GUILayout.Height(28f)))
                StartPathRecording();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            int observedFps = Mathf.RoundToInt(
                1f / Mathf.Max(0.0001f, Time.smoothDeltaTime));
            GUILayout.Label(
                "Observed performance: about " + observedFps +
                " FPS. Set the recording FPS and FPS limit at or " +
                "below the stable FPS you normally maintain to avoid dips. " +
                "Audio is captured from Unity's game mix.");
            GUILayout.Label(_recordingStatus);
            GUILayout.Label(
                "Press ESC while recording to stop early and save " +
                "everything captured so far.");
            GUILayout.EndVertical();
            GUILayout.BeginHorizontal();
            GUI.enabled = _pathPoints.Count >= 2;
            if (!_pathPlaying)
            {
                if (GUILayout.Button(
                        "Play Path (Without Recording)"))
                    StartPathPlayback(true);
            }
            else if (GUILayout.Button("Stop Path"))
            {
                StopPathPlayback();
            }
            GUI.enabled = true;
            DrawToggleWithHotkey(_pathLoop, "Loop");
            GUILayout.EndHorizontal();
            if (_pathPlaying)
                GUILayout.Label("Close the UI to begin pathing.");
            GUILayout.Label(
                _pathPoints.Count >= 2
                    ? "Ready: " + _pathPoints.Count +
                      " points in " +
                      _pathGroups[_selectedPathGroup].Name + "."
                    : "Add at least two points in the Path tab first.");
        }

        private void DrawOtherPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("MENU OPTIONS", _headingStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Open / Close Menu");
            GUILayout.FlexibleSpace();
            DrawStandaloneHotkey(_menuKey);
            GUILayout.EndHorizontal();
            GUILayout.Label(
                "Click the binding and press a keyboard key. " +
                "Escape clears it.");

            GUILayout.Space(10f);
            GUILayout.Label("PATH EDITING HOTKEYS", _headingStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Add Point");
            GUILayout.FlexibleSpace();
            DrawStandaloneHotkey(_addPointKey);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Remove Selected Point");
            GUILayout.FlexibleSpace();
            DrawStandaloneHotkey(_removePointKey);
            GUILayout.EndHorizontal();
            GUILayout.Label(
                "These work with the menu closed while freecam is active.");

            GUILayout.Space(10f);
            GUILayout.Label("GUI PRIMARY COLOR", _headingStyle);
            Color accent;
            if (!ColorUtility.TryParseHtmlString(
                    _accentColor.Value, out accent))
                accent = new Color32(120, 207, 245, 255);

            GUILayout.BeginHorizontal();
            Color previous = GUI.color;
            GUI.color = accent;
            GUILayout.Box(
                GUIContent.none,
                GUILayout.Width(48f),
                GUILayout.Height(48f));
            GUI.color = previous;
            GUILayout.BeginVertical();
            GUILayout.Label("Hex RGBA");
            string hex = GUILayout.TextField(
                _accentColor.Value,
                GUILayout.Width(180f));
            if (!string.Equals(
                    hex, _accentColor.Value,
                    StringComparison.Ordinal))
                _accentColor.Value = hex;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            float red = DrawColorChannel("Red", accent.r);
            float green = DrawColorChannel("Green", accent.g);
            float blue = DrawColorChannel("Blue", accent.b);
            float alpha = DrawColorChannel("Alpha", accent.a);
            Color edited = new Color(red, green, blue, alpha);
            if (edited != accent)
                _accentColor.Value =
                    "#" + ColorUtility.ToHtmlStringRGBA(edited);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Color"))
                _accentColor.Value = "#78CFF5FF";
            if (GUILayout.Button("Reset Menu Key"))
                _menuKey.Value =
                    new KeyboardShortcut(KeyCode.Home);
            if (GUILayout.Button("Reset Point Keys"))
            {
                _addPointKey.Value =
                    new KeyboardShortcut(KeyCode.Plus);
                _removePointKey.Value =
                    new KeyboardShortcut(KeyCode.Minus);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(
                "The primary color updates tabs, sliders, checkboxes, " +
                "headings, and highlighted controls.");
            GUILayout.EndVertical();
        }

        private static float DrawColorChannel(
            string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                label + ": " +
                Mathf.RoundToInt(value * 255f),
                GUILayout.Width(100f));
            value = GUILayout.HorizontalSlider(
                value, 0f, 1f);
            GUILayout.EndHorizontal();
            return value;
        }

        private void DrawSchematicsPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("PATH SCHEMATICS", _headingStyle);
            GUILayout.Label("SCHEMATIC NAME");
            _templateName = GUILayout.TextField(_templateName);
            GUILayout.BeginHorizontal();
            GUI.enabled = _pathPoints.Count > 0;
            if (GUILayout.Button(
                    "Copy Points as Camera Relative"))
                SaveCurrentPathTemplate(
                    SchematicSpace.CameraRelative);
            if (GUILayout.Button(
                    "Copy Points as World Coordinates"))
                SaveCurrentPathTemplate(
                    SchematicSpace.WorldCoordinates);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label(
                "Camera Relative uses the current camera as its anchor. " +
                "World Coordinates preserves the exact map positions.");

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(
                GUI.skin.box, GUILayout.Width(430f));
            GUILayout.Label("SAVED SCHEMATICS", _headingStyle);
            _templateScroll = GUILayout.BeginScrollView(
                _templateScroll, GUILayout.Height(300f));
            for (int i = 0; i < _savedPathTemplates.Count; i++)
                if (GUILayout.Toggle(
                        i == _selectedTemplate,
                        _savedPathTemplates[i].Name +
                        " (" +
                        _savedPathTemplates[i].Points.Count +
                        " points, " +
                        (_savedPathTemplates[i].Space ==
                         SchematicSpace.CameraRelative
                            ? "Relative"
                            : "World") +
                        ")",
                        GUI.skin.button))
                {
                    _selectedTemplate = i;
                }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("SELECTED SCHEMATIC", _headingStyle);
            bool hasSelection =
                _selectedTemplate >= 0 &&
                _selectedTemplate < _savedPathTemplates.Count;
            if (hasSelection)
            {
                SavedPathTemplate selected =
                    _savedPathTemplates[_selectedTemplate];
                GUILayout.Label(selected.Name);
                GUILayout.Label(
                    selected.Points.Count + " camera points");
                GUILayout.Label(
                    "Type: " +
                    (selected.Space ==
                     SchematicSpace.CameraRelative
                        ? "Camera Relative"
                        : "World Coordinates"));
                GUILayout.Space(8f);
            }
            else
            {
                GUILayout.Label(
                    "Select a schematic from the list.");
            }
            GUI.enabled = hasSelection;
            string spawnLabel =
                hasSelection &&
                _savedPathTemplates[_selectedTemplate].Space ==
                SchematicSpace.WorldCoordinates
                    ? "Spawn at Saved World Position"
                    : "Spawn at Current Position";
            if (GUILayout.Button(
                    spawnLabel,
                    GUILayout.Height(34f)))
                LoadSelectedPathTemplate();
            if (GUILayout.Button("Delete Schematic"))
                DeleteSelectedPathTemplate();
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Label(
                "Camera Relative schematics use the free camera as their " +
                "anchor. World Coordinates schematics preserve their exact " +
                "map positions when saved and spawned.");
            GUILayout.Label(
                "JSON files are stored in Hysocs-CineKit/Schematics " +
                "so they can be shared or included in a release.");
            GUILayout.EndVertical();
        }

        private void DrawSelectedPointInspector()
        {
            if (_selectedPathPoint < 0 ||
                _selectedPathPoint >= _pathPoints.Count)
            {
                GUILayout.Label(
                    "Select a point to edit its motion settings.");
                return;
            }

            CameraPathPoint point =
                _pathPoints[_selectedPathPoint];
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(
                "POINT " + (_selectedPathPoint + 1) +
                " SETTINGS", _headingStyle);
            string pointTypeLabel =
                point.Type == CameraPathPointType.Entity
                    ? "Entity Point"
                    : "3D Point";
            if (GUILayout.Button(
                    "Point Type: " + pointTypeLabel))
                _pointTypeDropdownOpen =
                    !_pointTypeDropdownOpen;
            if (_pointTypeDropdownOpen)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                if (GUILayout.Button("3D Point"))
                {
                    if (point.Type != CameraPathPointType.World)
                        SetPathPointType(
                            point, CameraPathPointType.World);
                    _pointTypeDropdownOpen = false;
                }
                if (GUILayout.Button("Entity Point"))
                {
                    if (point.Type != CameraPathPointType.Entity)
                        SetPathPointType(
                            point, CameraPathPointType.Entity);
                    _pointTypeDropdownOpen = false;
                }
                GUILayout.EndVertical();
            }
            if (point.Type == CameraPathPointType.Entity)
                DrawEntityPointSettings(point);
            GUILayout.Space(6f);
            int connection = point.NextPoint;
            string connectionLabel =
                connection >= 0 &&
                connection < _pathPoints.Count &&
                connection != _selectedPathPoint
                    ? "Point " + (connection + 1)
                    : "None";
            if (GUILayout.Button(
                    "Connect To: " + connectionLabel))
                _connectionDropdownOpen =
                    !_connectionDropdownOpen;
            if (_connectionDropdownOpen)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                if (GUILayout.Button("None"))
                {
                    SetPointConnection(
                        _selectedPathPoint, -1);
                    _connectionDropdownOpen = false;
                }
                for (int i = 0; i < _pathPoints.Count; i++)
                {
                    if (i == _selectedPathPoint)
                        continue;
                    if (GUILayout.Button(
                            "Point " + (i + 1)))
                    {
                        SetPointConnection(
                            _selectedPathPoint, i);
                        _connectionDropdownOpen = false;
                    }
                }
                GUILayout.EndVertical();
            }
            point.StopHere = GUILayout.Toggle(
                point.StopHere,
                "Stop Here");
            if (point.StopHere)
                GUILayout.Label(
                    _selectedPathPoint == 0
                        ? "As the starting point, this stops playback " +
                          "only when the path returns here."
                        : "Playback brakes and ends when it arrives " +
                          "at this point.");
            DrawPointSlider(
                "Speed", ref point.Speed, 0.5f, 100f, " m/s");
            DrawPointSlider(
                "Segment Duration", ref point.SegmentDuration,
                0f, 120f, " s");
            GUILayout.Label(
                point.SegmentDuration > 0f
                    ? "This segment reaches its next point and facing " +
                      "in the selected number of seconds."
                    : "0 seconds uses movement speed.");
            DrawPointSlider(
                "Acceleration", ref point.Acceleration,
                0.1f, 25f, " m/s²");
            DrawPointSlider(
                "Deceleration", ref point.Deceleration,
                0.1f, 25f, " m/s²");
            GUILayout.EndVertical();
        }

        private void SetPathPointType(
            CameraPathPoint point, CameraPathPointType type)
        {
            Vector3 current = GetPointPosition(point);
            Vector3 currentLookTarget = GetPointLookTarget(point);
            point.Type = type;
            _entityDropdownOpen = false;
            if (type != CameraPathPointType.Entity)
            {
                Vector3 delta = current - point.Position;
                point.Position = current;
                point.LookTarget =
                    currentLookTarget;
                point.ResolvedPositionInitialized = false;
                return;
            }
            Player player = _freecamPlayer;
            if (player == null)
                return;
            Quaternion inverseYaw = Quaternion.Inverse(
                GetEntityBodyRotation(player));
            point.EntityProfileId = string.Empty;
            point.EntityAttachment = EntityAttachmentPoint.Head;
            point.EntityOffset =
                inverseYaw *
                (point.Position -
                 GetEntityAttachmentPosition(
                     player, point.EntityAttachment));
            point.EntityAimOffset =
                Quaternion.Inverse(GetEntityLookRotation(player)) *
                (point.LookTarget - point.Position);
            if (point.StayDuration <= 0f)
                point.StayDuration = 5f;
            point.ResolvedPosition = point.Position;
            point.ResolvedPositionInitialized = true;
        }

        private void DrawEntityPointSettings(CameraPathPoint point)
        {
            Player selected = FindPathPointEntity(point);
            string entityLabel = selected == null
                ? "Target unavailable"
                : selected == _freecamPlayer
                    ? "Local Player"
                    : selected.Profile.Nickname;
            if (GUILayout.Button("Entity: " + entityLabel))
                _entityDropdownOpen = !_entityDropdownOpen;
            if (_entityDropdownOpen)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GameWorld world = Singleton<GameWorld>.Instance;
                if (world != null)
                {
                    DrawEntityChoice(point, world.MainPlayer);
                    foreach (IPlayer candidate in world.RegisteredPlayers)
                    {
                        Player player = candidate as Player;
                        if (player == null ||
                            player == world.MainPlayer)
                            continue;
                        DrawEntityChoice(point, player);
                    }
                }
                GUILayout.EndVertical();
            }

            int attachment = GUILayout.SelectionGrid(
                (int)point.EntityAttachment,
                new[] { "Head", "Chest" },
                2);
            if (attachment != (int)point.EntityAttachment &&
                selected != null)
            {
                Vector3 current = GetPointPosition(point);
                point.EntityAttachment =
                    (EntityAttachmentPoint)attachment;
                Quaternion rotation =
                    point.FollowEntityLookDirection
                        ? GetEntityLookRotation(selected)
                        : GetEntityBodyRotation(selected);
                point.EntityOffset =
                    Quaternion.Inverse(rotation) *
                    (current -
                     GetEntityAttachmentPosition(
                         selected, point.EntityAttachment));
            }
            bool followLook = GUILayout.Toggle(
                point.FollowEntityLookDirection,
                "Follow entity looking direction");
            if (followLook != point.FollowEntityLookDirection &&
                selected != null)
                SetEntityFollowLookDirection(
                    point, selected, followLook);
            point.SwivelPathToNext = GUILayout.Toggle(
                point.SwivelPathToNext,
                "Swivel path toward next point");
            GUILayout.Label(
                "Keeps the attached segment facing its next point and " +
                "shortens the curve as the points get closer.");

            GUILayout.Label(
                "Offset: X " + point.EntityOffset.x.ToString("0.00") +
                "  Y " + point.EntityOffset.y.ToString("0.00") +
                "  Z " + point.EntityOffset.z.ToString("0.00"));
            GUILayout.Label(
                "Drag this point normally in the world or curve editor " +
                "to change its entity-relative offset.");
            DrawPointSlider(
                "Attachment Lerp",
                ref point.AttachmentResponse,
                0f, 30f, "");
            DrawPointSlider(
                "Attachment Speed",
                ref point.AttachmentSpeed,
                0f, 100f, " m/s");
            GUILayout.Label(
                "The point follows the entity's rotated XYZ offset. " +
                "Lower values create more trailing lag; 0 removes that " +
                "limit, so setting both to 0 follows exactly.");
            if (GUILayout.Button("Set Offset From Current Camera") &&
                selected != null)
            {
                Quaternion inverseYaw = Quaternion.Inverse(
                    point.FollowEntityLookDirection
                        ? GetEntityLookRotation(selected)
                        : GetEntityBodyRotation(selected));
                point.EntityOffset =
                    inverseYaw *
                    (_freePosition -
                     GetEntityAttachmentPosition(
                         selected, point.EntityAttachment));
            }
        }

        private void SetEntityFollowLookDirection(
            CameraPathPoint point, Player player, bool enabled)
        {
            Vector3 currentPosition = GetPointPosition(point);
            Vector3 currentLookTarget = GetPointLookTarget(point);
            Quaternion rotation = enabled
                ? GetEntityLookRotation(player)
                : GetEntityBodyRotation(player);
            point.FollowEntityLookDirection = enabled;
            point.EntityOffset = Quaternion.Inverse(rotation) *
                (currentPosition -
                 GetEntityAttachmentPosition(
                     player, point.EntityAttachment));
            if (enabled)
                point.EntityAimOffset =
                    Quaternion.Inverse(rotation) *
                    (currentLookTarget - currentPosition);
            else
                point.LookTarget = point.Position +
                    (currentLookTarget - currentPosition);
        }

        private void DrawEntityChoice(
            CameraPathPoint point, Player player)
        {
            if (player == null)
                return;
            string label = player == _freecamPlayer
                ? "Local Player"
                : player.Profile.Nickname;
            if (!GUILayout.Button(label))
                return;
            Vector3 current = GetPointPosition(point);
            Vector3 currentLookTarget = GetPointLookTarget(point);
            Quaternion inverseYaw = Quaternion.Inverse(
                point.FollowEntityLookDirection
                    ? GetEntityLookRotation(player)
                    : GetEntityBodyRotation(player));
            point.EntityProfileId = player == _freecamPlayer
                ? string.Empty
                : player.ProfileId;
            point.EntityOffset =
                inverseYaw *
                (current -
                 GetEntityAttachmentPosition(
                     player, point.EntityAttachment));
            if (point.FollowEntityLookDirection)
                point.EntityAimOffset =
                    inverseYaw *
                    (currentLookTarget - current);
            point.ResolvedPosition = current;
            point.ResolvedPositionInitialized = true;
            _entityDropdownOpen = false;
        }

        private void SetPointConnection(
            int sourceIndex, int targetIndex)
        {
            CameraPathPoint source = _pathPoints[sourceIndex];
            source.NextPoint = targetIndex;
            if (targetIndex < 0 ||
                targetIndex >= _pathPoints.Count ||
                targetIndex == sourceIndex)
                return;
            CameraPathPoint target = _pathPoints[targetIndex];
            Vector3 third =
                (GetPointPosition(target) -
                 GetPointPosition(source)) / 3f;
            source.OutTangent = third;
            target.InTangent = -third;
            UpdateEntityOutgoingConnection(source);
        }

        private void DrawCurveEditor(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                _selectedPathGroup >= 0
                    ? _pathGroups[_selectedPathGroup].Name
                    : "No path selected",
                _headingStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Button("Close", GUILayout.Width(90f));
            GUILayout.EndHorizontal();

            if (_pathPoints.Count == 0)
            {
                GUILayout.Label(
                    "Add points to the selected group first.");
                GUI.DragWindow(new Rect(
                    0f, 0f, _curveEditorRect.width, 28f));
                return;
            }

            Rect topView = new Rect(
                16f, 62f,
                (_curveEditorRect.width - 48f) * 0.5f,
                _curveEditorRect.height - 82f);
            Rect sideView = new Rect(
                topView.xMax + 16f, 62f,
                topView.width, topView.height);
            GUI.Box(topView, "TOP VIEW (X / Z)");
            GUI.Box(
                sideView,
                "ELEVATION (Y ONLY - BEHIND AVERAGE LOOK)");
            DrawCurveProjection(topView, 0);
            DrawCurveProjection(sideView, 1);
            HandleCurveDrag(topView, sideView);
            GUI.DragWindow(new Rect(
                0f, 0f, _curveEditorRect.width, 28f));
        }

        private void GetCurveProjectionBounds(
            int view,
            out Vector2 minimum,
            out Vector2 maximum)
        {
            minimum = new Vector2(float.MaxValue, float.MaxValue);
            maximum = new Vector2(float.MinValue, float.MinValue);
            foreach (CameraPathPoint point in _pathPoints)
            {
                Vector3 pointPosition = GetPointPosition(point);
                Vector3[] positions =
                {
                    pointPosition,
                    pointPosition + point.InTangent,
                    pointPosition + point.OutTangent,
                    GetPointLookTarget(point)
                };
                foreach (Vector3 position in positions)
                {
                    Vector2 projected = view == 0
                        ? new Vector2(position.x, position.z)
                        : new Vector2(
                            Vector3.Dot(
                                position,
                                GetAveragePathRight()),
                            position.y);
                    minimum = Vector2.Min(minimum, projected);
                    maximum = Vector2.Max(maximum, projected);
                }
            }
            if (view == 1)
            {
                minimum.y = Mathf.Min(minimum.y, 0f);
                maximum.y = Mathf.Max(maximum.y, 0f);
            }
            Vector2 size = maximum - minimum;
            minimum -= new Vector2(
                Mathf.Max(1f, size.x * 0.15f),
                Mathf.Max(1f, size.y * 0.15f));
            maximum += new Vector2(
                Mathf.Max(1f, size.x * 0.15f),
                Mathf.Max(1f, size.y * 0.15f));
        }

        private Vector2 ProjectCurvePoint(
            Vector3 position,
            Rect rect,
            int view,
            Vector2 minimum,
            Vector2 maximum)
        {
            Vector2 point = view == 0
                ? new Vector2(position.x, position.z)
                : new Vector2(
                    Vector3.Dot(
                        position, GetAveragePathRight()),
                    position.y);
            float x = Mathf.InverseLerp(
                minimum.x, maximum.x, point.x);
            float y = Mathf.InverseLerp(
                minimum.y, maximum.y, point.y);
            return new Vector2(
                Mathf.Lerp(rect.x + 18f, rect.xMax - 18f, x),
                Mathf.Lerp(rect.yMax - 18f, rect.y + 28f, y));
        }

        private void DrawCurveProjection(Rect rect, int view)
        {
            GetCurveProjectionBounds(
                view, out Vector2 minimum,
                out Vector2 maximum);
            if (view == 1)
            {
                float seaLevel = Mathf.Lerp(
                    rect.yMax - 18f,
                    rect.y + 28f,
                    Mathf.InverseLerp(
                        minimum.y, maximum.y, 0f));
                DrawGuiLine(
                    new Vector2(rect.x + 18f, seaLevel),
                    new Vector2(rect.xMax - 18f, seaLevel),
                    new Color(0.25f, 0.55f, 0.8f, 0.8f),
                    1f);
                GUI.Label(
                    new Rect(
                        rect.x + 20f, seaLevel - 20f,
                        100f, 20f),
                    "Sea level  Y=0");
            }
            for (int i = 0; i < _pathPoints.Count - 1; i++)
            {
                Vector2 previous = ProjectCurvePoint(
                    GetPointPosition(_pathPoints[i]), rect, view,
                    minimum, maximum);
                for (int sample = 1; sample <= 24; sample++)
                {
                    Vector2 current = ProjectCurvePoint(
                        EvaluateBezier(
                            _pathPoints[i],
                            _pathPoints[i + 1],
                            sample / 24f),
                        rect, view, minimum, maximum);
                    DrawGuiLine(
                        previous, current,
                        new Color(0.3f, 0.85f, 1f), 2f);
                    previous = current;
                }
            }

            for (int i = 0; i < _pathPoints.Count; i++)
            {
                CameraPathPoint point = _pathPoints[i];
                Vector3 pointPosition = GetPointPosition(point);
                Vector2 center = ProjectCurvePoint(
                    pointPosition, rect, view,
                    minimum, maximum);
                Vector2 incoming = ProjectCurvePoint(
                    pointPosition + point.InTangent,
                    rect, view, minimum, maximum);
                Vector2 outgoing = ProjectCurvePoint(
                    pointPosition + point.OutTangent,
                    rect, view, minimum, maximum);
                DrawGuiLine(
                    incoming, outgoing,
                    new Color(1f, 0.7f, 0.15f), 1f);
                GUI.Box(
                    new Rect(center.x - 6f, center.y - 6f, 12f, 12f),
                    (i + 1).ToString());
                GUI.Box(new Rect(
                    incoming.x - 4f, incoming.y - 4f, 8f, 8f), "");
                GUI.Box(new Rect(
                    outgoing.x - 4f, outgoing.y - 4f, 8f, 8f), "");

                Vector2 target = ProjectCurvePoint(
                    GetPointLookTarget(point), rect, view,
                    minimum, maximum);
                DrawGuiLine(
                    center, target,
                    new Color(1f, 0.3f, 0.75f), 1.5f);
                Vector2 direction = target - center;
                if (direction.sqrMagnitude > 0.001f)
                {
                    direction.Normalize();
                    Vector2 perpendicular =
                        new Vector2(-direction.y, direction.x);
                    Vector2 nose =
                        center + direction * 14f;
                    Vector2 rear =
                        center - direction * 8f;
                    Vector2 rearLeft =
                        rear + perpendicular * 8f;
                    Vector2 rearRight =
                        rear - perpendicular * 8f;
                    Color cameraColor =
                        i == _selectedPathPoint
                            ? new Color(1f, 0.75f, 0.15f)
                            : new Color(0.3f, 0.85f, 1f);
                    DrawGuiLine(
                        nose, rearLeft, cameraColor, 2f);
                    DrawGuiLine(
                        rearLeft, rearRight, cameraColor, 2f);
                    DrawGuiLine(
                        rearRight, nose, cameraColor, 2f);
                }
                GUI.color = new Color(1f, 0.3f, 0.75f);
                GUI.Box(
                    new Rect(
                        target.x - 5f, target.y - 5f,
                        10f, 10f),
                    "");
                GUI.color = Color.white;
            }
        }

        private void HandleCurveDrag(Rect topView, Rect sideView)
        {
            Event current = Event.current;
            if (current == null)
                return;
            if (current.type == EventType.MouseUp)
            {
                _curveDragPoint = -1;
                return;
            }
            if (current.type == EventType.MouseDown)
            {
                Rect viewRect = topView.Contains(current.mousePosition)
                    ? topView
                    : sideView.Contains(current.mousePosition)
                        ? sideView
                        : default;
                if (viewRect.width <= 0f)
                    return;
                _curveDragView = viewRect == topView ? 0 : 1;
                GetCurveProjectionBounds(
                    _curveDragView, out Vector2 minimum,
                    out Vector2 maximum);
                float best = 14f * 14f;
                for (int i = 0; i < _pathPoints.Count; i++)
                {
                    CameraPathPoint point = _pathPoints[i];
                    Vector3 pointPosition = GetPointPosition(point);
                    Vector3[] candidates =
                    {
                        pointPosition,
                        pointPosition + point.InTangent,
                        pointPosition + point.OutTangent
                    };
                    for (int handle = 0; handle < 3; handle++)
                    {
                        Vector2 screen = ProjectCurvePoint(
                            candidates[handle], viewRect,
                            _curveDragView, minimum, maximum);
                        float distance =
                            (screen - current.mousePosition)
                            .sqrMagnitude;
                        if (distance < best)
                        {
                            best = distance;
                            _curveDragPoint = i;
                            _curveDragHandle = handle;
                        }
                    }
                }
                if (_curveDragPoint >= 0)
                {
                    _selectedPathPoint = _curveDragPoint;
                    current.Use();
                }
                return;
            }
            if (current.type != EventType.MouseDrag ||
                _curveDragPoint < 0 ||
                _curveDragPoint >= _pathPoints.Count)
                return;

            Rect activeRect =
                _curveDragView == 0 ? topView : sideView;
            GetCurveProjectionBounds(
                _curveDragView, out Vector2 min,
                out Vector2 max);
            Vector2 worldDelta = new Vector2(
                current.delta.x *
                (max.x - min.x) /
                Mathf.Max(1f, activeRect.width - 36f),
                -current.delta.y *
                (max.y - min.y) /
                Mathf.Max(1f, activeRect.height - 46f));
            Vector3 delta = _curveDragView == 0
                ? new Vector3(worldDelta.x, 0f, worldDelta.y)
                : Vector3.up * worldDelta.y;
            CameraPathPoint dragged =
                _pathPoints[_curveDragPoint];
            if (_curveDragHandle == 0)
                SetPointWorldPosition(
                    dragged,
                    GetPointPosition(dragged) + delta);
            else if (_curveDragHandle == 1)
                dragged.InTangent += delta;
            else
                dragged.OutTangent += delta;
            current.Use();
        }

        private Vector3 GetAveragePathRight()
        {
            Vector3 averageForward = Vector3.zero;
            foreach (CameraPathPoint point in _pathPoints)
            {
                Vector3 direction =
                    GetPointLookTarget(point) -
                    GetPointPosition(point);
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.000001f)
                    averageForward += direction.normalized;
            }
            if (averageForward.sqrMagnitude <= 0.000001f)
                averageForward = Vector3.forward;
            averageForward.Normalize();
            Vector3 right = Vector3.Cross(
                Vector3.up, averageForward);
            return right.sqrMagnitude <= 0.000001f
                ? Vector3.right
                : right.normalized;
        }

        private static void DrawGuiLine(
            Vector2 start,
            Vector2 end,
            Color color,
            float width)
        {
            Matrix4x4 matrix = GUI.matrix;
            Color previous = GUI.color;
            float angle = Vector2.SignedAngle(
                Vector2.right, end - start);
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.color = color;
            GUI.DrawTexture(
                new Rect(
                    start.x, start.y,
                    Vector2.Distance(start, end), width),
                Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }

        private static void DrawPointSlider(
            string label,
            ref float value,
            float minimum,
            float maximum,
            string suffix)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                label + ": " + value.ToString("0.0") +
                suffix, GUILayout.Width(180f));
            value = GUILayout.HorizontalSlider(
                value, minimum, maximum);
            GUILayout.EndHorizontal();
        }

        private void DrawPathPointRow(int index)
        {
            CameraPathPoint point = _pathPoints[index];
            bool selected = index == _selectedPathPoint;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(
                    selected,
                    "Point " + (index + 1) +
                    (point.Type == CameraPathPointType.Entity
                        ? " [Entity]"
                        : ""),
                    GUI.skin.button))
                _selectedPathPoint = index;
            GUILayout.Label(
                "Speed " + point.Speed.ToString("0.0") +
                " m/s", GUILayout.Width(85f));

            GUI.enabled = index > 0;
            if (GUILayout.Button("↑", GUILayout.Width(28f)))
            {
                SwapPathPoints(index, index - 1);
                _selectedPathPoint = index - 1;
            }
            GUI.enabled = index < _pathPoints.Count - 1;
            if (GUILayout.Button("↓", GUILayout.Width(28f)))
            {
                SwapPathPoints(index, index + 1);
                _selectedPathPoint = index + 1;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void SwapPathPoints(int first, int second)
        {
            CameraPathPoint temporary = _pathPoints[first];
            _pathPoints[first] = _pathPoints[second];
            _pathPoints[second] = temporary;
            foreach (CameraPathPoint pathPoint in _pathPoints)
            {
                if (pathPoint.NextPoint == first)
                    pathPoint.NextPoint = second;
                else if (pathPoint.NextPoint == second)
                    pathPoint.NextPoint = first;
            }
        }

        private void RemovePathPoint(int index)
        {
            _pathPoints.RemoveAt(index);
            foreach (CameraPathPoint pathPoint in _pathPoints)
            {
                if (pathPoint.NextPoint == index)
                    pathPoint.NextPoint = -1;
                else if (pathPoint.NextPoint > index)
                    pathPoint.NextPoint--;
            }
        }

        private void DeleteSelectedPathPoint()
        {
            if (_selectedPathPoint < 0 ||
                _selectedPathPoint >= _pathPoints.Count)
                return;
            StopPathPlayback();
            int removedIndex = _selectedPathPoint;
            RemovePathPoint(removedIndex);
            _selectedPathPoint = _pathPoints.Count == 0
                ? -1
                : Mathf.Min(
                    removedIndex, _pathPoints.Count - 1);
            _connectionDropdownOpen = false;
        }

        private void DrawPathLabels()
        {
            if (_gameCamera == null ||
                _pathPoints.Count == 0 ||
                (_pathPlaying &&
                 _hidePathElementsDuringPlayback) ||
                _recordingPath)
                return;

            for (int i = 0; i < _pathPoints.Count; i++)
            {
                Vector3 screen = _gameCamera.WorldToScreenPoint(
                    GetPointPosition(_pathPoints[i]) +
                    Vector3.up * 0.45f);
                if (screen.z <= 0f)
                    continue;
                Rect labelRect = new Rect(
                    screen.x - 20f,
                    Screen.height - screen.y - 12f,
                    40f, 24f);
                Color previous = GUI.color;
                GUI.color = i == _selectedPathPoint
                    ? new Color(1f, 0.75f, 0.2f)
                    : new Color(0.3f, 0.85f, 1f);
                GUI.Label(labelRect, (i + 1).ToString());
                GUI.color = previous;
            }
        }

        private void HandleWorldPathEditing()
        {
            if (_gameCamera == null ||
                _pathPoints.Count == 0 ||
                _pathPlaying ||
                _recordingPath)
                return;

            if (Input.GetMouseButtonUp(0))
            {
                _worldDragPoint = -1;
                _worldDragHandle = -1;
            }

            Ray ray = _gameCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));
            FindWorldHandle(
                ray,
                out _worldHoverPoint,
                out _worldHoverHandle);

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                int scrollPoint = _worldDragPoint >= 0
                    ? _worldDragPoint
                    : _worldHoverPoint;
                int scrollHandle = _worldDragPoint >= 0
                    ? _worldDragHandle
                    : _worldHoverHandle;
                if (scrollPoint >= 0)
                {
                    float distance = _worldDragPoint >= 0
                        ? _worldDragDistance
                        : Vector3.Distance(
                            ray.origin,
                            GetWorldHandlePosition(
                                scrollPoint, scrollHandle));
                    float amount = scroll *
                        Mathf.Max(0.15f, distance * 0.04f);
                    if (_worldDragPoint >= 0)
                        _worldDragDistance =
                            Mathf.Max(
                                0.25f,
                                _worldDragDistance + amount);
                    else
                        SetWorldHandlePosition(
                            scrollPoint,
                            scrollHandle,
                            GetWorldHandlePosition(
                                scrollPoint, scrollHandle) +
                            ray.direction * amount);
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (_worldHoverPoint >= 0)
                {
                    _selectedPathPoint = _worldHoverPoint;
                    _worldDragPoint = _worldHoverPoint;
                    _worldDragHandle = _worldHoverHandle;
                    Vector3 handlePosition =
                        GetWorldHandlePosition(
                            _worldDragPoint,
                            _worldDragHandle);
                    _worldDragDistance = Mathf.Max(
                        0.25f,
                        Vector3.Dot(
                            handlePosition - ray.origin,
                            ray.direction));
                    _worldDragOffset =
                        handlePosition -
                        ray.GetPoint(_worldDragDistance);
                }
            }

            if (!Input.GetMouseButton(0) ||
                _worldDragPoint < 0)
                return;
            SetWorldHandlePosition(
                _worldDragPoint,
                _worldDragHandle,
                ray.GetPoint(_worldDragDistance) +
                _worldDragOffset);
        }

        private void FindWorldHandle(
            Ray ray, out int pointIndex, out int handle)
        {
            pointIndex = -1;
            handle = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _pathPoints.Count; i++)
            {
                TestWorldHandle(
                    ray, i, 0,
                    ref bestDistance,
                    ref pointIndex, ref handle);
                TestWorldHandle(
                    ray, i, 3,
                    ref bestDistance,
                    ref pointIndex, ref handle);
                if (GetConnectionTarget(i) >= 0)
                    TestWorldHandle(
                        ray, i, 4,
                        ref bestDistance,
                        ref pointIndex, ref handle);
            }
        }

        private void TestWorldHandle(
            Ray ray, int candidatePoint, int candidateHandle,
            ref float bestDistance,
            ref int pointIndex, ref int handle)
        {
            Vector3 position = GetWorldHandlePosition(
                candidatePoint, candidateHandle);
            Vector3 toHandle = position - ray.origin;
            float alongRay = Vector3.Dot(
                toHandle, ray.direction);
            if (alongRay <= 0f || alongRay >= bestDistance)
                return;
            Vector3 closest =
                ray.origin + ray.direction * alongRay;
            float hitRadius = Mathf.Max(
                0.2f, alongRay * 0.014f);
            if ((position - closest).sqrMagnitude >
                hitRadius * hitRadius)
                return;
            bestDistance = alongRay;
            pointIndex = candidatePoint;
            handle = candidateHandle;
        }

        private Vector3 GetWorldHandlePosition(
            int pointIndex, int handle)
        {
            CameraPathPoint point = _pathPoints[pointIndex];
            if (handle == 4)
                return GetSegmentControlPosition(pointIndex) +
                       Vector3.up * 0.8f;
            if (handle == 3)
                return GetPointLookTarget(point);
            return GetPointPosition(point) + Vector3.up * 0.8f;
        }

        private void SetWorldHandlePosition(
            int pointIndex, int handle, Vector3 position)
        {
            CameraPathPoint point = _pathPoints[pointIndex];
            if (handle == 4)
                SetSegmentControlPosition(
                    pointIndex,
                    position - Vector3.up * 0.8f);
            else if (handle == 3)
                SetPointLookTargetWorld(point, position);
            else
            {
                Vector3 newPointPosition =
                    position - Vector3.up * 0.8f;
                SetPointWorldPosition(
                    point, newPointPosition);
            }
        }

        private Vector3 GetSegmentControlPosition(int segment)
        {
            CameraPathPoint from = _pathPoints[segment];
            int target = GetConnectionTarget(segment);
            CameraPathPoint to = _pathPoints[target];
            Vector3 fromPosition = GetPointPosition(from);
            Vector3 toPosition = GetPointPosition(to);
            Vector3 fromControl =
                fromPosition + from.OutTangent * 1.5f;
            Vector3 toControl =
                toPosition + to.InTangent * 1.5f;
            return (fromControl + toControl) * 0.5f;
        }

        private void SetSegmentControlPosition(
            int segment, Vector3 control)
        {
            CameraPathPoint from = _pathPoints[segment];
            int target = GetConnectionTarget(segment);
            if (target < 0)
                return;
            CameraPathPoint to = _pathPoints[target];
            Vector3 fromPosition = GetPointPosition(from);
            Vector3 toPosition = GetPointPosition(to);
            from.OutTangent =
                (control - fromPosition) * (2f / 3f);
            to.InTangent =
                (control - toPosition) * (2f / 3f);
        }

        private int GetConnectionTarget(int pointIndex)
        {
            if (pointIndex < 0 ||
                pointIndex >= _pathPoints.Count)
                return -1;
            int target = _pathPoints[pointIndex].NextPoint;
            if (target >= 0 &&
                target < _pathPoints.Count &&
                target != pointIndex)
                return target;
            if (_pathLoop.Value && _pathPoints.Count > 1 &&
                pointIndex != 0)
                return 0;
            return -1;
        }

        private void EnsureMenuCursorTexture()
        {
            if (_menuCursorTexture != null)
                return;

            // Unity displays software cursors at native texture size. A
            // fixed 2:3 texture remains consistent on every screen aspect.
            const int width = 14;
            const int height = 21;
            bool[,] fill = new bool[width, height];
            Vector2[] shape =
            {
                new Vector2(1f, 1f),
                new Vector2(1f, 17f),
                new Vector2(5f, 13f),
                new Vector2(8f, 20f),
                new Vector2(11f, 19f),
                new Vector2(7f, 12f),
                new Vector2(13f, 12f)
            };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = false;
                    for (int current = 0, previous = shape.Length - 1;
                         current < shape.Length;
                         previous = current++)
                    {
                        Vector2 a = shape[current];
                        Vector2 b = shape[previous];
                        if ((a.y > y) != (b.y > y) &&
                            x < (b.x - a.x) * (y - a.y) /
                                (b.y - a.y) + a.x)
                            inside = !inside;
                    }
                    fill[x, y] = inside;
                }
            }

            Color32[] pixels = new Color32[width * height];
            Color32 outline = new Color32(8, 10, 14, 255);
            Color32 fillColor =
                new Color32(242, 246, 252, 255);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool filled = fill[x, y];
                    bool bordered = false;
                    if (!filled)
                    {
                        for (int offsetY = -1;
                             offsetY <= 1 && !bordered;
                             offsetY++)
                        {
                            for (int offsetX = -1;
                                 offsetX <= 1;
                                 offsetX++)
                            {
                                int sampleX = x + offsetX;
                                int sampleY = y + offsetY;
                                if (sampleX >= 0 &&
                                    sampleX < width &&
                                    sampleY >= 0 &&
                                    sampleY < height &&
                                    fill[sampleX, sampleY])
                                {
                                    bordered = true;
                                    break;
                                }
                            }
                        }
                    }

                    pixels[
                        (height - 1 - y) * width + x] =
                        filled
                            ? fillColor
                            : bordered
                                ? outline
                                : new Color32(0, 0, 0, 0);
                }
            }

            _menuCursorTexture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false)
            {
                name = "CineKit Menu Cursor",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _menuCursorTexture.SetPixels32(pixels);
            _menuCursorTexture.Apply(false, false);
            _textures.Add(_menuCursorTexture);
        }

        private void SetMenuOpen(bool open)
        {
            if (_menuOpen == open)
            {
                if (open) MaintainMenuCursor();
                return;
            }
            _menuOpen = open;
            if (open)
            {
                _savedCursorLock = Cursor.lockState;
                _savedCursorVisible = Cursor.visible;
                MaintainMenuCursor();
            }
            else
            {
                if (_menuCursorApplied)
                {
                    Cursor.SetCursor(
                        null,
                        Vector2.zero,
                        CursorMode.Auto);
                    _menuCursorApplied = false;
                }
                Cursor.lockState = _freecamEnabled.Value
                    ? CursorLockMode.Locked : _savedCursorLock;
                Cursor.visible = _freecamEnabled.Value
                    ? false : _savedCursorVisible;
                _windowX.Value = _windowRect.x;
                _windowY.Value = _windowRect.y;
            }
        }

        private void MaintainMenuCursor()
        {
            if (!_menuOpen)
                return;
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;

            // Reapply every frame because another open menu can replace or
            // clear Unity's single shared cursor.
            EnsureMenuCursorTexture();
            if (_menuCursorTexture != null)
            {
                Cursor.SetCursor(
                    _menuCursorTexture,
                    Vector2.zero,
                    CursorMode.ForceSoftware);
                _menuCursorApplied = true;
            }
        }

        private void HandleMenuShortcutUpdate()
        {
            KeyboardShortcut shortcut = _menuKey.Value;
            if (shortcut.MainKey == KeyCode.None)
                return;
            if (Input.GetKeyUp(shortcut.MainKey) || !Input.GetKey(shortcut.MainKey))
                _menuShortcutLatched = false;
            if (Input.GetKeyDown(shortcut.MainKey) &&
                AreShortcutModifiersHeld(shortcut))
                ToggleMenuFromShortcut();
        }

        private void HandleMenuShortcutGuiEvent()
        {
            Event current = Event.current;
            KeyboardShortcut shortcut = _menuKey.Value;
            if (current == null || shortcut.MainKey == KeyCode.None)
                return;
            if (current.type == EventType.KeyUp &&
                current.keyCode == shortcut.MainKey)
            {
                _menuShortcutLatched = false;
                return;
            }
            if (current.type == EventType.KeyDown &&
                current.keyCode == shortcut.MainKey &&
                AreShortcutModifiersHeld(shortcut))
            {
                ToggleMenuFromShortcut();
                current.Use();
            }
        }

        private static bool AreShortcutModifiersHeld(KeyboardShortcut shortcut)
        {
            foreach (KeyCode modifier in shortcut.Modifiers)
                if (!Input.GetKey(modifier))
                    return false;
            return true;
        }

        private void ToggleMenuFromShortcut()
        {
            if (_menuShortcutLatched)
                return;
            _menuShortcutLatched = true;
            GUI.FocusControl(null);
            SetMenuOpen(!_menuOpen);
        }

        private void ClampWindowToScreen()
        {
            _windowRect.width = Mathf.Clamp(
                Screen.width - 32f,
                Mathf.Min(680f, Screen.width),
                1000f);
            _windowRect.height = Mathf.Clamp(
                Screen.height - 32f,
                Mathf.Min(520f, Screen.height),
                820f);
            _windowRect.x = Mathf.Clamp(
                _windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(
                _windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
        }

        private void ClampCurveEditorToScreen()
        {
            _curveEditorRect.width = Mathf.Max(
                320f, Mathf.Min(1050f, Screen.width - 24f));
            _curveEditorRect.height = Mathf.Max(
                260f, Mathf.Min(720f, Screen.height - 24f));
            _curveEditorRect.x = Mathf.Clamp(
                _curveEditorRect.x, 0f,
                Mathf.Max(
                    0f, Screen.width - _curveEditorRect.width));
            _curveEditorRect.y = Mathf.Clamp(
                _curveEditorRect.y, 0f,
                Mathf.Max(
                    0f, Screen.height - _curveEditorRect.height));
        }

        private void InstallPatches()
        {
            Patch(AccessTools.Method(typeof(GamePlayerOwner),
                nameof(GamePlayerOwner.TranslateCommand)),
                nameof(BlockGameCommand));
            Patch(AccessTools.Method(typeof(GamePlayerOwner),
                nameof(GamePlayerOwner.TranslateAxes)),
                nameof(BlockGameAxes));
            Patch(
                AccessTools.PropertyGetter(
                    typeof(Koenigz.PerfectCulling.PerfectCullingCamera),
                    nameof(Koenigz.PerfectCulling
                        .PerfectCullingCamera.ObservePosition)),
                nameof(OverrideCullingObservePosition));
            Patch(AccessTools.Method(
                    typeof(EFT.CameraControl.CameraManager),
                    nameof(EFT.CameraControl.CameraManager.ForceSetPosition)),
                nameof(BlockForcedCameraPosition));
            Patch(AccessTools.Method(
                typeof(EFT.CameraControl.PlayerCameraController),
                "LateUpdate"), nameof(ReapplyFreeCameraPose), true);
            Patch(AccessTools.Method(
                    typeof(EFT.EnvironmentEffect.EnvironmentManager),
                    nameof(EFT.EnvironmentEffect.EnvironmentManager
                        .SetTriggerForPlayer)),
                nameof(OverrideLocalPlayerEnvironmentTrigger));
            Patch(
                AccessTools.PropertyGetter(
                    typeof(GPUInstancerManager),
                    nameof(GPUInstancerManager.bGenerateMotionVectors)),
                nameof(DisableVegetationMotionVectors));
            Patch(AccessTools.Method(
                typeof(EFT.CameraControl.CameraLodBiasController),
                "OnPreCull"), nameof(ExtendCameraLodDistance), true);
            Patch(AccessTools.Method(typeof(CullingManager), "Update"),
                nameof(PrepareObjectCullingCamera));
            Patch(AccessTools.Method(
                    typeof(Koenigz.PerfectCulling.EFT
                        .PerfectCullingCrossSceneGroup),
                    "Update"),
                nameof(MaintainCrossSceneMeshGroup), true);
            Patch(AccessTools.PropertySetter(
                    typeof(Koenigz.PerfectCulling.EFT
                        .PerfectCullingCrossSceneContent),
                    nameof(Koenigz.PerfectCulling.EFT
                        .PerfectCullingCrossSceneContent
                        .RuntimeCullingGroup)),
                nameof(RegisterCrossSceneMeshContent), true);
            Patch(AccessTools.PropertyGetter(
                    typeof(DisablerCullingObjectBase),
                    nameof(DisablerCullingObjectBase.HasEntered)),
                nameof(OverrideCullingTriggerState));
            Patch(AccessTools.Method(
                    typeof(DisablerCullingObjectBase),
                    nameof(DisablerCullingObjectBase.Awake)),
                nameof(RegisterLoadedCuller), true);
            Patch(AccessTools.Method(
                    typeof(GPUInstancerDetailManager),
                    nameof(GPUInstancerDetailManager.Awake)),
                nameof(ApplyLoadedGrassDistance), true);
        }

        private void Patch(
            MethodBase original, string patchName, bool postfix = false)
        {
            HarmonyMethod patch = new HarmonyMethod(
                AccessTools.Method(typeof(Plugin), patchName));
            if (postfix)
                _harmony.Patch(original, postfix: patch);
            else
                _harmony.Patch(original, prefix: patch);
        }

        private bool ResolveFieldKitHomeConflict()
        {
            const string fieldKitGuid = "com.hysocs.fieldkit";
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(fieldKitGuid, out info))
                return true;
            if (info.Instance == null)
                return false;

            ConfigEntry<KeyboardShortcut> fieldKitEspKey;
            ConfigDefinition definition =
                new ConfigDefinition("Hotkeys", "Toggle ESP");
            if (!info.Instance.Config.TryGetEntry(
                    definition, out fieldKitEspKey) ||
                fieldKitEspKey.Value.MainKey != KeyCode.Home)
                return true;

            fieldKitEspKey.Value = new KeyboardShortcut(KeyCode.End);
            Logger.LogWarning(
                "FieldKit's ESP hotkey conflicted with CineKit HOME. " +
                "FieldKit Toggle ESP was reassigned to END.");
            return true;
        }

        private bool ShouldBlockPlayerInput =>
            _menuOpen ||
            (IsFreecamActive && !_controlPlayerFromFreecam.Value);

        private bool IsFreecamActive =>
            _freecamEnabled != null &&
            _freecamEnabled.Value &&
            _cameraDetached;

        private Vector3 CurrentCameraPosition =>
            _renderPoseInitialized
                ? _renderPosition
                : _freePosition;

        private static Plugin _instance;

        private static bool BlockGameCommand(ref InputNode.ETranslateResult __result)
        {
            if (_instance == null || !_instance.ShouldBlockPlayerInput)
                return true;
            __result = InputNode.ETranslateResult.BlockAll;
            return false;
        }

        private static bool BlockGameAxes(ref float[] axes)
        {
            if (_instance == null || !_instance.ShouldBlockPlayerInput)
                return true;
            if (axes != null)
                Array.Clear(axes, 0, axes.Length);
            return false;
        }

        private static bool OverrideCullingObservePosition(
            ref Vector3 __result)
        {
            Plugin plugin = _instance;
            if (plugin == null || !plugin.IsFreecamActive)
                return true;
            __result = plugin.CurrentCameraPosition;
            return false;
        }

        private static bool BlockForcedCameraPosition()
        {
            return _instance == null || !_instance.IsFreecamActive;
        }

        private static bool OverrideCullingTriggerState(
            DisablerCullingObjectBase __instance, ref bool __result)
        {
            Plugin plugin = _instance;
            if (plugin == null || !plugin.IsFreecamActive)
                return true;
            __result = plugin.GetFreecamCullingTriggerState(__instance);
            return false;
        }

        private static void RegisterLoadedCuller(
            DisablerCullingObjectBase __instance)
        {
            Plugin plugin = _instance;
            if (plugin != null && plugin.IsFreecamActive)
                plugin.RegisterIndoorCuller(__instance);
        }

        private static void ApplyLoadedGrassDistance(
            GPUInstancerDetailManager __instance)
        {
            Plugin plugin = _instance;
            if (plugin == null || !plugin.IsFreecamActive)
                return;
            plugin.ApplyGrassRenderDistance(__instance);
            plugin.ScheduleGrassSpatialRefresh();
        }

        private static void ReapplyFreeCameraPose()
        {
            if (_instance == null || !_instance.IsFreecamActive)
                return;
            _instance.ApplyFreeCameraPose();
        }

        private static void OverrideLocalPlayerEnvironmentTrigger(
            EFT.IPlayer player,
            ref EFT.EnvironmentEffect.IndoorTrigger trigger)
        {
            Plugin plugin = _instance;
            if (plugin == null ||
                !plugin.IsFreecamActive ||
                player == null ||
                !player.IsYourPlayer)
                return;

            EFT.EnvironmentEffect.EnvironmentManager manager =
                EFT.EnvironmentEffect.EnvironmentManager.Instance;
            if (manager != null)
                trigger = manager.TryFindTriggerByPos(
                    plugin.CurrentCameraPosition);
        }

        private static bool DisableVegetationMotionVectors(
            ref bool __result)
        {
            if (_instance == null || !_instance.IsFreecamActive)
                return true;

            __result = false;
            return false;
        }

        private static void ExtendCameraLodDistance()
        {
            Plugin plugin = _instance;
            if (plugin == null || !plugin.IsFreecamActive)
                return;
            QualitySettings.lodBias *=
                plugin._lodDistanceMultiplier.Value;
        }

        private static void PrepareObjectCullingCamera()
        {
            if (_instance == null || !_instance.IsFreecamActive)
                return;
            _instance.ApplyFreeCameraPose();
        }

        private static void MaintainCrossSceneMeshGroup(
            Koenigz.PerfectCulling.EFT
                .PerfectCullingCrossSceneGroup __instance)
        {
            Plugin plugin = _instance;
            if (plugin == null || !plugin.IsFreecamActive ||
                !plugin._crossSceneGroupCullingStates
                    .ContainsKey(__instance) ||
                IsCrossSceneGroupVisible(__instance))
                return;
            plugin.ForceCrossSceneGroupVisible(__instance);
        }

        private static void RegisterCrossSceneMeshContent(
            Koenigz.PerfectCulling.EFT
                .PerfectCullingCrossSceneContent __instance)
        {
            Plugin plugin = _instance;
            Koenigz.PerfectCulling.EFT
                .PerfectCullingCrossSceneContentMeshes meshes =
                __instance as Koenigz.PerfectCulling.EFT
                    .PerfectCullingCrossSceneContentMeshes;
            if (plugin != null &&
                plugin.IsFreecamActive &&
                meshes != null)
                plugin.AddCrossSceneMeshContent(meshes);
        }


        private void EnsureSkin()
        {
            if (_skinRefreshPending)
            {
                _skinRefreshPending = false;
                DestroySkin();
            }
            if (_skin != null)
                return;

            Color accent;
            if (!ColorUtility.TryParseHtmlString(_accentColor.Value, out accent))
                accent = new Color32(120, 207, 245, 255);
            Color32 window = new Color32(17, 21, 28, 255);
            Color32 surface = new Color32(18, 22, 30, 255);
            Color32 raised = new Color32(24, 29, 39, 255);
            Color32 hover = new Color32(32, 38, 51, 255);
            Color32 border = new Color32(43, 50, 66, 255);
            Color32 text = new Color32(230, 233, 240, 255);
            Color32 muted = new Color32(137, 145, 167, 255);
            Color32 accentColor = accent;
            Color32 accentHover =
                Color.Lerp(accent, Color.white, 0.18f);
            Color32 accentDeep =
                Color.Lerp(accent, Color.black, 0.34f);

            Texture2D windowTexture = MakeSolidTexture(window);
            Texture2D surfaceTexture =
                MakeThemeTexture(surface, border);
            Texture2D raisedTexture =
                MakeThemeTexture(raised, border);
            Texture2D hoverTexture =
                MakeThemeTexture(hover, accentColor);
            Texture2D accentTexture =
                MakeThemeTexture(accentDeep, accentColor);
            Texture2D accentHoverTexture =
                MakeThemeTexture(accentColor, accentHover);
            Texture2D sliderTexture =
                MakeSliderTrackTexture(border);
            Texture2D thumbTexture =
                MakeSliderThumbTexture(accentColor, accentHover);
            Texture2D checkboxTexture =
                MakeCheckboxTexture(raised, border, text, false);
            Texture2D checkboxHoverTexture =
                MakeCheckboxTexture(
                    raised, accentColor, text, false);
            Texture2D checkboxCheckedTexture =
                MakeCheckboxTexture(
                    accentColor, accentColor, Color.white, true);
            Texture2D checkboxCheckedHoverTexture =
                MakeCheckboxTexture(
                    accentHover, accentHover, Color.white, true);

            _skin = Instantiate(GUI.skin);
            _skin.name = "CineKit Skin";

            ConfigureStyle(
                _skin.window,
                windowTexture, windowTexture, windowTexture,
                text, accentColor, accentColor);
            _skin.window.border = new RectOffset(0, 0, 0, 0);
            _skin.window.padding = new RectOffset(14, 14, 34, 14);
            _skin.window.fontSize = 14;
            _skin.window.fontStyle = FontStyle.Bold;
            _skin.window.alignment = TextAnchor.UpperLeft;

            ConfigureStyle(
                _skin.box,
                surfaceTexture, surfaceTexture, surfaceTexture,
                text, text, text);
            _skin.box.border = new RectOffset(4, 4, 4, 4);
            _skin.box.padding = new RectOffset(14, 14, 12, 14);
            _skin.box.margin = new RectOffset(5, 5, 6, 6);

            ConfigureStyle(
                _skin.button,
                raisedTexture, hoverTexture, accentTexture,
                text, accentHover, Color.white);
            _skin.button.border = new RectOffset(4, 4, 4, 4);
            _skin.button.padding = new RectOffset(12, 12, 6, 6);
            _skin.button.margin = new RectOffset(3, 3, 3, 3);
            _skin.button.fixedHeight = 30f;

            _skin.toggle.normal.background = checkboxTexture;
            _skin.toggle.normal.textColor = text;
            _skin.toggle.hover.background = checkboxHoverTexture;
            _skin.toggle.hover.textColor = text;
            _skin.toggle.active.background = checkboxHoverTexture;
            _skin.toggle.active.textColor = text;
            _skin.toggle.focused.background = checkboxTexture;
            _skin.toggle.focused.textColor = text;
            _skin.toggle.onNormal.background = checkboxCheckedTexture;
            _skin.toggle.onNormal.textColor = text;
            _skin.toggle.onHover.background =
                checkboxCheckedHoverTexture;
            _skin.toggle.onHover.textColor = text;
            _skin.toggle.onActive.background =
                checkboxCheckedHoverTexture;
            _skin.toggle.onActive.textColor = text;
            _skin.toggle.onFocused.background =
                checkboxCheckedTexture;
            _skin.toggle.onFocused.textColor = text;
            _skin.toggle.border = new RectOffset(18, 45, 0, 0);
            _skin.toggle.padding = new RectOffset(25, 4, 4, 4);
            _skin.toggle.margin = new RectOffset(2, 2, 2, 2);
            _skin.toggle.fixedHeight = 24f;
            _skin.toggle.alignment = TextAnchor.MiddleLeft;

            _skin.label.normal.textColor = text;
            _skin.label.hover.textColor = text;
            _skin.label.fontSize = 13;
            _skin.label.padding = new RectOffset(3, 3, 2, 2);
            _skin.label.wordWrap = true;

            ConfigureStyle(
                _skin.horizontalSlider,
                sliderTexture, sliderTexture, sliderTexture,
                text, text, text);
            _skin.horizontalSlider.border =
                new RectOffset(8, 8, 8, 8);
            _skin.horizontalSlider.fixedHeight = 18f;
            _skin.horizontalSlider.margin =
                new RectOffset(5, 5, 4, 8);
            _skin.horizontalSlider.padding =
                new RectOffset(0, 0, 0, 0);
            _skin.horizontalSlider.overflow =
                new RectOffset(0, 0, 0, 0);

            ConfigureStyle(
                _skin.horizontalSliderThumb,
                thumbTexture, accentHoverTexture, accentTexture,
                text, text, text);
            _skin.horizontalSliderThumb.border =
                new RectOffset(8, 8, 8, 8);
            _skin.horizontalSliderThumb.fixedWidth = 18f;
            _skin.horizontalSliderThumb.fixedHeight = 18f;
            _skin.horizontalSliderThumb.margin =
                new RectOffset(0, 0, 0, 0);
            _skin.horizontalSliderThumb.padding =
                new RectOffset(0, 0, 0, 0);
            _skin.horizontalSliderThumb.overflow =
                new RectOffset(0, 0, 0, 0);

            ConfigureStyle(
                _skin.scrollView,
                windowTexture, windowTexture, windowTexture,
                text, text, text);
            _skin.scrollView.border = new RectOffset(0, 0, 0, 0);
            _skin.scrollView.padding = new RectOffset(5, 5, 5, 5);

            ConfigureStyle(
                _skin.verticalScrollbar,
                windowTexture, windowTexture, windowTexture,
                muted, muted, muted);
            ConfigureStyle(
                _skin.verticalScrollbarThumb,
                raisedTexture, hoverTexture, accentTexture,
                muted, accentColor, accentColor);
            _skin.verticalScrollbar.fixedWidth = 10f;
            _skin.verticalScrollbarThumb.fixedWidth = 10f;

            _tabStyle = new GUIStyle(_skin.button);
            _tabStyle.fixedHeight = 36f;
            _tabStyle.fontStyle = FontStyle.Bold;
            _tabStyle.normal.background = windowTexture;
            _tabStyle.normal.textColor = muted;
            _tabStyle.hover.background = hoverTexture;
            _tabStyle.hover.textColor = text;
            _tabStyle.onNormal.background = raisedTexture;
            _tabStyle.onNormal.textColor = accentColor;
            _tabStyle.onHover.background = raisedTexture;
            _tabStyle.onHover.textColor = accentHover;
            _tabStyle.onActive.background = accentTexture;
            _tabStyle.onActive.textColor = Color.white;
            _tabStyle.focused.background = windowTexture;
            _tabStyle.focused.textColor = muted;
            _tabStyle.onFocused.background = raisedTexture;
            _tabStyle.onFocused.textColor = accentColor;

            _headingStyle = new GUIStyle(_skin.label);
            _headingStyle.normal.textColor = accentColor;
            _headingStyle.fontStyle = FontStyle.Bold;
            _headingStyle.fontSize = 14;
            _headingStyle.margin = new RectOffset(0, 0, 0, 6);
        }

        private Texture2D MakeThemeTexture(
            Color32 fill, Color32 border)
        {
            const int size = 12;
            Texture2D texture = new Texture2D(
                size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    const int radius = 3;
                    int nearestX = Mathf.Clamp(
                        x, radius, size - radius - 1);
                    int nearestY = Mathf.Clamp(
                        y, radius, size - radius - 1);
                    int deltaX = x - nearestX;
                    int deltaY = y - nearestY;
                    int distanceSquared =
                        deltaX * deltaX + deltaY * deltaY;
                    bool outside = distanceSquared > radius * radius;
                    bool edge = !outside &&
                        (x == 0 || y == 0 ||
                         x == size - 1 || y == size - 1 ||
                         distanceSquared >=
                            (radius - 1) * (radius - 1));
                    pixels[y * size + x] = outside
                        ? new Color32(0, 0, 0, 0)
                        : edge ? border : fill;
                }
            return FinalizeTexture(texture, pixels, FilterMode.Point);
        }

        private Texture2D MakeSolidTexture(Color32 color)
        {
            Texture2D texture = new Texture2D(
                2, 2, TextureFormat.RGBA32, false);
            return FinalizeTexture(texture,
                new[] { color, color, color, color }, FilterMode.Point);
        }

        private Texture2D MakeSliderTrackTexture(Color32 color)
        {
            const int width = 24;
            const int height = 18;
            Texture2D texture = new Texture2D(
                width, height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[width * height];
            for (int y = 7; y <= 10; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = color;
            return FinalizeTexture(texture, pixels, FilterMode.Bilinear);
        }

        private Texture2D MakeSliderThumbTexture(
            Color32 fill, Color32 border)
        {
            const int size = 18;
            const float center = 8.5f;
            Texture2D texture = new Texture2D(
                size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float deltaX = x - center;
                    float deltaY = y - center;
                    float distance = Mathf.Sqrt(
                        deltaX * deltaX + deltaY * deltaY);
                    pixels[y * size + x] =
                        distance > 8f
                            ? new Color32(0, 0, 0, 0)
                            : distance > 6.5f ? border : fill;
                }
            return FinalizeTexture(texture, pixels, FilterMode.Bilinear);
        }

        private Texture2D MakeCheckboxTexture(
            Color32 fill, Color32 border,
            Color32 check, bool isChecked)
        {
            const int width = 64;
            const int height = 22;
            Texture2D texture = new Texture2D(
                width, height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[width * height];
            for (int y = 4; y <= 17; y++)
                for (int x = 2; x <= 15; x++)
                {
                    bool edge =
                        x == 2 || x == 15 || y == 4 || y == 17;
                    pixels[y * width + x] = edge ? border : fill;
            }
            if (isChecked)
            {
                for (int i = 0; i < CheckboxCheckPixels.Length; i += 2)
                    pixels[CheckboxCheckPixels[i + 1] * width +
                        CheckboxCheckPixels[i]] = check;
            }
            return FinalizeTexture(texture, pixels, FilterMode.Point);
        }

        private Texture2D FinalizeTexture(
            Texture2D texture, Color32[] pixels, FilterMode filterMode)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            texture.filterMode = filterMode;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            _textures.Add(texture);
            return texture;
        }

        private static void ConfigureStyle(
            GUIStyle style,
            Texture2D normal,
            Texture2D hover,
            Texture2D active,
            Color normalText,
            Color hoverText,
            Color activeText)
        {
            style.normal.background = normal;
            style.normal.textColor = normalText;
            style.hover.background = hover;
            style.hover.textColor = hoverText;
            style.active.background = active;
            style.active.textColor = activeText;
            style.focused.background = hover;
            style.focused.textColor = hoverText;
            style.onNormal.background = active;
            style.onNormal.textColor = activeText;
            style.onHover.background = active;
            style.onHover.textColor = activeText;
            style.onActive.background = active;
            style.onActive.textColor = activeText;
            style.onFocused.background = active;
            style.onFocused.textColor = activeText;
        }

        private void OnAccentChanged(
            object sender, EventArgs args) =>
            _skinRefreshPending = true;

        private void DestroySkin()
        {
            if (_menuCursorApplied)
            {
                Cursor.SetCursor(
                    null,
                    Vector2.zero,
                    CursorMode.Auto);
                _menuCursorApplied = false;
            }
            if (_skin != null) Destroy(_skin);
            _skin = null;
            _tabStyle = null;
            _headingStyle = null;
            _menuCursorTexture = null;
            foreach (Texture2D texture in _textures)
                if (texture != null) Destroy(texture);
            _textures.Clear();
        }

        private void OnDestroy()
        {
            if (_recordingPath || _ffmpegProcess != null)
                FinishPathRecording(false);
            _freecamEnabled.SettingChanged -= OnFreecamSettingChanged;
            _playerFollowsFreecam.SettingChanged -=
                OnPlayerFollowsFreecamChanged;
            _controlPlayerFromFreecam.SettingChanged -=
                OnControlPlayerFromFreecamChanged;
            _freecamAntialiasing.SettingChanged -=
                OnFreecamAntialiasingChanged;
            _hideRaidHud.SettingChanged -=
                OnHideRaidHudChanged;
            _grassRenderDistance.SettingChanged -=
                OnGrassRenderDistanceChanged;
            _accentColor.SettingChanged -= OnAccentChanged;
            RestoreCamera();
            if (_menuOpen) SetMenuOpen(false);
            if (_harmony != null) _harmony.UnpatchSelf();
            if (_pathMaterial != null)
                Destroy(_pathMaterial);
            _pathMaterial = null;
            _instance = null;
            DestroySkin();
        }
    }
}
