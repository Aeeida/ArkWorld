using Godot;
using System;

namespace Ark.Player;

/// <summary>
/// TPS 相机系统 — 带碰撞检测的第三人称相机。
/// 支持平滑跟随、碰撞推进、视角缩放。
/// </summary>
public partial class TpsCameraRig : Node3D
{
    // ═══ 跟随参数 ═══
    [ExportGroup("Follow")]
    [Export] public NodePath TargetPath { get; set; } = "";
    [Export] public float FollowSpeed { get; set; } = 10.0f;
    [Export] public Vector3 Offset { get; set; } = new Vector3(0, 1.6f, 0);
    
    // ═══ 相机参数 ═══
    [ExportGroup("Camera")]
    [Export] public float DefaultDistance { get; set; } = 4.0f;
    [Export] public float MinDistance { get; set; } = 1.0f;
    [Export] public float MaxDistance { get; set; } = 10.0f;
    [Export] public float ZoomSpeed { get; set; } = 0.5f;
    [Export] public float CollisionMargin { get; set; } = 0.2f;
    
    // ═══ 视角参数 ═══
    [ExportGroup("Look")]
    [Export] public float MouseSensitivity { get; set; } = 0.002f;
    [Export] public float MinPitch { get; set; } = -80.0f;
    [Export] public float MaxPitch { get; set; } = 80.0f;
    
    // ═══ 内部状态 ═══
    private Node3D? _target;
    private Camera3D? _camera;
    private float _currentDistance;
    private float _targetDistance;
    private float _yaw;
    private float _pitch;
    private bool _mouseCaptured = true;
    
    // 碰撞检测
    private RayCast3D? _cameraRay;
    
    public Camera3D? Camera => _camera;
    
    public override void _Ready()
    {
        // 获取目标
        if (!string.IsNullOrEmpty(TargetPath.ToString()))
        {
            _target = GetNodeOrNull<Node3D>(TargetPath);
        }
        
        _currentDistance = DefaultDistance;
        _targetDistance = DefaultDistance;
        
        // 创建相机
        SetupCamera();
        
        // 捕获鼠标
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        GD.Print("[TpsCameraRig] Ready");
    }
    
    private void SetupCamera()
    {
        // 相机
        _camera = new Camera3D
        {
            Name = "TpsCamera",
            Fov = 75,
            Near = 0.1f,
            Far = 1000f
        };
        AddChild(_camera);
        
        // 碰撞射线
        _cameraRay = new RayCast3D
        {
            Name = "CameraRay",
            Enabled = true,
            CollideWithAreas = false,
            CollideWithBodies = true,
            CollisionMask = 1 // 只与环境碰撞
        };
        AddChild(_cameraRay);
    }
    
    public void SetTarget(Node3D target)
    {
        _target = target;
    }
    
    public override void _Input(InputEvent @event)
    {
        if (!_mouseCaptured) return;
        
        // 鼠标移动控制视角
        if (@event is InputEventMouseMotion mouseMotion)
        {
            _yaw -= mouseMotion.Relative.X * MouseSensitivity;
            _pitch -= mouseMotion.Relative.Y * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
        }
        
        // 滚轮缩放
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                _targetDistance = Mathf.Max(_targetDistance - ZoomSpeed, MinDistance);
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                _targetDistance = Mathf.Min(_targetDistance + ZoomSpeed, MaxDistance);
            }
        }
        
        // ESC 切换鼠标
        if (@event.IsActionPressed("ui_cancel"))
        {
            _mouseCaptured = !_mouseCaptured;
            Input.MouseMode = _mouseCaptured ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        }
    }
    
    public override void _PhysicsProcess(double delta)
    {
        if (_target == null || _camera == null) return;
        
        float dt = (float)delta;
        
        // ═══ 1. 计算目标位置 ═══
        Vector3 targetPos = _target.GlobalPosition + Offset;
        
        // 平滑跟随
        GlobalPosition = GlobalPosition.Lerp(targetPos, FollowSpeed * dt);
        
        // ═══ 2. 计算相机位置 ═══
        // 基于 yaw/pitch 计算相机方向
        Vector3 cameraDir = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch)
        ).Normalized();
        
        // 平滑缩放
        _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, 10 * dt);
        
        // ═══ 3. 碰撞检测 ═══
        float finalDistance = _currentDistance;
        if (_cameraRay != null)
        {
            _cameraRay.GlobalPosition = GlobalPosition;
            _cameraRay.TargetPosition = cameraDir * (_currentDistance + CollisionMargin);
            _cameraRay.ForceRaycastUpdate();
            
            if (_cameraRay.IsColliding())
            {
                float hitDistance = GlobalPosition.DistanceTo(_cameraRay.GetCollisionPoint());
                finalDistance = Mathf.Max(hitDistance - CollisionMargin, MinDistance);
            }
        }
        
        // ═══ 4. 应用相机位置和朝向 ═══
        Vector3 cameraPos = GlobalPosition + cameraDir * finalDistance;
        _camera.GlobalPosition = cameraPos;
        _camera.LookAt(GlobalPosition, Vector3.Up);
    }
    
    /// <summary>
    /// 获取相机朝向的世界方向。
    /// </summary>
    public Vector3 GetAimDirection()
    {
        if (_camera == null) return Vector3.Forward;
        return -_camera.GlobalTransform.Basis.Z;
    }
    
    /// <summary>
    /// 获取相机水平朝向（忽略俯仰）。
    /// </summary>
    public Vector3 GetHorizontalDirection()
    {
        return new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw)).Normalized();
    }
    
    /// <summary>
    /// 设置初始朝向。
    /// </summary>
    public void SetLookDirection(float yaw, float pitch = 0)
    {
        _yaw = yaw;
        _pitch = Mathf.Clamp(pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
    }
}
