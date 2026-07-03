using Godot;
using Ark.Abstractions;

namespace Ark.World;

/// <summary>
/// 世界环境管理器 — 具体实现拆分到多个 partial 文件。
/// </summary>
public sealed partial class WorldEnvironmentManager : Node3D, ITerrainQuery, IWorldInitializer
{
    // IWorldInitializer 的实现在 WorldEnvironmentManager.PublicAPI.cs 中
}
