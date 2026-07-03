using System.Buffers.Binary;
using Game.Shared.Core.DTOs;
using GameLayer.Combat;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking.SignalR;

public sealed partial class GameHub
{
    // ══════════════════════════════════════════════════════════════════
    // 世界 — 登录 / 角色 / 加入 / 环境 / 查询
    // ══════════════════════════════════════════════════════════════════

    public async Task Authenticate(System.Guid playerId)
    {
        await ReplaceSessionAsync(playerId);
        await Clients.Caller.SendAsync("Authenticated", playerId);
        logger.LogInformation("Player {PlayerId} authenticated on {ConnectionId}", playerId, Context.ConnectionId);
    }

    public async Task<LoginResultDto> LoginAsync(LoginCommandDto cmd)
    {
        var result = await InvokeApiAsync<LoginResultDto>(api => api.LoginAsync(cmd, Context.ConnectionAborted));

        if (result.Success && result.Player is not null)
        {
            await ReplaceSessionAsync(result.Player.Id);
            await Clients.Caller.SendAsync("OnServerMessage", $"登录成功：{result.Player.Name}");
        }

        return result;
    }

    public async Task<LogoutResultDto> LogoutAsync(LogoutCommandDto cmd)
    {
        var result = await InvokeApiAsync<LogoutResultDto>(api => api.LogoutAsync(cmd, Context.ConnectionAborted));

        if (result.Success)
        {
            await sessionManager.RemoveSessionAsync(Context.ConnectionId);
            await Clients.Caller.SendAsync("OnServerMessage", "已登出");
        }

        return result;
    }

    public async Task<CharacterListDto> GetCharacterListAsync(System.Guid accountId)
    {
        var result = await InvokeApiAsync<CharacterListDto>(api => api.GetCharacterListAsync(accountId, Context.ConnectionAborted));
        await Clients.Caller.SendAsync("OnCharacterListReceived", result);
        return result;
    }

    public async Task<CharacterCreateFullResultDto> CreateCharacterFullAsync(CreateCharacterFullCommandDto cmd)
    {
        var result = await InvokeApiAsync<CharacterCreateFullResultDto>(api => api.CreateCharacterFullAsync(cmd, Context.ConnectionAborted));
        await Clients.Caller.SendAsync("OnCharacterCreated", result);
        return result;
    }

    public async Task<SelectCharacterResultDto> SelectCharacterAsync(SelectCharacterCommandDto cmd)
    {
        var result = await InvokeApiAsync<SelectCharacterResultDto>(api => api.SelectCharacterAsync(cmd, Context.ConnectionAborted));

        if (result.Success && result.CharacterId.HasValue)
        {
            await ReplaceSessionAsync(result.CharacterId.Value);
            await Clients.Caller.SendAsync("OnServerMessage", $"角色已选择：{result.Player?.Name ?? result.CharacterId.Value.ToString()} ");
        }

        await Clients.Caller.SendAsync("OnCharacterSelected", result);
        return result;
    }

    public async Task<WorldEnvironmentDto> GetWorldEnvironmentAsync(string worldId)
    {
        var result = await InvokeApiAsync<WorldEnvironmentDto>(api => api.GetWorldEnvironmentAsync(worldId, Context.ConnectionAborted));
        await Clients.Caller.SendAsync("OnWorldEnvironmentReceived", result);

        if (result.TerrainModifications.Count > 0)
        {
            await Clients.Caller.SendAsync("OnTerrainModificationsReceived", result.TerrainModifications);
        }

        return result;
    }

    public async Task<JoinWorldResultDto> JoinWorldAsync(JoinWorldCommandDto cmd)
    {
        var result = await InvokeApiAsync<JoinWorldResultDto>(api => api.JoinWorldAsync(cmd, Context.ConnectionAborted));

        logger.LogInformation(
            "╔══ JoinWorldAsync result: Success={Success}, WorldId={WorldId}, Seed={Seed}, Spawn=({X:F1},{Y:F1},{Z:F1}), Online={Online}",
            result.Success, result.WorldId, result.TerrainSeed, result.SpawnX, result.SpawnY, result.SpawnZ, result.OnlinePlayerCount);

        if (result.Success && !string.IsNullOrWhiteSpace(result.WorldId))
        {
            await worldPopulation.EnsureWorldPopulatedAsync(result.WorldId, Context.ConnectionAborted);

            // Add to SignalR group for world-wide broadcasts (chat, events, etc.)
            await Groups.AddToGroupAsync(Context.ConnectionId, result.WorldId);

            // Spawn player entity in server-authoritative world state so WorldTickService
            // includes them in zone snapshots.
            try
            {
                var world = serviceProvider.GetService<GameLayer.Core.ServerWorldState>();
                if (world is null)
                {
                    logger.LogError("╠══ ❌ ServerWorldState is NULL — entity will NOT be spawned!");
                }
                else
                {
                    var spawnPos = new System.Numerics.Vector3(result.SpawnX, result.SpawnY, result.SpawnZ);
                    var existing = world.Entities.GetByNetworkId(cmd.PlayerId);

                    logger.LogInformation("╠══ Before spawn: EntityCount={Count}, PlayerAlreadyExists={Exists}",
                        world.Entities.Count, existing is not null);

                    if (existing is null)
                    {
                        var entity = world.SpawnPlayer(cmd.PlayerId, result.WorldId, cmd.PlayerId.ToString("N")[..8], spawnPos);
                        var weapons = serviceProvider.GetRequiredService<WeaponRegistry>();
                        weapons.TryApplyLoadout(entity, 20);
                        logger.LogInformation(
                            "╠══ ✅ SPAWNED entity: Id={EntityId}, NetworkId={PlayerId}, Zone={Zone}, Pos=({X:F1},{Y:F1},{Z:F1})",
                            entity.Id, cmd.PlayerId, entity.ZoneId, spawnPos.X, spawnPos.Y, spawnPos.Z);
                    }
                    else
                    {
                        world.UpdateEntityPosition(cmd.PlayerId, spawnPos);
                        if (existing.PersonalWeaponDefId <= 0)
                        {
                            var weapons = serviceProvider.GetRequiredService<WeaponRegistry>();
                            weapons.TryApplyLoadout(existing, 20);
                        }
                        logger.LogInformation("╠══ Updated existing entity position: Id={EntityId}, Zone={Zone}",
                            existing.Id, existing.ZoneId);
                    }

                    logger.LogInformation("╠══ After spawn: EntityCount={Count}", world.Entities.Count);

                    // Dump all entities for debugging
                    foreach (var e in world.Entities.GetAll())
                    {
                        logger.LogInformation("╠══   Entity[{Id}] NetworkId={NetworkId}, Type={Type}, Zone={Zone}, Pos=({X:F1},{Y:F1},{Z:F1})",
                            e.Id, e.NetworkId, e.Type, e.ZoneId, e.Position.X, e.Position.Y, e.Position.Z);
                    }

                    // Set session zone
                    var playerEntity = world.Entities.GetByNetworkId(cmd.PlayerId);
                    var session = await sessionManager.GetSessionAsync(Context.ConnectionId);

                    logger.LogInformation("╠══ Session: ConnectionId={ConnId}, SessionFound={Found}, EntityZone={Zone}, SessionZone={SessZone}, TcpBound={TcpBound}",
                        Context.ConnectionId,
                        session is not null,
                        playerEntity?.ZoneId,
                        session?.CurrentZoneId,
                        session?.TcpConnectionId is not null);

                    if (session is not null && playerEntity?.ZoneId is not null)
                    {
                        session.CurrentWorldId = result.WorldId;
                        session.CurrentZoneId = playerEntity.ZoneId;

                        // If TCP is already bound, also register in the TCP zone
                        if (session.TcpConnectionId is not null)
                        {
                            var tcp = serviceProvider.GetService<GameServer.Networking.Transport.TcpTransportServer>();
                            tcp?.JoinZone(session.TcpConnectionId, playerEntity.ZoneId);
                            logger.LogInformation("╠══ TCP zone bound: TcpId={TcpId}, Zone={Zone}",
                                session.TcpConnectionId, playerEntity.ZoneId);
                        }
                    }

                    var fullSnapshot = world.GetFullSnapshot(result.WorldId);
                    var frame = new byte[1 + 8 + 4 + fullSnapshot.Length];
                    frame[0] = SnapshotPacketId;
                    BinaryPrimitives.WriteUInt64LittleEndian(frame.AsSpan(1), world.CurrentTick);
                    BinaryPrimitives.WriteSingleLittleEndian(frame.AsSpan(9), world.WorldTime);
                    fullSnapshot.CopyTo(frame.AsSpan(13));

                    await Clients.Group(result.WorldId).SendAsync("OnServerSnapshot", frame, Context.ConnectionAborted);
                    logger.LogInformation("╠══ Broadcasted full snapshot to world group {WorldId}: entities={Count}, bytes={Bytes}",
                        result.WorldId, world.Entities.Count, frame.Length);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "╠══ ❌ EXCEPTION spawning entity!");
            }
        }

        logger.LogInformation("╚══ JoinWorldAsync complete for player {PlayerId}", cmd.PlayerId);
        return result;
    }

    public async Task<IReadOnlyList<TerrainModificationDto>> GetTerrainModificationsAsync(string worldId, string zoneId)
    {
        return await InvokeApiAsync<IReadOnlyList<TerrainModificationDto>>(api => api.GetTerrainModificationsAsync(worldId, zoneId, Context.ConnectionAborted));
    }

    public async Task<PartyInfoDto> GetPartyInfoAsync(System.Guid playerId)
    {
        var result = await InvokeApiAsync<PartyInfoDto>(api => api.GetPartyInfoAsync(playerId, Context.ConnectionAborted));
        await Clients.Caller.SendAsync("OnPartyUpdated", result);
        return result;
    }

    public async Task<NearbyEntitiesDto> GetNearbyEntitiesAsync(System.Guid playerId, float radius)
    {
        var result = await InvokeApiAsync<NearbyEntitiesDto>(api => api.GetNearbyEntitiesAsync(playerId, radius, Context.ConnectionAborted));
        await Clients.Caller.SendAsync("OnNearbyEntitiesUpdated", result);
        return result;
    }

    // ── 库存 ──────────────────────────────────────────────────────────

    public async Task<InventoryDto> GetInventoryAsync(System.Guid playerId)
    {
        return await InvokeApiAsync<InventoryDto>(api => api.GetInventoryAsync(playerId, Context.ConnectionAborted));
    }

    // ── 任务 ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<QuestDto>> GetActiveQuestsAsync(System.Guid playerId)
    {
        return await InvokeApiAsync<IReadOnlyList<QuestDto>>(api => api.GetActiveQuestsAsync(playerId, Context.ConnectionAborted));
    }

    // ── 制造 ──────────────────────────────────────────────────────────

    public async Task<CraftingQueueDto> GetCraftingQueueAsync(System.Guid playerId)
    {
        return await InvokeApiAsync<CraftingQueueDto>(api => api.GetCraftingQueueAsync(playerId, Context.ConnectionAborted));
    }

    // ── 主权 ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SovereigntyDto>> GetSovereigntyMapAsync()
    {
        return await InvokeApiAsync<IReadOnlyList<SovereigntyDto>>(api => api.GetSovereigntyMapAsync(Context.ConnectionAborted));
    }

    // ── 邮件 ──────────────────────────────────────────────────────────

    public async Task<GetMailDto> GetMailAsync(System.Guid playerId)
    {
        return await InvokeApiAsync<GetMailDto>(api => api.GetMailAsync(playerId, Context.ConnectionAborted));
    }
}
