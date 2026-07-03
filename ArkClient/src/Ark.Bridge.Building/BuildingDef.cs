using Godot;

namespace Ark.Bridge.Features.BaseBuilding;

/// <summary>
/// 建筑类型静态定义 — 视觉尺寸、颜色、建造时间等。
/// </summary>
public static class BuildingDef
{
    public readonly struct Def
    {
        public readonly int    TypeId;
        public readonly string Name;
        public readonly string Icon;        // Unicode emoji，用于 UI 按钮
        public readonly Vector3 Size;       // 建筑视觉尺寸 (X, Y, Z)
        public readonly Vector3 FootprintHalfSize; // 地基半尺寸 (X, Z)，用于碰撞检测
        public readonly Color   BodyColor;
        public readonly Color   RoofColor;
        public readonly float   BuildTime;  // 建造秒数
        public readonly string  Description;

        public Def(int typeId, string name, string icon,
                   Vector3 size, Color bodyColor, Color roofColor,
                   float buildTime, string description)
        {
            TypeId           = typeId;
            Name             = name;
            Icon             = icon;
            Size             = size;
            FootprintHalfSize = new Vector3(size.X * 0.5f, 0, size.Z * 0.5f);
            BodyColor        = bodyColor;
            RoofColor        = roofColor;
            BuildTime        = buildTime;
            Description      = description;
        }
    }

    // ═══ 6 种建筑类型定义 ═══
    public static readonly Def[] All =
    {
        new(1, "Wall",       "🧱",
            new Vector3(4f, 2.5f, 0.5f),
            new Color(0.72f, 0.70f, 0.65f),
            new Color(0.50f, 0.48f, 0.44f),
            buildTime: 3f,
            "防御围墙，保护基地边界"),

        new(2, "Tower",      "🗼",
            new Vector3(3f, 7f, 3f),
            new Color(0.55f, 0.52f, 0.45f),
            new Color(0.35f, 0.30f, 0.25f),
            buildTime: 8f,
            "瞭望塔，提供视野和防御"),

        new(3, "Storage",    "🏪",
            new Vector3(6f, 3f, 5f),
            new Color(0.60f, 0.45f, 0.28f),
            new Color(0.65f, 0.22f, 0.18f),
            buildTime: 6f,
            "仓库，增加资源储量"),

        new(4, "Barracks",   "⚔️",
            new Vector3(7f, 4f, 6f),
            new Color(0.42f, 0.42f, 0.48f),
            new Color(0.25f, 0.35f, 0.55f),
            buildTime: 12f,
            "兵营，训练战斗单位"),

        new(5, "Rocket Pad", "🚀",
            new Vector3(10f, 1.5f, 10f),
            new Color(0.30f, 0.30f, 0.32f),
            new Color(0.80f, 0.55f, 0.15f),
            buildTime: 20f,
            "火箭发射台，通往太空"),

        new(6, "Tank Factory", "🏭",
            new Vector3(12f, 5f, 10f),
            new Color(0.38f, 0.40f, 0.36f),
            new Color(0.28f, 0.30f, 0.26f),
            buildTime: 15f,
            "坦克工厂，生产各类载具"),
    };

    public static Def? Get(int typeId)
    {
        foreach (var d in All)
            if (d.TypeId == typeId) return d;
        return null;
    }

    /// <summary>建造中的 ghost 颜色（可放置 = 绿色半透明，不可放置 = 红色）</summary>
    public static Color GhostValid   => new(0.3f, 1.0f, 0.3f, 0.45f);
    public static Color GhostInvalid => new(1.0f, 0.2f, 0.2f, 0.45f);

    /// <summary>地基颜色（已建造区域的地面贴片）</summary>
    public static Color FoundationColor => new(0.50f, 0.48f, 0.42f, 1f);

    /// <summary>建造进度条颜色</summary>
    public static Color ProgressBarBg   => new(0.15f, 0.15f, 0.15f, 0.8f);
    public static Color ProgressBarFill => new(0.30f, 0.80f, 0.30f, 0.9f);

    /// <summary>最小建筑间距（圆形碰撞检测半径之和）</summary>
    public const float MinSpacing = 2.0f;
}
