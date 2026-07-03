#[compute]
#version 450

// ═══════════════════════════════════════════════════════════════════════════════
// GPU 粒子模拟 — 适用于大量轻量粒子（弹道轨迹、爆炸碎片、环境粒子）
// 每帧更新位置、速度、生命周期
// ═══════════════════════════════════════════════════════════════════════════════

layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

struct Particle {
    vec4 position;   // xyz = pos, w = size
    vec4 velocity;   // xyz = vel, w = rotation
    vec4 color;      // rgba
    vec4 lifetime;   // x = current, y = max, z = fadeStart, w = emitterIndex
};

layout(set = 0, binding = 0, std430) buffer ParticleBuffer {
    Particle particles[];
};

// 发射器参数（多个发射器共用 shader）
struct EmitterParams {
    vec4 position;       // xyz = 发射器位置
    vec4 direction;      // xyz = 发射方向, w = 扩散角度
    vec4 velocityRange;  // x = min, y = max
    vec4 sizeRange;      // x = startMin, y = startMax, z = endMin, w = endMax
    vec4 colorStart;
    vec4 colorEnd;
    vec4 lifetimeRange;  // x = min, y = max
    vec4 gravity;        // xyz = 重力方向 * 强度
};

layout(set = 0, binding = 1, std430) readonly buffer EmitterBuffer {
    EmitterParams emitters[];
};

// 死亡粒子队列（用于回收）
layout(set = 0, binding = 2, std430) buffer DeadParticleQueue {
    uint deadCount;
    uint deadIndices[];
};

layout(push_constant) uniform PushConstants {
    float deltaTime;
    uint  particleCount;
    uint  maxParticles;
    float globalTime;      // 用于噪声
};

// 简单伪随机（基于索引和时间）
float rand(uint seed) {
    seed = (seed ^ 61u) ^ (seed >> 16u);
    seed *= 9u;
    seed = seed ^ (seed >> 4u);
    seed *= 0x27d4eb2du;
    seed = seed ^ (seed >> 15u);
    return float(seed) / 4294967295.0;
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= particleCount) return;

    Particle p = particles[idx];
    
    // 已死亡的粒子跳过
    if (p.lifetime.x <= 0.0) return;
    
    // ═══ 更新生命周期 ═══
    p.lifetime.x -= deltaTime;
    
    if (p.lifetime.x <= 0.0) {
        // 死亡：加入回收队列
        uint slot = atomicAdd(deadCount, 1u);
        if (slot < maxParticles) {
            deadIndices[slot] = idx;
        }
        p.color.a = 0.0;
        particles[idx] = p;
        return;
    }
    
    // ═══ 获取发射器参数 ═══
    uint emitterIdx = uint(p.lifetime.w);
    EmitterParams emitter = emitters[emitterIdx];
    
    // ═══ 物理更新 ═══
    // 应用重力
    p.velocity.xyz += emitter.gravity.xyz * deltaTime;
    
    // 简单空气阻力
    p.velocity.xyz *= 1.0 - (0.1 * deltaTime);
    
    // 更新位置
    p.position.xyz += p.velocity.xyz * deltaTime;
    
    // ═══ 外观更新 ═══
    float lifeRatio = p.lifetime.x / p.lifetime.y;
    float fadeRatio = (p.lifetime.x < p.lifetime.z) 
        ? (p.lifetime.x / p.lifetime.z) 
        : 1.0;
    
    // 颜色插值
    p.color = mix(emitter.colorEnd, emitter.colorStart, lifeRatio);
    p.color.a *= fadeRatio;
    
    // 大小插值
    float sizeStart = mix(emitter.sizeRange.x, emitter.sizeRange.y, rand(idx));
    float sizeEnd   = mix(emitter.sizeRange.z, emitter.sizeRange.w, rand(idx + 1000u));
    p.position.w = mix(sizeEnd, sizeStart, lifeRatio);
    
    // 旋转
    p.velocity.w += deltaTime * 2.0;
    
    particles[idx] = p;
}
