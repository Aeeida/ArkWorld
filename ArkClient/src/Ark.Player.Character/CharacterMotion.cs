namespace Ark.Player.Character;

/// <summary>
/// 角色运动学核心 — 纯函数式 API，零 Godot 依赖。
/// 从 SquadMemberController / TpsPlayerController 中抽出的运动学逻辑。
/// 所有方法为 static pure-function，方便单元测试。
/// </summary>
public static class CharacterMotion
{
    /// <summary>根据冲刺状态选择目标速度。</summary>
    public static float TargetSpeed(bool isSprinting, float walkSpeed, float sprintSpeed)
        => isSprinting ? sprintSpeed : walkSpeed;

    /// <summary>
    /// 沿单轴平滑靠近目标速度（MoveToward 等价）。
    /// </summary>
    public static float MoveToward(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta) return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }

    /// <summary>
    /// 计算水平速度分量（X / Z），含加速/减速和空气控制。
    /// </summary>
    public static (float vx, float vz) ApplyHorizontal(
        float vx, float vz,
        float targetVx, float targetVz,
        float accel, float decel, float airControl,
        bool isOnFloor, bool hasInput, float dt)
    {
        float a = hasInput ? accel : decel;
        if (!isOnFloor) a *= airControl;
        return (MoveToward(vx, targetVx, a * dt),
                MoveToward(vz, targetVz, a * dt));
    }

    /// <summary>
    /// 应用重力 + 地面着陆逻辑，返回新的 verticalVelocity 和 jumpsRemaining。
    /// </summary>
    public static (float vy, int jumpsRemaining) ApplyGravity(
        float vy, float gravity, float dt,
        bool isOnFloor, int jumpsRemaining, int maxJumps)
    {
        if (!isOnFloor)
        {
            vy -= gravity * dt;
        }
        else
        {
            jumpsRemaining = maxJumps;
            if (vy < 0) vy = 0;
        }
        return (vy, jumpsRemaining);
    }

    /// <summary>
    /// 尝试跳跃，返回新的 verticalVelocity 和 jumpsRemaining。
    /// </summary>
    public static (float vy, int jumpsRemaining) TryJump(
        float vy, float jumpForce, int jumpsRemaining, bool jumpRequested)
    {
        if (jumpRequested && jumpsRemaining > 0)
        {
            vy = jumpForce;
            jumpsRemaining--;
        }
        return (vy, jumpsRemaining);
    }
}
