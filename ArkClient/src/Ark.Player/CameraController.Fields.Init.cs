using System;
using Godot;
using Ark.Abstractions;

namespace Ark.Player;

public partial class CameraController
{
    [ExportGroup("Input")]
    [Export] public float MouseSensitivity { get; set; } = 0.002f;
    [Export] public float ZoomSpeed { get; set; } = 1.0f;

    [ExportGroup("Third Person")]
    [Export] public float TpsMinPitch { get; set; } = -80.0f;
    [Export] public float TpsMaxPitch { get; set; } = 80.0f;
    [Export] public float TpsDefaultZoom { get; set; } = 4.0f;

    [ExportGroup("Top Down (Build Mode)")]
    [Export] public float TopDownMinPitch { get; set; } = -80.0f;
    [Export] public float TopDownMaxPitch { get; set; } = -10.0f;
    [Export] public float TopDownDefaultZoom { get; set; } = 12.0f;

    [ExportGroup("Smoothing")]
    [Export] public float PositionSmoothing { get; set; } = 10.0f;
    [Export] public float RotationSmoothing { get; set; } = 15.0f;
    [Export] public float ZoomSmoothing { get; set; } = 7.0f;

    private Camera3D? _camera;
    private Node3D? _cameraRig;
    private Node3D? _cameraPivot;
    private Node3D? _cameraArm;

    private ICameraTarget? _currentTarget;
    private CameraMode _currentMode = CameraMode.ThirdPerson;

    private float _targetYaw;
    private float _targetPitch;
    private float _currentYaw;
    private float _currentPitch;
    private float _targetZoom;
    private float _currentZoom;

    private bool _isOrbiting;
    private bool _inputEnabled = true;
    private float _savedYaw;
    private float _savedPitch;

    private Func<float, float, float>? _sampleTerrainHeight;
    private const float CameraTerrainMinOffset = 1.0f;

    public Camera3D? Camera => _camera;
    public ICameraTarget? CurrentTarget => _currentTarget;
    public CameraMode CurrentMode => _currentMode;

    public bool InputEnabled
    {
        get => _inputEnabled;
        set => _inputEnabled = value;
    }

    public void SetTerrainQuery(Func<float, float, float>? sampleHeight)
        => _sampleTerrainHeight = sampleHeight;

    public event Action<ICameraTarget?, ICameraTarget?>? OnTargetChanged;
    public event Action<CameraMode>? OnModeChanged;

    public override void _Ready()
    {
        _cameraRig = new Node3D { Name = "CameraRig" };

        _cameraPivot = new Node3D { Name = "CameraPivot" };
        _cameraRig.AddChild(_cameraPivot);

        _cameraArm = new Node3D { Name = "CameraArm" };
        _cameraArm.Position = new Vector3(0, 1.6f, 0);
        _cameraPivot.AddChild(_cameraArm);

        _camera = new Camera3D
        {
            Name = "MainCamera",
            Fov = 75,
            Near = 0.1f,
            Far = 5000f,
            Current = true,
        };
        _camera.Position = new Vector3(0, 0.5f, TpsDefaultZoom);
        _cameraArm.AddChild(_camera);

        AddChild(_cameraRig);
        _currentZoom = _targetZoom = TpsDefaultZoom;
    }
}
