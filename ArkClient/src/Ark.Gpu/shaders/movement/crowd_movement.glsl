#[compute]
#version 450

// ═══════════════════════════════════════════════════════════════════════════════
// 群体移动计算 — 1 线程 = 1 实体
// 输入：EntityBuffer（位置、速度、输入）
// 输出：TransformBuffer（直接给 MultiMesh 用）
// ═══════════════════════════════════════════════════════════════════════════════

layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

// 与 C# Position/Velocity 结构体对齐（std430）
struct EntityData {
    vec4 position;   // xyz = pos, w = _pad
    vec4 velocity;   // xyz = vel, w = speed
    vec4 moveInput;  // xyz = dir, w = targetSpeed
    vec4 rotation;   // quaternion xyzw
};

// MultiMesh 变换格式：每实例 3 个 vec4（列主序 3x4 矩阵）
struct InstanceTransform {
    vec4 col0;  // basis.x + origin.x
    vec4 col1;  // basis.y + origin.y
    vec4 col2;  // basis.z + origin.z
};

layout(set = 0, binding = 0, std430) buffer EntityBuffer {
    EntityData entities[];
};

layout(set = 0, binding = 1, std430) buffer TransformBuffer {
    InstanceTransform transforms[];
};

layout(push_constant) uniform PushConstants {
    float deltaTime;
    uint  entityCount;
    float maxSpeed;
    float acceleration;
};

// 四元数转旋转矩阵（3x3）
mat3 quatToMat3(vec4 q) {
    float xx = q.x * q.x, yy = q.y * q.y, zz = q.z * q.z;
    float xy = q.x * q.y, xz = q.x * q.z, yz = q.y * q.z;
    float wx = q.w * q.x, wy = q.w * q.y, wz = q.w * q.z;
    
    return mat3(
        1.0 - 2.0 * (yy + zz), 2.0 * (xy + wz),       2.0 * (xz - wy),
        2.0 * (xy - wz),       1.0 - 2.0 * (xx + zz), 2.0 * (yz + wx),
        2.0 * (xz + wy),       2.0 * (yz - wx),       1.0 - 2.0 * (xx + yy)
    );
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= entityCount) return;

    // ═══ 读取实体数据 ═══
    vec3 pos       = entities[idx].position.xyz;
    vec3 vel       = entities[idx].velocity.xyz;
    vec3 inputDir  = entities[idx].moveInput.xyz;
    float targetSpd = entities[idx].moveInput.w;
    vec4 rot       = entities[idx].rotation;

    // ═══ 移动计算 ═══
    // 加速向目标速度
    float currentSpd = length(vel);
    float speedDiff  = targetSpd - currentSpd;
    float accelMag   = acceleration * deltaTime;
    
    if (length(inputDir) > 0.01) {
        inputDir = normalize(inputDir);
        // 平滑转向（简单插值）
        if (currentSpd > 0.01) {
            vec3 currentDir = normalize(vel);
            inputDir = normalize(mix(currentDir, inputDir, min(1.0, 5.0 * deltaTime)));
        }
        // 应用加速
        float newSpeed = min(currentSpd + accelMag, min(targetSpd, maxSpeed));
        vel = inputDir * newSpeed;
    } else {
        // 减速停止
        float newSpeed = max(currentSpd - accelMag * 2.0, 0.0);
        if (currentSpd > 0.01) {
            vel = normalize(vel) * newSpeed;
        } else {
            vel = vec3(0.0);
        }
    }

    // 更新位置
    pos += vel * deltaTime;

    // ═══ 地面钳位 — 防止实体掉出地形 ═══
    const float MIN_Y = -50.0; // 绝对下限（最低群系 Ocean BaseHeight=-5）
    if (pos.y < MIN_Y) {
        pos.y = MIN_Y;
        vel.y = 0.0;
    }

    // ═══ 写回实体数据 ═══
    entities[idx].position.xyz = pos;
    entities[idx].velocity.xyz = vel;
    entities[idx].velocity.w   = length(vel);

    // ═══ 生成 MultiMesh Transform ═══
    mat3 basis = quatToMat3(rot);
    
    transforms[idx].col0 = vec4(basis[0], pos.x);
    transforms[idx].col1 = vec4(basis[1], pos.y);
    transforms[idx].col2 = vec4(basis[2], pos.z);
}
