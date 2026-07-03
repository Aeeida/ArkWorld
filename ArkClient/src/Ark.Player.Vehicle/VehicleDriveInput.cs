namespace Ark.Player.Vehicle;

/// <summary>
/// 载具驾驶纯计算 — 转向、油门、速度接近。
/// 从 TpsPlayerController / SquadMemberController 的 ProcessVehicleControl 中抽出。
/// 所有方法为 static pure-function，方便单元测试。
/// </summary>
public static class VehicleDriveInput
{
    /// <summary>从原始输入计算油门值 (0..1)。</summary>
    public static float CalculateThrottle(float forwardInput)
        => Math.Clamp(forwardInput, 0f, 1f);

    /// <summary>从原始输入计算刹车值 (0..1)。</summary>
    public static float CalculateBrake(float backwardInput)
        => Math.Clamp(-backwardInput, 0f, 1f);

    /// <summary>从原始输入计算转向值 (-1..1)。</summary>
    public static float CalculateSteering(float horizontalInput)
        => Math.Clamp(horizontalInput, -1f, 1f);

    /// <summary>
    /// 根据当前车速计算实际转向速率（车速越快转向越灵活）。
    /// </summary>
    public static float TurnRate(float currentSpeed, float maxSpeed, float baseTurnRate = 2f)
    {
        float speedRatio = maxSpeed > 0 ? Math.Clamp(MathF.Abs(currentSpeed) / maxSpeed, 0f, 1f) : 0f;
        return baseTurnRate * (0.3f + 0.7f * speedRatio);
    }

    /// <summary>
    /// 计算目标速度：W/S 前进/后退，冲刺加速，后退 80%。
    /// </summary>
    /// <param name="forwardBackInput">前进为正，后退为负 (-1..1)。</param>
    /// <param name="maxSpeed">最大前进速度。</param>
    /// <param name="isSprinting">冲刺模式。</param>
    /// <param name="sprintFactor">冲刺时速度系数（默认 1.0 = 满速）。</param>
    /// <param name="reverseFactor">后退速度系数（默认 0.8）。</param>
    public static float TargetSpeed(float forwardBackInput, float maxSpeed,
        bool isSprinting, float sprintFactor = 1.0f, float reverseFactor = 0.8f)
    {
        float forwardSpeed = isSprinting ? maxSpeed * sprintFactor : maxSpeed * 0.7f;
        float reverseSpeed = maxSpeed * reverseFactor;
        return forwardBackInput >= 0
            ? forwardBackInput * forwardSpeed
            : forwardBackInput * reverseSpeed;
    }

    /// <summary>
    /// 速度平滑靠近目标值（MoveToward）。
    /// </summary>
    public static float ApproachSpeed(float current, float target, float maxSpeed, float dt, float factor = 3f)
    {
        float maxDelta = maxSpeed * dt * factor;
        if (MathF.Abs(target - current) <= maxDelta) return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }
}
