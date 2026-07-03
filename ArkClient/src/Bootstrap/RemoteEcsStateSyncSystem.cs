using Ark.Ecs.Components;
using Ark.Services;
using Friflo.Engine.ECS;

namespace Ark;

/// <summary>
/// 将远程服务态与服务端推送态投影到 ECS，保证 HUD / 调试 UI / 表现层只读 ECS。
/// </summary>
public sealed partial class RemoteEcsStateSyncSystem
{
    private readonly EntityStore _store;

    public RemoteEcsStateSyncSystem(EntityStore store)
    {
        _store = store;
    }

    public void Update()
    {
        if (!GameServices.IsNetworkMode)
            return;

        var localEntity = ResolveLocalPresentationEntity();
        if (localEntity.IsNull)
            return;

        SyncServiceState(localEntity);
    }

    private Entity ResolveLocalPresentationEntity()
    {
        var entityId = GameServices.RemoteWorldEcsCache?.LocalPresentationEntityId ?? 0;
        return entityId > 0 ? _store.GetEntityById(entityId) : default;
    }
}
