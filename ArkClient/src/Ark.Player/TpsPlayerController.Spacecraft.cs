using Godot;
using Ark.Ecs.Components;

namespace Ark.Player;

public partial class TpsPlayerController
{
    private float _spacecraftThrottle = 1f;
    private bool _spacecraftHoverMode;
    private bool _spacecraftEngineCutoff;

    public bool IsSpacecraftControlActive => TryGetProjectedLocalControlState(out var controlState)
        ? (LocalControlSource)controlState.ControlSource == LocalControlSource.SpacecraftRemote
        : ResolveRuntimeLocalControlMode() == LocalControlMode.Spacecraft;
    public float SpacecraftThrottle => TryGetProjectedLocalControlState(out var controlState)
        ? controlState.SpacecraftThrottle
        : _spacecraftThrottle;
    public bool SpacecraftHoverMode => TryGetProjectedLocalControlState(out var controlState)
        ? controlState.HoverMode != 0
        : _spacecraftHoverMode;
    public bool SpacecraftEngineCutoff => TryGetProjectedLocalControlState(out var controlState)
        ? controlState.EngineCutoff != 0
        : _spacecraftEngineCutoff;

    public void BeginSpacecraftControl()
    {
        _spacecraftThrottle = 1f;
        _spacecraftHoverMode = false;
        _spacecraftEngineCutoff = false;
        _externalControlLocked = true;
        _isFiring = false;
        _velocity = Vector3.Zero;
        Velocity = Vector3.Zero;
        SyncLocalControlStateToEcs(
            LocalControlMode.Spacecraft,
            ResolveControlledSpacecraftSnapshotEntityId(),
            controlSourceOverride: LocalControlSource.SpacecraftRemote,
            externalControlLockedOverride: true,
            activeNetworkIdOverride: ResolveSpacecraftActiveNetworkId());
    }

    public void EndSpacecraftControl()
    {
        var fallbackMode = ResolveControlledVehicleSnapshotEntityId() > 0
            ? LocalControlMode.Vehicle
            : LocalControlMode.Character;
        _spacecraftThrottle = 0f;
        _spacecraftHoverMode = false;
        _spacecraftEngineCutoff = true;
        _externalControlLocked = false;
        SyncLocalControlStateToEcs(
            fallbackMode,
            fallbackMode == LocalControlMode.Vehicle ? ResolveControlledVehicleSnapshotEntityId() : 0,
            controlSourceOverride: fallbackMode == LocalControlMode.Vehicle ? LocalControlSource.VehicleSeat : LocalControlSource.CharacterDirect,
            externalControlLockedOverride: false,
            activeNetworkIdOverride: System.Guid.Empty);
    }

    public void SetSpacecraftThrottle(float throttle)
    {
        _spacecraftThrottle = Mathf.Clamp(throttle, 0f, 1f);
        if (_spacecraftThrottle > 0f)
            _spacecraftEngineCutoff = false;
        SyncLocalControlStateToEcs();
    }

    public void ReadSpacecraftControlInput(float dt, out Vector3 thrust, out Vector3 rotation, out byte actionFlags)
    {
        thrust = Vector3.Zero;
        rotation = Vector3.Zero;

        var moveInput = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        rotation.X = -moveInput.Y;
        rotation.Y = moveInput.X;
        rotation.Z = Input.GetAxis("spacecraft_roll_left", "spacecraft_roll_right");

        if (Input.IsActionPressed("spacecraft_throttle_up"))
        {
            _spacecraftThrottle = Mathf.Clamp(_spacecraftThrottle + dt * 0.5f, 0f, 1f);
            _spacecraftEngineCutoff = false;
            _spacecraftHoverMode = false;
        }
        if (Input.IsActionPressed("spacecraft_throttle_down"))
        {
            _spacecraftThrottle = Mathf.Clamp(_spacecraftThrottle - dt * 0.5f, 0f, 1f);
            _spacecraftHoverMode = false;
        }

        if (Input.IsActionJustPressed("spacecraft_hover"))
        {
            _spacecraftHoverMode = !_spacecraftHoverMode;
            if (_spacecraftHoverMode)
                _spacecraftEngineCutoff = false;
        }

        if (Input.IsActionJustPressed("spacecraft_engine_cutoff"))
        {
            _spacecraftEngineCutoff = true;
            _spacecraftHoverMode = false;
            _spacecraftThrottle = 0f;
        }

        thrust.Y = _spacecraftThrottle;

        actionFlags = 0;
        if (Input.IsActionJustPressed("reload"))
            actionFlags |= 0x01;
        if (_spacecraftHoverMode)
            actionFlags |= 0x02;
        if (_spacecraftEngineCutoff)
            actionFlags |= 0x04;

        SyncLocalControlStateToEcs();
    }

    public void SyncLocalControlStateToEcs(
        LocalControlMode? modeOverride = null,
        int? controlledSnapshotEntityIdOverride = null,
        LocalControlSource? controlSourceOverride = null,
        bool? externalControlLockedOverride = null,
        System.Guid? activeNetworkIdOverride = null)
    {
        if (_entity.Id == 0)
            return;

        var mode = modeOverride ?? ResolveRuntimeLocalControlMode();
        int controlledSnapshotEntityId = controlledSnapshotEntityIdOverride ?? ResolveRuntimeControlledSnapshotEntityId();
        var controlSource = controlSourceOverride ?? ResolveRuntimeLocalControlSource(mode, controlledSnapshotEntityId);
        bool externalControlLocked = externalControlLockedOverride ?? ResolveRuntimeExternalControlLocked();
        System.Guid activeNetworkId = activeNetworkIdOverride ?? ResolveRuntimeActiveControlNetworkId(mode);

        NormalizeProjectedControlState(ref mode, ref controlSource, ref controlledSnapshotEntityId, ref activeNetworkId);
        ResolveRuntimeVehicleSeatInfo(controlledSnapshotEntityId, controlSource, out var seatIndex, out var seatType);

        _ecsAuth?.Write(_entity, new LocalControlState
        {
            ControlledSnapshotEntityId = controlledSnapshotEntityId,
            ActiveNetworkId = activeNetworkId,
            SpacecraftThrottle = _spacecraftThrottle,
            Mode = (byte)mode,
            ControlSource = (byte)controlSource,
            SeatIndex = seatIndex,
            SeatType = seatType,
            ExternalControlLocked = externalControlLocked ? (byte)1 : (byte)0,
            BuildMode = _buildCameraMode ? (byte)1 : (byte)0,
            MouseCaptured = _mouseCaptured ? (byte)1 : (byte)0,
            HoverMode = _spacecraftHoverMode ? (byte)1 : (byte)0,
            EngineCutoff = _spacecraftEngineCutoff ? (byte)1 : (byte)0,
            InVehicle = controlSource == LocalControlSource.VehicleSeat ? (byte)1 : (byte)0,
        });
    }

    private static void NormalizeProjectedControlState(
        ref LocalControlMode mode,
        ref LocalControlSource controlSource,
        ref int controlledSnapshotEntityId,
        ref System.Guid activeNetworkId)
    {
        if (controlSource == LocalControlSource.SpacecraftRemote)
        {
            mode = LocalControlMode.Spacecraft;
            controlledSnapshotEntityId = 0;
            return;
        }

        activeNetworkId = System.Guid.Empty;
        if (controlSource == LocalControlSource.VehicleSeat)
        {
            mode = LocalControlMode.Vehicle;
            if (controlledSnapshotEntityId <= 0)
                controlSource = LocalControlSource.CharacterDirect;
            return;
        }

        controlSource = LocalControlSource.CharacterDirect;
        mode = LocalControlMode.Character;
        controlledSnapshotEntityId = 0;
    }

    private LocalControlSource ResolveEffectiveLocalControlSource()
    {
        return TryGetProjectedLocalControlState(out var controlState)
            ? (LocalControlSource)controlState.ControlSource
            : ResolveRuntimeLocalControlSource();
    }

    private LocalControlMode ResolveEffectiveLocalControlMode()
    {
        return TryGetProjectedLocalControlState(out var controlState)
            ? (LocalControlMode)controlState.Mode
            : ResolveRuntimeLocalControlMode();
    }

    private int ResolveEffectiveControlledSnapshotEntityId()
    {
        return TryGetProjectedLocalControlState(out var controlState) && controlState.ControlledSnapshotEntityId > 0
            ? controlState.ControlledSnapshotEntityId
            : ResolveRuntimeControlledSnapshotEntityId();
    }

    private int ResolveEffectiveVehicleEntityId()
    {
        return ResolveEffectiveLocalControlSource() == LocalControlSource.VehicleSeat
            ? ResolveEffectiveControlledSnapshotEntityId()
            : 0;
    }

    private bool IsVehicleControlActive()
    {
        return ResolveEffectiveLocalControlSource() == LocalControlSource.VehicleSeat;
    }

    private bool TryGetProjectedLocalControlState(out LocalControlState controlState)
    {
        if (_entity.Id != 0 && _entity.TryGetComponent<LocalControlState>(out controlState))
            return true;

        controlState = default;
        return false;
    }

    private LocalControlMode ResolveRuntimeLocalControlMode()
    {
        if (_entity.Id != 0
            && _entity.TryGetComponent<RemoteRocketControlState>(out var rocketControlState)
            && rocketControlState.HasActiveRocket != 0
            && ResolveRuntimeExternalControlLocked())
        {
            return LocalControlMode.Spacecraft;
        }

        return ResolveControlledVehicleSnapshotEntityId() > 0 ? LocalControlMode.Vehicle : LocalControlMode.Character;
    }

    private bool ResolveRuntimeExternalControlLocked()
    {
        if (_entity.Id != 0 && _entity.TryGetComponent<LocalControlState>(out var localControlState))
            return localControlState.ExternalControlLocked != 0;

        return _externalControlLocked;
    }

    private LocalControlSource ResolveRuntimeLocalControlSource(LocalControlMode? modeOverride = null, int? controlledSnapshotEntityIdOverride = null)
    {
        var mode = modeOverride ?? ResolveRuntimeLocalControlMode();
        int controlledSnapshotEntityId = controlledSnapshotEntityIdOverride ?? ResolveRuntimeControlledSnapshotEntityId();
        return mode switch
        {
            LocalControlMode.Spacecraft => LocalControlSource.SpacecraftRemote,
            LocalControlMode.Vehicle when controlledSnapshotEntityId > 0 => LocalControlSource.VehicleSeat,
            _ => LocalControlSource.CharacterDirect,
        };
    }

    private int ResolveRuntimeControlledSnapshotEntityId()
    {
        return ResolveRuntimeLocalControlMode() switch
        {
            LocalControlMode.Vehicle => ResolveControlledVehicleSnapshotEntityId(),
            LocalControlMode.Spacecraft => ResolveControlledSpacecraftSnapshotEntityId(),
            _ => 0,
        };
    }

    [Ark.Analyzers.Attributes.ControlAuthorityResolver]
    private int ResolveControlledVehicleSnapshotEntityId()
    {
        if (_entity.Id != 0)
        {
            if (_entity.TryGetComponent<VehicleSeat>(out var vehicleSeat)
                && vehicleSeat.VehicleEntityId > 0)
                return vehicleSeat.VehicleEntityId;

            if (_entity.TryGetComponent<RemoteVehicleOccupantState>(out var vehicleOccupantState)
                && vehicleOccupantState.SnapshotVehicleEntityId > 0)
                return vehicleOccupantState.SnapshotVehicleEntityId;
        }

        return 0;
    }

    private void ResolveRuntimeVehicleSeatInfo(int controlledSnapshotEntityId, LocalControlSource controlSource, out byte seatIndex, out byte seatType)
    {
        seatIndex = 0;
        seatType = 0;
        if (controlSource != LocalControlSource.VehicleSeat || controlledSnapshotEntityId <= 0 || _entity.Id == 0)
            return;

        if (_entity.TryGetComponent<VehicleSeat>(out var vehicleSeat)
            && vehicleSeat.VehicleEntityId == controlledSnapshotEntityId)
        {
            seatIndex = vehicleSeat.SeatIndex;
            seatType = vehicleSeat.SeatType;
            return;
        }

        if (_entity.TryGetComponent<RemoteVehicleOccupantState>(out var vehicleOccupantState)
            && vehicleOccupantState.SnapshotVehicleEntityId == controlledSnapshotEntityId)
        {
            seatIndex = (byte)Mathf.Clamp(vehicleOccupantState.CurrentSeatIndex, 0, byte.MaxValue);
            seatType = vehicleOccupantState.CurrentSeatType;
        }
    }

    [Ark.Analyzers.Attributes.ControlAuthorityResolver]
    private int ResolveControlledSpacecraftSnapshotEntityId()
    {
        return _entity.Id != 0 && _entity.TryGetComponent<RemoteRocketControlState>(out var rocketControlState)
            ? rocketControlState.SnapshotSpacecraftEntityId
            : 0;
    }

    private System.Guid ResolveSpacecraftActiveNetworkId()
    {
        return _entity.Id != 0
            && _entity.TryGetComponent<RemoteRocketControlState>(out var rocketControlState)
            ? rocketControlState.ActiveRocketNetworkId
            : System.Guid.Empty;
    }

    private System.Guid ResolveRuntimeActiveControlNetworkId(LocalControlMode? modeOverride = null)
    {
        return (modeOverride ?? ResolveRuntimeLocalControlMode()) == LocalControlMode.Spacecraft
            ? ResolveSpacecraftActiveNetworkId()
            : System.Guid.Empty;
    }
}
