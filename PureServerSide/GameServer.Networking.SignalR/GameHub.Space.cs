using System.Buffers.Binary;
using System.Numerics;
using Game.Shared.Core.DTOs;
using Game.Shared.Core.Space;
using GameLayer.Building;
using GameLayer.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking.SignalR;

public sealed partial class GameHub
{
    // ══════════════════════════════════════════════════════════════════
    // 火箭组装/发射/太空飞行器控制（服务端权威）
    // ══════════════════════════════════════════════════════════════════

    public async Task<AssembleRocketResultDto> AssembleRocketAsync(AssembleRocketCommandDto cmd)
    {
        var world = serviceProvider.GetRequiredService<ServerWorldState>();
        var building = serviceProvider.GetRequiredService<BuildingManager>();
        var session = await sessionManager.GetSessionAsync(Context.ConnectionId);
        var worldId = session?.CurrentWorldId ?? "default";

        var launchPad = world.Entities.GetByNetworkId(cmd.LaunchPadEntityId);
        if (launchPad is null)
            return new AssembleRocketResultDto(false, null, "Launch pad not found.");

        if (!building.TryOccupyBuilding(launchPad.Id, cmd.PlayerId))
            return new AssembleRocketResultDto(false, null, "Launch pad is already occupied.");

        var rocketId = System.Guid.NewGuid();
        var rocketPos = launchPad.Position + new Vector3(0, 5f, 0);
        var rocketEntity = world.SpawnEntity(rocketId, worldId, ServerEntityType.Spacecraft, rocketPos, 1000f, "Rocket", 6);
        rocketEntity.FuelPercent = 100f;
        rocketEntity.RemainingDeltaV = 1000f;
        rocketEntity.SpaceFlightPhase = 0;
        building.RegisterRocketLaunchPad(rocketId, launchPad.Id, cmd.PlayerId, cmd.RocketConfigJson);

        var result = new AssembleRocketResultDto(true, rocketId, null);

        if (session?.CurrentWorldId is not null)
        {
            await BroadcastWorldSnapshotAsync(session.CurrentWorldId, world);
            await Clients.Group(session.CurrentWorldId).SendAsync("OnRocketAssembled", result);
        }

        logger.LogInformation("Rocket assembled: RocketId={RocketId}, Player={Player}", rocketId, cmd.PlayerId);
        return result;
    }

    public async Task<LaunchRocketResultDto> LaunchRocketAsync(LaunchRocketCommandDto cmd)
    {
        var world = serviceProvider.GetRequiredService<ServerWorldState>();
        var building = serviceProvider.GetRequiredService<BuildingManager>();
        var rocket = world.Entities.GetByNetworkId(cmd.RocketEntityId);
        if (rocket is null)
            return new LaunchRocketResultDto(false, null, "Rocket not found.");

        var launchDirection = Vector3.UnitY;
        if (RocketEngineForceCatalog.TryGetReactionDirectionFromConfigJson(building.GetRocketConfigJson(cmd.RocketEntityId), out var localReactionDirection))
            launchDirection = Vector3.Transform(localReactionDirection, rocket.Rotation);

        // 发射火箭 — 设置初始速度方向，由引擎反作用力方向决定
        rocket.Velocity = Vector3.Normalize(launchDirection) * 10f;
        rocket.SpaceFlightPhase = 2;
        rocket.Altitude = MathF.Max(0f, rocket.Position.Y);
        rocket.OrbitalVelocity = rocket.Velocity.Length();
        building.ReleaseRocketLaunchPad(cmd.RocketEntityId);

        var result = new LaunchRocketResultDto(true, "Liftoff", null);

        var session = await sessionManager.GetSessionAsync(Context.ConnectionId);
        if (session?.CurrentWorldId is not null)
        {
            await BroadcastWorldSnapshotAsync(session.CurrentWorldId, world);
            await Clients.Group(session.CurrentWorldId).SendAsync("OnRocketLaunched", result);
        }

        logger.LogInformation("Rocket launched: RocketId={RocketId}, Player={Player}", cmd.RocketEntityId, cmd.PlayerId);
        return result;
    }

    public Task<bool> SpacecraftControlAsync(System.Guid playerId, System.Guid spacecraftId, float thrustX, float thrustY, float thrustZ, float rotX, float rotY, float rotZ, byte actionFlags)
    {
        var world = serviceProvider.GetRequiredService<ServerWorldState>();
        var building = serviceProvider.GetRequiredService<BuildingManager>();
        if (!building.CanControlRocket(spacecraftId, playerId))
            return Task.FromResult(false);

        var spacecraft = world.Entities.GetByNetworkId(spacecraftId);
        if (spacecraft is null || spacecraft.Type != ServerEntityType.Spacecraft)
            return Task.FromResult(false);

        var localReactionDirection = Vector3.UnitY;
        RocketEngineForceCatalog.TryGetReactionDirectionFromConfigJson(building.GetRocketConfigJson(spacecraftId), out localReactionDirection);

        // ── 引擎关闭 (bit 2 = 0x04) → 冻结推力和旋转 ──
        bool engineCutoff = (actionFlags & 0x04) != 0;

        if (!engineCutoff)
        {
            // ── 旋转控制 ──
            var rotInput = new Vector3(rotX, rotY, rotZ);
            if (rotInput.LengthSquared() > 0.0001f)
            {
                var yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotInput.Y * 0.03f);
                var pitch = Quaternion.CreateFromAxisAngle(Vector3.UnitX, rotInput.X * 0.03f);
                var roll = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotInput.Z * 0.03f);
                spacecraft.Rotation = Quaternion.Normalize(roll * pitch * yaw * spacecraft.Rotation);
            }

            // ── 推力控制 ──
            if (MathF.Abs(thrustY) > 0.0001f)
            {
                var engineForceDirection = Vector3.Transform(localReactionDirection * MathF.Sign(thrustY), spacecraft.Rotation);
                spacecraft.Velocity += Vector3.Normalize(engineForceDirection) * (MathF.Abs(thrustY) * 5f);
                spacecraft.FuelPercent = MathF.Max(0f, spacecraft.FuelPercent - MathF.Abs(thrustY) * 0.4f);
                spacecraft.RemainingDeltaV = MathF.Max(0f, spacecraft.RemainingDeltaV - MathF.Abs(thrustY) * 4f);
            }

            var maneuverInput = new Vector3(thrustX, 0f, thrustZ);
            if (maneuverInput.LengthSquared() > 0.0001f)
            {
                maneuverInput = Vector3.Normalize(maneuverInput);
                maneuverInput = Vector3.Transform(maneuverInput, spacecraft.Rotation);
                spacecraft.Velocity += maneuverInput * 5f;
                spacecraft.FuelPercent = MathF.Max(0f, spacecraft.FuelPercent - 0.1f);
                spacecraft.RemainingDeltaV = MathF.Max(0f, spacecraft.RemainingDeltaV - 1f);
            }
        }

        // ── 悬停模式 (bit 1 = 0x02) → 速度阻尼到零 ──
        if ((actionFlags & 0x02) != 0)
        {
            spacecraft.Velocity *= 0.85f;
            if (spacecraft.Velocity.LengthSquared() < 0.1f)
                spacecraft.Velocity = Vector3.Zero;
        }

        // ── 分级 (bit 0 = 0x01) → 轻微减速 ──
        if ((actionFlags & 0x01) != 0)
            spacecraft.Velocity *= 0.97f;

        spacecraft.Altitude = MathF.Max(0f, spacecraft.Position.Y);
        spacecraft.OrbitalVelocity = spacecraft.Velocity.Length();
        spacecraft.SpaceFlightPhase = spacecraft.Altitude < 5f && spacecraft.OrbitalVelocity < 1f ? (byte)0
            : spacecraft.Altitude < 50f ? (byte)2
            : spacecraft.Altitude < 10000f ? (byte)3
            : (byte)4;

        return Task.FromResult(true);
    }

    private async Task BroadcastWorldSnapshotAsync(string worldId, ServerWorldState world)
    {
        var fullSnapshot = world.GetFullSnapshot(worldId);
        var frame = new byte[1 + 8 + 4 + fullSnapshot.Length];
        frame[0] = SnapshotPacketId;
        BinaryPrimitives.WriteUInt64LittleEndian(frame.AsSpan(1), world.CurrentTick);
        BinaryPrimitives.WriteSingleLittleEndian(frame.AsSpan(9), world.WorldTime);
        fullSnapshot.CopyTo(frame.AsSpan(13));
        await Clients.Group(worldId).SendAsync("OnServerSnapshot", frame, Context.ConnectionAborted);
    }
}
