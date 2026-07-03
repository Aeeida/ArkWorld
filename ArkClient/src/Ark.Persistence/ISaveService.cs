namespace Ark.Persistence;

/// <summary>
/// 持久化服务接口 — 存档/读档、ECS 状态序列化。
/// TODO: 实现 JSON/Binary 序列化、自动存档、存档槽管理。
/// </summary>
public interface ISaveService
{
    /// <summary>保存当前游戏状态到指定槽位。</summary>
    bool Save(int slotIndex);

    /// <summary>从指定槽位加载游戏状态。</summary>
    bool Load(int slotIndex);

    /// <summary>获取所有存档槽位的元信息。</summary>
    SaveSlotInfo[] GetSlots();

    /// <summary>删除指定槽位。</summary>
    bool Delete(int slotIndex);
}

/// <summary>存档槽位元信息。</summary>
public record struct SaveSlotInfo(
    int SlotIndex,
    string DisplayName,
    DateTime SaveTime,
    bool IsEmpty
);
