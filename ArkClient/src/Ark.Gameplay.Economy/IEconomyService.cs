namespace Ark.Gameplay.Economy;

/// <summary>
/// 经济系统接口 — 资源管理、交易、生产链。
/// TODO: 实现资源类型注册、产出/消耗管线、交易系统。
/// </summary>
public interface IEconomyService
{
    /// <summary>获取玩家当前拥有的资源数量。</summary>
    int GetResource(int resourceTypeId);

    /// <summary>尝试消耗资源，成功返回 true。</summary>
    bool TryConsume(int resourceTypeId, int amount);

    /// <summary>增加资源。</summary>
    void AddResource(int resourceTypeId, int amount);
}

/// <summary>资源类型。</summary>
public enum ResourceType : byte
{
    Metal,
    Crystal,
    Fuel,
    Food,
    Credits,
}
