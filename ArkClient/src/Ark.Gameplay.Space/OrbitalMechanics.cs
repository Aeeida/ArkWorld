using System.Numerics;
using Ark.Configuration;
using Ark.Shared.Data;

namespace Ark.Gameplay.Space;

// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║    轨道力学计算器 — 纯函数，KSP 式 Vis-Viva / 开普勒轨道计算                   ║
// ║    零 Godot 依赖，全部使用 System.Numerics                                    ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

/// <summary>
/// 轨道力学纯函数工具集。
/// 使用 Vis-Viva 方程和开普勒定律计算轨道参数。
/// </summary>
public static class OrbitalMechanics
{
    /// <summary>
    /// Vis-Viva 方程：v = sqrt(μ * (2/r - 1/a))
    /// 计算在给定高度上的轨道速度。
    /// </summary>
    /// <param name="altitude">距地表高度 (m)</param>
    /// <param name="semiMajorAxis">半长轴 (m)，从星球中心量起</param>
    public static float OrbitalSpeed(float altitude, float semiMajorAxis)
    {
        var cfg = GameConfig.Current.Space;
        float r = cfg.PlanetRadius + altitude;
        float val = cfg.PlanetMu * (2f / r - 1f / semiMajorAxis);
        return val > 0 ? MathF.Sqrt(val) : 0f;
    }

    /// <summary>
    /// 计算圆轨道速度：v = sqrt(μ/r)
    /// </summary>
    public static float CircularSpeed(float altitude)
    {
        var cfg = GameConfig.Current.Space;
        float r = cfg.PlanetRadius + altitude;
        return MathF.Sqrt(cfg.PlanetMu / r);
    }

    /// <summary>
    /// 从远拱点和近拱点计算轨道参数。
    /// </summary>
    public static OrbitalParams FromApsides(float apoapsisAlt, float periapsisAlt)
    {
        float R = GameConfig.Current.Space.PlanetRadius;
        float ra = R + apoapsisAlt;
        float rp = R + periapsisAlt;
        float a = (ra + rp) / 2f;
        float e = (ra - rp) / (ra + rp);
        return new OrbitalParams(a, e, 0f, apoapsisAlt, periapsisAlt);
    }

    /// <summary>
    /// 判断轨道是否稳定（近圆轨道且在最低高度以上）。
    /// </summary>
    public static bool IsStableOrbit(OrbitalParams p)
    {
        var cfg = GameConfig.Current.Space;
        return p.Eccentricity < cfg.StableOrbitEccentricity
            && p.Periapsis >= cfg.MinOrbitAltitude;
    }

    /// <summary>
    /// 计算从当前轨道在远拱点做圆化烧录所需的 ΔV。
    /// </summary>
    public static float CircularizationDeltaV(float apoapsisAlt, float periapsisAlt)
    {
        float R = GameConfig.Current.Space.PlanetRadius;
        float ra = R + apoapsisAlt;
        float a = (ra + R + periapsisAlt) / 2f;

        float vAtApo = OrbitalSpeed(apoapsisAlt, a);
        float vCirc = CircularSpeed(apoapsisAlt);
        return MathF.Abs(vCirc - vAtApo);
    }

    /// <summary>
    /// 齐奥尔科夫斯基火箭方程：ΔV = ISP * g₀ * ln(m₀/m₁)
    /// </summary>
    /// <param name="isp">比冲 (s)</param>
    /// <param name="wetMass">满载质量 (kg)</param>
    /// <param name="dryMass">干质量 (kg)</param>
    public static float TsiolkovskyDeltaV(float isp, float wetMass, float dryMass)
    {
        if (dryMass <= 0 || wetMass <= dryMass) return 0f;
        const float g0 = 9.80665f;
        return isp * g0 * MathF.Log(wetMass / dryMass);
    }

    /// <summary>
    /// 计算重力加速度（简化球对称模型）。
    /// </summary>
    public static float GravityAt(float altitude)
    {
        var cfg = GameConfig.Current.Space;
        float r = cfg.PlanetRadius + altitude;
        return (float)(cfg.PlanetMu / (r * r));
    }

    /// <summary>
    /// 简化大气密度模型（指数衰减）。
    /// 返回 [0,1] 的归一化密度。
    /// </summary>
    public static float AtmosphereDensity(float altitude)
    {
        float H = GameConfig.Current.Space.AtmosphereHeight;
        if (altitude >= H) return 0f;
        if (altitude <= 0) return 1f;
        float scaleHeight = H / 7f;
        return MathF.Exp(-altitude / scaleHeight);
    }
}
