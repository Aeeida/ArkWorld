using Godot;

namespace Ark.Player;

public partial class CameraController
{
    private void UpdateCameraTransform(float dt)
    {
        if (_cameraRig == null || _cameraPivot == null || _cameraArm == null || _camera == null)
            return;

        _currentYaw = Mathf.LerpAngle(_currentYaw, _targetYaw, RotationSmoothing * dt);
        _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, RotationSmoothing * dt);
        _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, ZoomSmoothing * dt);

        if (_currentTarget != null)
        {
            _cameraRig.GlobalPosition = _cameraRig.GlobalPosition.Lerp(
                _currentTarget.CameraAnchorPosition,
                PositionSmoothing * dt);
        }

        _cameraPivot.Rotation = new Vector3(0, _currentYaw, 0);
        _cameraArm.Rotation = new Vector3(_currentPitch, 0, 0);

        var offset = _currentTarget?.DefaultCameraOffset ?? new Vector3(0, 0.5f, 4f);
        _camera.Position = new Vector3(0, offset.Y, _currentZoom);

        if (_sampleTerrainHeight != null)
        {
            var camGlobalPos = _camera.GlobalPosition;
            float terrainY = _sampleTerrainHeight(camGlobalPos.X, camGlobalPos.Z);
            float minCamY = terrainY + CameraTerrainMinOffset;
            if (camGlobalPos.Y < minCamY)
            {
                float correction = minCamY - camGlobalPos.Y;
                _camera.Position = new Vector3(_camera.Position.X, _camera.Position.Y + correction, _camera.Position.Z);
            }
        }
    }

    public Vector3 GetForwardDirection()
    {
        if (_camera == null) return Vector3.Forward;
        return -_camera.GlobalTransform.Basis.Z;
    }

    public Vector3 GetHorizontalForward()
    {
        var forward = GetForwardDirection();
        forward.Y = 0;
        return forward.Normalized();
    }

    public float GetYaw() => _currentYaw;

    public void SetYaw(float yaw)
    {
        _targetYaw = _currentYaw = yaw;
    }
}
