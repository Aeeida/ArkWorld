using Godot;

namespace Ark.Camera;

/// <summary>
/// 火箭追踪相机控制器 — 发射时锁定跟随火箭 + 右键拖拽视角旋转。
///
/// 功能：
///   • 自动跟随目标 Node3D（火箭体）保持固定偏移
///   • 右键按住拖拽 → 环绕轨道旋转（俯仰 + 偏航）
///   • 滚轮缩放距离
///   • 平滑插值避免剧烈抖动
///   • 可随时启用/禁用
/// </summary>
public partial class RocketCameraController : Node3D
{
    // ═══ 配置 ═══
    private const float DefaultDistance = 30f;
    private const float MinDistance     = 8f;
    private const float MaxDistance     = 200f;
    private const float ZoomStep       = 3f;
    private const float RotateSpeed    = 0.003f;
    private const float SmoothSpeed    = 8f;
    private const float MinPitch       = -80f;
    private const float MaxPitch       = 80f;

    // ═══ 状态 ═══
    private Camera3D? _camera;
    private Node3D?   _target;
    private bool      _active;
    private float     _distance = DefaultDistance;
    private float     _yaw;        // 水平角度 (rad)
    private float     _pitch = 0.3f; // 垂直角度 (rad)
    private bool      _rotating;   // 右键按住
    private Vector3   _currentPos;

    /// <summary>控制器是否激活。</summary>
    public bool Active => _active;

    /// <summary>使用的相机实例。</summary>
    public Camera3D? Camera => _camera;

    public override void _Ready()
    {
        _camera = new Camera3D
        {
            Name = "RocketCamera",
            Near = 0.5f,
            Far = 200000f, // 火箭需要看到星球全貌
        };
        AddChild(_camera);
        _camera.Current = false;
    }

    /// <summary>激活火箭追踪相机，锁定到指定目标。若已锁定同一目标则不重复初始化。</summary>
    public void Activate(Node3D target)
    {
        // 防止每帧重复调用导致状态重置和日志刷屏
        if (_active && _target == target)
            return;

        _target = target;
        _active = true;
        _distance = DefaultDistance;
        _yaw = 0;
        _pitch = 0.3f;
        _rotating = false;

        if (_camera != null)
        {
            _camera.Current = true;
            _currentPos = CalculateDesiredPosition();
            _camera.GlobalPosition = _currentPos;
            _camera.LookAt(target.GlobalPosition, Vector3.Up);
        }
        GD.Print("[RocketCamera] Activated");
    }

    /// <summary>停用火箭追踪相机。</summary>
    public void Deactivate()
    {
        _active = false;
        _rotating = false;
        if (_camera != null) _camera.Current = false;
        _target = null;
        GD.Print("[RocketCamera] Deactivated");
    }

    public override void _Process(double delta)
    {
        if (!_active || _target == null || _camera == null) return;

        // 确保火箭相机始终为活动相机（防止其他系统意外切换）
        if (!_camera.Current)
            _camera.Current = true;

        // 目标节点可能已被释放（火箭坠毁）
        if (!IsInstanceValid(_target))
        {
            Deactivate();
            return;
        }

        float dt = (float)delta;
        var desiredPos = CalculateDesiredPosition();

        // 平滑插值
        _currentPos = _currentPos.Lerp(desiredPos, dt * SmoothSpeed);
        _camera.GlobalPosition = _currentPos;
        _camera.LookAt(_target.GlobalPosition, Vector3.Up);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_active) return;

        // 右键按下/释放
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Right)
            {
                _rotating = mb.Pressed;
                Input.MouseMode = _rotating ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
            }
            // 滚轮缩放
            else if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            {
                _distance = Mathf.Max(MinDistance, _distance - ZoomStep);
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            {
                _distance = Mathf.Min(MaxDistance, _distance + ZoomStep);
            }
        }

        // 右键拖拽旋转
        if (@event is InputEventMouseMotion mm && _rotating)
        {
            _yaw -= mm.Relative.X * RotateSpeed;
            _pitch -= mm.Relative.Y * RotateSpeed;
            _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
        }
    }

    private Vector3 CalculateDesiredPosition()
    {
        if (_target == null) return GlobalPosition;

        var targetPos = _target.GlobalPosition;
        float x = _distance * Mathf.Cos(_pitch) * Mathf.Sin(_yaw);
        float y = _distance * Mathf.Sin(_pitch);
        float z = _distance * Mathf.Cos(_pitch) * Mathf.Cos(_yaw);

        return targetPos + new Vector3(x, y + 5f, z);
    }
}
