using Godot;

namespace Ark;

/// <summary>
/// 游戏引导器 — Godot Autoload 入口 / Composition Root。
///
/// 职责（仅限粘合层）：
///   1. 按正确顺序实例化全部子系统
///   2. 注入跨模块依赖（事件订阅、工厂委托）
///   3. 每帧按固定顺序驱动子系统 Update
///   4. 负责关机清理
///
/// 规则：
///   • 不包含业务逻辑 / ECS 查询 / Godot 节点构建 / Mesh 工厂
///   • 所有实质代码位于各自的 CSPROJ 模块
///
/// 文件结构（partial class）：
///   • Bootstrap/GameBootstrap.Fields.cs        — 字段声明
///   • Bootstrap/GameBootstrap.Lifecycle.cs      — _Ready / _Process / _ExitTree / _UnhandledKeyInput
///   • Bootstrap/GameBootstrap.Init.cs           — 模块/场景/小队/世界初始化
///   • Bootstrap/GameBootstrap.ModeSwitching.cs  — 玩法模式切换
///   • Bootstrap/GameBootstrap.Events.cs         — 事件回调
///   • Bootstrap/GameBootstrap.Helpers.cs        — HUD/GPU/地形辅助方法
/// </summary>
public partial class GameBootstrap : Node;

