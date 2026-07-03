using System;
using Friflo.Engine.ECS;
using Godot;
using Ark.Services;
using Ark.Services.Remote;

namespace Ark.Bridge.Player;

/// <summary>
/// <see cref="RemotePlayerBridge"/> 创建工厂。
/// 由 GameBootstrap 调用以替代旧的 <c>GameServices.CreateRemotePlayerBridge</c>，
/// 把 Node3D 创建移出 <c>Ark.Services</c>，保持服务层不再创建场景节点。
/// </summary>
public static class RemotePlayerBridgeFactory
{
    public static RemotePlayerBridge? Create(
        EntityStore store,
        RemoteWorldEcsCacheSystem? remoteWorldEcsCache,
        Action<int, System.Numerics.Vector3, Quaternion, int, byte>? spawnTypedBuilding = null,
        Action<int>? removeBuilding = null,
        Action<int, Node3D, byte>? attachWeapon = null,
        Action<int>? detachWeapon = null)
    {
        if (!GameServices.IsNetworkMode)
        {
            GD.PrintErr("[RemotePlayerBridgeFactory] Cannot create: not in network mode");
            return null;
        }

        var bridge = new RemotePlayerBridge();
        bridge.Initialize(
            store,
            remoteWorldEcsCache,
            GameServices.RemotePlayerId,
            spawnTypedBuilding,
            removeBuilding,
            attachWeapon,
            detachWeapon);
        return bridge;
    }
}
