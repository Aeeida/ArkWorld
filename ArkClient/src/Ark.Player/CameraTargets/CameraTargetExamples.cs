using Godot;
using Ark.Abstractions;

namespace Ark.Player.CameraTargets;

/// <summary>
/// 载具相机目标 — 用于汽车、飞船、坦克等。
/// </summary>
public partial class VehicleCameraTarget : Node3D, ICameraTarget
{
    [Export] public Vector3 CameraOffset { get; set; } = new(0, 3f, 8f);
    [Export] public float VehicleMinZoom { get; set; } = 3.0f;
    [Export] public float VehicleMaxZoom { get; set; } = 25.0f;
    [Export] public bool AllowRotation { get; set; } = true;

    public Vector3 CameraAnchorPosition => GlobalPosition;
    public Quaternion CameraAnchorRotation => Quaternion;
    public CameraMode PreferredCameraMode => CameraMode.Follow;
    public Vector3 DefaultCameraOffset => CameraOffset;
    public bool CanReceiveInput => true;
    public bool AllowCameraRotation => AllowRotation;
    public float MinZoom => VehicleMinZoom;
    public float MaxZoom => VehicleMaxZoom;

    public void OnCameraAttached() => GD.Print("[Vehicle] Camera attached");
    public void OnCameraDetached() => GD.Print("[Vehicle] Camera detached");
}

/// <summary>
/// 建筑相机目标 — 用于基地、炮塔等静态建筑。
/// </summary>
public partial class BuildingCameraTarget : Node3D, ICameraTarget
{
    [Export] public Vector3 CameraOffset { get; set; } = new(0, 10f, 15f);
    [Export] public float BuildingMinZoom { get; set; } = 5.0f;
    [Export] public float BuildingMaxZoom { get; set; } = 50.0f;

    public Vector3 CameraAnchorPosition => GlobalPosition + new Vector3(0, 2f, 0);
    public Quaternion CameraAnchorRotation => Quaternion.Identity;
    public CameraMode PreferredCameraMode => CameraMode.Orbit;
    public Vector3 DefaultCameraOffset => CameraOffset;
    public bool CanReceiveInput => false; // 建筑不可控制
    public bool AllowCameraRotation => true;
    public float MinZoom => BuildingMinZoom;
    public float MaxZoom => BuildingMaxZoom;

    public void OnCameraAttached() => GD.Print("[Building] Camera attached");
    public void OnCameraDetached() => GD.Print("[Building] Camera detached");
}

/// <summary>
/// 投射物相机目标 — 用于子弹、导弹等。
/// </summary>
public partial class ProjectileCameraTarget : Node3D, ICameraTarget
{
    [Export] public Vector3 CameraOffset { get; set; } = new(0, 1f, 5f);
    [Export] public float ProjectileMinZoom { get; set; } = 2.0f;
    [Export] public float ProjectileMaxZoom { get; set; } = 20.0f;

    public Vector3 CameraAnchorPosition => GlobalPosition;
    public Quaternion CameraAnchorRotation => Quaternion;
    public CameraMode PreferredCameraMode => CameraMode.Follow;
    public Vector3 DefaultCameraOffset => CameraOffset;
    public bool CanReceiveInput => false;
    public bool AllowCameraRotation => false; // 锁定跟随
    public float MinZoom => ProjectileMinZoom;
    public float MaxZoom => ProjectileMaxZoom;

    public void OnCameraAttached() => GD.Print("[Projectile] Camera attached");
    public void OnCameraDetached() => GD.Print("[Projectile] Camera detached");
}

/// <summary>
/// 军团中心相机目标 — 用于观察大量单位的中心位置。
/// </summary>
public partial class FormationCenterTarget : Node3D, ICameraTarget
{
    [Export] public Vector3 CameraOffset { get; set; } = new(0, 30f, 30f);
    [Export] public float FormationMinZoom { get; set; } = 10.0f;
    [Export] public float FormationMaxZoom { get; set; } = 100.0f;

    private Vector3 _centerPosition;
    private int _unitCount;

    public Vector3 CameraAnchorPosition => _centerPosition;
    public Quaternion CameraAnchorRotation => Quaternion.Identity;
    public CameraMode PreferredCameraMode => CameraMode.TopDown;
    public Vector3 DefaultCameraOffset => CameraOffset;
    public bool CanReceiveInput => false;
    public bool AllowCameraRotation => true;
    public float MinZoom => FormationMinZoom;
    public float MaxZoom => FormationMaxZoom;

    /// <summary>
    /// 更新军团中心位置。
    /// </summary>
    public void UpdateCenter(Vector3[] unitPositions)
    {
        if (unitPositions.Length == 0)
        {
            _centerPosition = GlobalPosition;
            _unitCount = 0;
            return;
        }

        Vector3 sum = Vector3.Zero;
        foreach (var pos in unitPositions)
            sum += pos;

        _centerPosition = sum / unitPositions.Length;
        _unitCount = unitPositions.Length;
        GlobalPosition = _centerPosition;
    }

    public void OnCameraAttached() => GD.Print($"[Formation] Camera attached, {_unitCount} units");
    public void OnCameraDetached() => GD.Print("[Formation] Camera detached");
}

/// <summary>
/// 自由相机目标 — 用于观战、编辑器等。
/// </summary>
public partial class FreeCameraTarget : Node3D, IControllableCameraTarget
{
    [Export] public float MoveSpeed { get; set; } = 10.0f;
    [Export] public float FastMoveSpeed { get; set; } = 30.0f;
    [Export] public float FreeMinZoom { get; set; } = 1.0f;
    [Export] public float FreeMaxZoom { get; set; } = 100.0f;

    private Vector3 _position;
    private Vector2 _moveInput;
    private bool _sprint;

    public Vector3 CameraAnchorPosition => _position;
    public Quaternion CameraAnchorRotation => Quaternion.Identity;
    public CameraMode PreferredCameraMode => CameraMode.Free;
    public Vector3 DefaultCameraOffset => Vector3.Zero;
    public bool CanReceiveInput => true;
    public bool AllowCameraRotation => true;
    public float MinZoom => FreeMinZoom;
    public float MaxZoom => FreeMaxZoom;

    public override void _Ready()
    {
        _position = GlobalPosition;
    }

    public override void _Process(double delta)
    {
        if (_moveInput != Vector2.Zero)
        {
            float speed = _sprint ? FastMoveSpeed : MoveSpeed;
            var forward = -GlobalTransform.Basis.Z;
            var right = GlobalTransform.Basis.X;

            _position += (right * _moveInput.X + forward * -_moveInput.Y) * speed * (float)delta;
            GlobalPosition = _position;
        }
    }

    public void ProcessMovementInput(Vector2 input, bool sprint)
    {
        _moveInput = input;
        _sprint = sprint;
    }

    public void ProcessJumpInput()
    {
        // 自由相机上升
        _position.Y += (_sprint ? FastMoveSpeed : MoveSpeed) * 0.1f;
    }

    public void ProcessAimInput(Vector2 delta) { }
    public void ProcessInteractInput() { }
    public void ProcessPrimaryAction(bool pressed) { }
    public void ProcessSecondaryAction(bool pressed) { }

    public void OnCameraAttached() => GD.Print("[FreeCamera] Activated");
    public void OnCameraDetached() => GD.Print("[FreeCamera] Deactivated");
}

/// <summary>
/// 星球相机目标 — 用于观察星球/天体。
/// </summary>
public partial class CelestialCameraTarget : Node3D, ICameraTarget
{
    [Export] public float PlanetRadius { get; set; } = 100f;
    [Export] public Vector3 CameraOffset { get; set; } = new(0, 0, 300f);
    [Export] public float CelestialMinZoom { get; set; } = 50.0f;
    [Export] public float CelestialMaxZoom { get; set; } = 1000.0f;

    public Vector3 CameraAnchorPosition => GlobalPosition;
    public Quaternion CameraAnchorRotation => Quaternion.Identity;
    public CameraMode PreferredCameraMode => CameraMode.Orbit;
    public Vector3 DefaultCameraOffset => CameraOffset;
    public bool CanReceiveInput => false;
    public bool AllowCameraRotation => true;
    public float MinZoom => CelestialMinZoom;
    public float MaxZoom => CelestialMaxZoom;

    public void OnCameraAttached() => GD.Print($"[Celestial] Camera attached, radius={PlanetRadius}");
    public void OnCameraDetached() => GD.Print("[Celestial] Camera detached");
}
