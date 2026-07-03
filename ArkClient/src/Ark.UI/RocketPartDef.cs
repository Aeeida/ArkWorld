namespace Ark.UI;

/// <summary>
/// 火箭配件定义 — 单个可安装部件，含完整物理属性。
/// </summary>
public sealed class RocketPartDef
{
    public int    PartId    { get; init; }
    public string Name      { get; init; } = "";
    public string Icon      { get; init; } = "";
    public string Category  { get; init; } = "";   // Capsule, FuelTank, Engine, Booster, Structure, Aero, Utility, Science
    public float  Mass      { get; init; }          // 干质量 (吨)
    public float  Thrust    { get; init; }          // 额定推力 kN (仅引擎/助推器)
    public float  ISP       { get; init; }          // 比冲 (秒) — 燃料效率
    public float  FuelCapacity { get; init; }       // 燃料容量 (单位)
    public float  DragCoefficient { get; init; }    // 空气阻力系数 (越大阻力越大)
    public int    CrewCapacity { get; init; }        // 乘员容量
    public int    CargoSlots   { get; init; }        // 物资槽位
    public float  HeatTolerance { get; init; }       // 耐热温度 (K)
    public bool   Decoupler    { get; init; }        // 是否为分离器
    public string Description  { get; init; } = "";

    /// <summary>引擎有效排气速度 (m/s) = ISP × 9.81</summary>
    public float ExhaustVelocity => ISP * 9.81f;

    /// <summary>满载质量（干质量 + 燃料质量，燃料按 5kg/单位 估算）</summary>
    public float WetMass => Mass + FuelCapacity * 0.005f;

    // ═══════════════════════════════════════════════════════════════════════
    //                          完整配件目录
    // ═══════════════════════════════════════════════════════════════════════

    public static readonly RocketPartDef[] All =
    {
        // ── 指令舱 ──
        new() { PartId = 1,  Name = "指令舱 MK1",      Icon = "\U0001f6f0", Category = "Capsule",
                Mass = 0.8f, CrewCapacity = 1, CargoSlots = 2, DragCoefficient = 0.2f,
                HeatTolerance = 2400f, Description = "单人指令舱，含隔热罩与降落伞" },
        new() { PartId = 2,  Name = "指令舱 MK2",      Icon = "\U0001f6f0", Category = "Capsule",
                Mass = 2.5f, CrewCapacity = 3, CargoSlots = 6, DragCoefficient = 0.25f,
                HeatTolerance = 2800f, Description = "三人大型指令舱，适合深空任务" },

        // ── 燃料箱 ──
        new() { PartId = 10, Name = "液体燃料箱 S",     Icon = "\U0001f6e2", Category = "FuelTank",
                Mass = 0.25f, FuelCapacity = 200f, DragCoefficient = 0.1f,
                HeatTolerance = 2000f, Description = "小型液体燃料箱 (LF+Ox)" },
        new() { PartId = 11, Name = "液体燃料箱 M",     Icon = "\U0001f6e2", Category = "FuelTank",
                Mass = 0.56f, FuelCapacity = 500f, DragCoefficient = 0.1f,
                HeatTolerance = 2000f, Description = "中型液体燃料箱" },
        new() { PartId = 12, Name = "液体燃料箱 L",     Icon = "\U0001f6e2", Category = "FuelTank",
                Mass = 1.5f, FuelCapacity = 1200f, DragCoefficient = 0.12f,
                HeatTolerance = 2000f, Description = "大型液体燃料箱" },
        new() { PartId = 13, Name = "液体燃料箱 XL",    Icon = "\U0001f6e2", Category = "FuelTank",
                Mass = 4.0f, FuelCapacity = 3600f, DragCoefficient = 0.15f,
                HeatTolerance = 2000f, Description = "超大型液体燃料箱，重型运载必备" },

        // ── 液体火箭发动机 ──
        new() { PartId = 20, Name = "发动机 RE-1",      Icon = "\U0001f525", Category = "Engine",
                Mass = 0.5f, Thrust = 200f, ISP = 290f, DragCoefficient = 0.05f,
                HeatTolerance = 3500f, Description = "基础液体火箭发动机，适合上面级" },
        new() { PartId = 21, Name = "发动机 RE-3",      Icon = "\U0001f525", Category = "Engine",
                Mass = 1.5f, Thrust = 600f, ISP = 310f, DragCoefficient = 0.08f,
                HeatTolerance = 3500f, Description = "高推力液体引擎，中等效率" },
        new() { PartId = 22, Name = "发动机 RE-7 矢量",  Icon = "\U0001f525", Category = "Engine",
                Mass = 3.0f, Thrust = 1400f, ISP = 305f, DragCoefficient = 0.1f,
                HeatTolerance = 3500f, Description = "重型矢量推力引擎，可偏转喷口" },
        new() { PartId = 23, Name = "真空发动机 VE-1",   Icon = "\U0001f525", Category = "Engine",
                Mass = 0.3f, Thrust = 60f, ISP = 345f, DragCoefficient = 0.03f,
                HeatTolerance = 3200f, Description = "低推力高比冲真空引擎" },

        // ── 固体助推器 ──
        new() { PartId = 30, Name = "固体助推器 SRB-S",  Icon = "\U0001f680", Category = "Booster",
                Mass = 1.0f, Thrust = 300f, ISP = 230f, FuelCapacity = 300f, DragCoefficient = 0.15f,
                HeatTolerance = 2800f, Description = "小型固体助推器，可抛弃" },
        new() { PartId = 31, Name = "固体助推器 SRB-L",  Icon = "\U0001f680", Category = "Booster",
                Mass = 3.5f, Thrust = 900f, ISP = 235f, FuelCapacity = 1000f, DragCoefficient = 0.18f,
                HeatTolerance = 2800f, Description = "大型固体助推器，起飞增推" },

        // ── 结构件 ──
        new() { PartId = 40, Name = "分离器 TR-1",       Icon = "\u2702", Category = "Structure",
                Mass = 0.05f, Decoupler = true, DragCoefficient = 0.02f,
                HeatTolerance = 2000f, Description = "级间分离器，实现分级飞行" },
        new() { PartId = 41, Name = "支撑架",            Icon = "\U0001f9f1", Category = "Structure",
                Mass = 0.02f, DragCoefficient = 0.01f,
                HeatTolerance = 2000f, Description = "径向支撑架，用于侧挂助推器" },

        // ── 空气动力学 ──
        new() { PartId = 50, Name = "整流罩",            Icon = "\U0001f6e1", Category = "Aero",
                Mass = 0.15f, DragCoefficient = -0.3f, // 负值 = 减少阻力
                HeatTolerance = 2400f, Description = "载荷整流罩，显著降低空气阻力" },
        new() { PartId = 51, Name = "尾翼组",            Icon = "\U0001f53a", Category = "Aero",
                Mass = 0.04f, DragCoefficient = 0.05f,
                HeatTolerance = 2200f, Description = "稳定尾翼，增强飞行稳定性" },
        new() { PartId = 52, Name = "隔热底板",          Icon = "\U0001f525", Category = "Aero",
                Mass = 0.3f, DragCoefficient = 0.4f,
                HeatTolerance = 3500f, Description = "再入隔热板，保护返回舱" },

        // ── 功能件 ──
        new() { PartId = 60, Name = "太阳能板",          Icon = "\u2600", Category = "Utility",
                Mass = 0.05f, DragCoefficient = 0.02f,
                HeatTolerance = 1200f, Description = "可展开太阳能板，提供持续电力" },
        new() { PartId = 61, Name = "降落伞",            Icon = "\U0001fa82", Category = "Utility",
                Mass = 0.1f, DragCoefficient = 0.0f,
                HeatTolerance = 1500f, Description = "大型降落伞，安全着陆必备" },
        new() { PartId = 62, Name = "RCS 推进器",        Icon = "\U0001f4a8", Category = "Utility",
                Mass = 0.03f, Thrust = 2f, ISP = 240f, FuelCapacity = 20f, DragCoefficient = 0.01f,
                HeatTolerance = 2000f, Description = "姿态控制推进器（四向）" },
        new() { PartId = 63, Name = "对接口",            Icon = "\U0001f517", Category = "Utility",
                Mass = 0.08f, DragCoefficient = 0.03f,
                HeatTolerance = 2000f, Description = "标准对接口，用于轨道对接" },
        new() { PartId = 64, Name = "蓄电池组",          Icon = "\U0001f50b", Category = "Utility",
                Mass = 0.02f, DragCoefficient = 0.01f,
                HeatTolerance = 2000f, Description = "大容量蓄电池" },

        // ── 科学载荷 ──
        new() { PartId = 70, Name = "科学仪器包",        Icon = "\U0001f52c", Category = "Science",
                Mass = 0.05f, DragCoefficient = 0.02f,
                HeatTolerance = 1800f, Description = "温度/气压/引力传感器组" },
        new() { PartId = 71, Name = "通信天线",          Icon = "\U0001f4e1", Category = "Science",
                Mass = 0.01f, DragCoefficient = 0.01f,
                HeatTolerance = 1200f, Description = "远程通信天线，传输科学数据" },
    };

    /// <summary>所有配件分类。</summary>
    public static readonly string[] Categories =
        { "Capsule", "FuelTank", "Engine", "Booster", "Structure", "Aero", "Utility", "Science" };

    /// <summary>分类的中文显示名称。</summary>
    public static string CategoryDisplayName(string cat) => cat switch
    {
        "Capsule"   => "\U0001f6f0 指令舱",
        "FuelTank"  => "\U0001f6e2 燃料系统",
        "Engine"    => "\U0001f525 发动机",
        "Booster"   => "\U0001f680 助推器",
        "Structure" => "\U0001f9f1 结构件",
        "Aero"      => "\U0001f4a8 气动部件",
        "Utility"   => "\U0001f527 功能件",
        "Science"   => "\U0001f52c 科学载荷",
        _ => cat
    };

    public static RocketPartDef? Get(int partId)
    {
        foreach (var p in All)
            if (p.PartId == partId) return p;
        return null;
    }

    /// <summary>获取指定分类的所有配件。</summary>
    public static List<RocketPartDef> GetByCategory(string category)
    {
        var list = new List<RocketPartDef>();
        foreach (var p in All)
            if (p.Category == category) list.Add(p);
        return list;
    }
}

/// <summary>
/// 火箭级段 — 一组配件构成一级（含分离器分界）。
/// </summary>
public sealed class RocketStage
{
    public int StageIndex { get; set; }
    public List<int> PartIds { get; } = new();

    public float DryMass   { get { float m = 0; foreach (var id in PartIds) { var p = RocketPartDef.Get(id); if (p != null) m += p.Mass; } return m; } }
    public float FuelMass  { get { float m = 0; foreach (var id in PartIds) { var p = RocketPartDef.Get(id); if (p != null) m += p.FuelCapacity * 0.005f; } return m; } }
    public float WetMass   => DryMass + FuelMass;
    public float Thrust    { get { float t = 0; foreach (var id in PartIds) { var p = RocketPartDef.Get(id); if (p != null) t += p.Thrust; } return t; } }
    public float Fuel      { get { float f = 0; foreach (var id in PartIds) { var p = RocketPartDef.Get(id); if (p != null) f += p.FuelCapacity; } return f; } }

    public float AverageISP
    {
        get
        {
            float totalThrust = 0, weightedISP = 0;
            foreach (var id in PartIds)
            {
                var p = RocketPartDef.Get(id);
                if (p != null && p.Thrust > 0 && p.ISP > 0)
                {
                    totalThrust += p.Thrust;
                    weightedISP += p.Thrust * p.ISP;
                }
            }
            return totalThrust > 0 ? weightedISP / totalThrust : 0;
        }
    }

    /// <summary>齐奥尔科夫斯基火箭方程计算本级 ΔV (m/s)。payloadMass = 上方所有级的总质量。</summary>
    public float DeltaV(float payloadMass)
    {
        float isp = AverageISP;
        if (isp <= 0 || WetMass <= 0) return 0;
        float m0 = WetMass + payloadMass;    // 初始质量（含载荷）
        float m1 = DryMass + payloadMass;    // 燃尽质量
        if (m1 <= 0) return 0;
        return isp * 9.81f * MathF.Log(m0 / m1);
    }
}

/// <summary>
/// 火箭配置 — 完整的多级火箭设计。
/// </summary>
public sealed class RocketConfig
{
    /// <summary>所有已安装配件 ID（按安装顺序）。</summary>
    public List<int> InstalledPartIds { get; } = new();

    /// <summary>级段列表（底部=索引0为第一级/主推进，顶部=最后一级）。</summary>
    public List<RocketStage> Stages { get; } = new();

    /// <summary>火箭名称。</summary>
    public string VesselName { get; set; } = "新火箭 #1";

    // ─── 汇总属性 ───

    public float TotalDryMass
    {
        get { float m = 0; foreach (var id in InstalledPartIds) { var p = RocketPartDef.Get(id); if (p != null) m += p.Mass; } return m; }
    }
    public float TotalFuelMass
    {
        get { float m = 0; foreach (var id in InstalledPartIds) { var p = RocketPartDef.Get(id); if (p != null) m += p.FuelCapacity * 0.005f; } return m; }
    }
    public float TotalMass => TotalDryMass + TotalFuelMass;
    public float TotalThrust
    {
        get { float t = 0; foreach (var id in InstalledPartIds) { var p = RocketPartDef.Get(id); if (p != null) t += p.Thrust; } return t; }
    }
    public float TotalFuel
    {
        get { float f = 0; foreach (var id in InstalledPartIds) { var p = RocketPartDef.Get(id); if (p != null) f += p.FuelCapacity; } return f; }
    }
    public float TotalDragCoefficient
    {
        get { float d = 0; foreach (var id in InstalledPartIds) { var p = RocketPartDef.Get(id); if (p != null) d += p.DragCoefficient; } return MathF.Max(d, 0.01f); }
    }

    /// <summary>推重比（地面）。</summary>
    public float ThrustToWeightRatio => TotalMass > 0 ? TotalThrust / (TotalMass * 9.81f) : 0;

    /// <summary>总 ΔV (m/s) — 各级 ΔV 之和。</summary>
    public float TotalDeltaV
    {
        get
        {
            if (Stages.Count == 0) RebuildStages();
            float total = 0;
            float payloadAbove = 0;
            // 从顶级(最后)到底级(第一级)
            for (int i = Stages.Count - 1; i >= 0; i--)
            {
                total += Stages[i].DeltaV(payloadAbove);
                payloadAbove += Stages[i].WetMass;
            }
            return total;
        }
    }

    public int TotalCrewCapacity
    {
        get { int c = 0; foreach (var id in InstalledPartIds) { var p = RocketPartDef.Get(id); if (p != null) c += p.CrewCapacity; } return c; }
    }
    public int TotalCargoSlots
    {
        get { int c = 0; foreach (var id in InstalledPartIds) { var p = RocketPartDef.Get(id); if (p != null) c += p.CargoSlots; } return c; }
    }

    // ─── 发射就绪检查 ───

    public bool HasCapsule  => InstalledPartIds.Exists(id => RocketPartDef.Get(id)?.Category == "Capsule");
    public bool HasEngine   => InstalledPartIds.Exists(id =>
    {
        var c = RocketPartDef.Get(id)?.Category;
        return c == "Engine" || c == "Booster";
    });
    public bool HasFuel          => TotalFuel > 0;
    public bool HasParachute     => InstalledPartIds.Exists(id => RocketPartDef.Get(id)?.PartId == 61);
    public bool IsLaunchReady    => HasCapsule && HasEngine && HasFuel && ThrustToWeightRatio > 1.0f;

    public Godot.Vector3 GetPrimaryReactionDirection()
    {
        var weighted = Godot.Vector3.Zero;
        float totalThrust = 0f;

        foreach (var id in InstalledPartIds)
        {
            var part = RocketPartDef.Get(id);
            if (part == null)
                continue;

            if (part.Category is not ("Engine" or "Booster"))
                continue;

            weighted += Godot.Vector3.Up * part.Thrust;
            totalThrust += part.Thrust;
        }

        if (totalThrust <= 0f || weighted.LengthSquared() <= 1e-6f)
            return Godot.Vector3.Up;

        return (weighted / totalThrust).Normalized();
    }

    /// <summary>发射就绪问题列表。</summary>
    public List<string> GetIssues()
    {
        var issues = new List<string>();
        if (!HasCapsule) issues.Add("缺少指令舱");
        if (!HasEngine)  issues.Add("缺少发动机/助推器");
        if (!HasFuel)    issues.Add("缺少燃料");
        if (ThrustToWeightRatio <= 1.0f && HasEngine)
            issues.Add($"推重比不足 ({ThrustToWeightRatio:F2} ≤ 1.0)");
        if (!HasParachute) issues.Add("警告：无降落伞（无法安全着陆）");
        return issues;
    }

    /// <summary>根据分离器自动重建级段。</summary>
    public void RebuildStages()
    {
        Stages.Clear();
        var current = new RocketStage { StageIndex = 0 };

        foreach (var id in InstalledPartIds)
        {
            var part = RocketPartDef.Get(id);
            if (part == null) continue;

            if (part.Decoupler && current.PartIds.Count > 0)
            {
                Stages.Add(current);
                current = new RocketStage { StageIndex = Stages.Count };
            }
            current.PartIds.Add(id);
        }
        if (current.PartIds.Count > 0)
            Stages.Add(current);
    }

    /// <summary>获取每个配件的详细属性文本（用于鼠标悬浮提示）。</summary>
    public static string GetPartTooltip(RocketPartDef part)
    {
        var lines = new List<string>
        {
            part.Description,
            $"类别: {RocketPartDef.CategoryDisplayName(part.Category)}",
            $"干质量: {part.Mass:F2}t  |  满载: {part.WetMass:F2}t",
        };
        if (part.Thrust > 0)  lines.Add($"推力: {part.Thrust:F0}kN  |  比冲: {part.ISP:F0}s");
        if (part.FuelCapacity > 0) lines.Add($"燃料: {part.FuelCapacity:F0}单位");
        if (part.CrewCapacity > 0) lines.Add($"乘员: {part.CrewCapacity}人");
        if (part.CargoSlots > 0)   lines.Add($"货仓: {part.CargoSlots}槽");
        lines.Add($"阻力系数: {part.DragCoefficient:F2}  |  耐热: {part.HeatTolerance:F0}K");
        if (part.Decoupler) lines.Add("✂ 分离器 — 分级分界点");
        return string.Join("\n", lines);
    }
}
