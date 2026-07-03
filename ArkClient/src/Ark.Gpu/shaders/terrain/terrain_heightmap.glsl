#[compute]
#version 450

// ═══════════════════════════════════════════════════════════════════════════════
// GPU 地形高度图生成 — 1 线程 = 1 高度采样点
//
// 使用 FBM (Fractional Brownian Motion) 噪声程序化生成地形高度。
// 支持群系参数驱动：不同群系产生不同地形风格。
// 输出直接写入高度图 buffer，CPU 端读取后构建网格。
//
// 性能：65×65 = 4225 采样 → 17 个工作组（256线程/组），单区块 <0.1ms
// ═══════════════════════════════════════════════════════════════════════════════

layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

// 输出高度图（一维展开，row-major）
layout(set = 0, binding = 0, std430) writeonly buffer HeightBuffer {
    float heights[];
};

// 输出群系 ID（一维展开）
layout(set = 0, binding = 1, std430) writeonly buffer BiomeBuffer {
    uint biomeIds[];
};

// 群系参数（最多 8 种群系）
struct BiomeParams {
    float baseHeight;       // 基础高度偏移
    float heightAmplitude;  // 高度振幅倍数
    float frequencyScale;   // 噪声频率倍数
    float octaves;          // FBM 叠加层数（float → int 使用）
    float useRidged;        // 0.0 = FBM, 1.0 = 脊化噪声
    float slopeThreshold;   // 坡度阈值（度）
    float _pad0;
    float _pad1;
};

layout(set = 0, binding = 2, std430) readonly buffer BiomeParamBuffer {
    BiomeParams biomes[];
};

layout(push_constant) uniform PushConstants {
    float chunkOriginX;     // 区块世界原点 X
    float chunkOriginZ;     // 区块世界原点 Z
    float chunkSize;        // 区块边长（米）
    uint  resolution;       // 高度图分辨率（如 65）
    float maxTerrainHeight; // 最大地形高度
    uint  heightSeed;       // 高度噪声种子
    uint  biomeSeed;        // 群系噪声种子
    uint  biomeCount;       // 已注册群系数量
    };

// ═══════════════════════════════════════════════════════════════════════════════
//                              噪声函数
// ═══════════════════════════════════════════════════════════════════════════════

// 基于种子的哈希（生成伪随机梯度）
vec2 hash2(vec2 p, uint seed) {
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + float(seed) * 0.0001 + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy) * 2.0 - 1.0;
}

// 2D Simplex-like 值噪声
float valueNoise(vec2 p, uint seed) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    
    // Hermite 插值
    vec2 u = f * f * (3.0 - 2.0 * f);
    
    float a = dot(hash2(i + vec2(0.0, 0.0), seed), f - vec2(0.0, 0.0));
    float b = dot(hash2(i + vec2(1.0, 0.0), seed), f - vec2(1.0, 0.0));
    float c = dot(hash2(i + vec2(0.0, 1.0), seed), f - vec2(0.0, 1.0));
    float d = dot(hash2(i + vec2(1.0, 1.0), seed), f - vec2(1.0, 1.0));
    
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// FBM（分形布朗运动）
float fbm(vec2 p, uint seed, int octaves, float frequency) {
    float value = 0.0;
    float amplitude = 1.0;
    float totalAmp = 0.0;
    vec2 pos = p * frequency;
    
    for (int i = 0; i < octaves; i++) {
        value += valueNoise(pos, seed + uint(i) * 31u) * amplitude;
        totalAmp += amplitude;
        pos *= 2.0;        // lacunarity
        amplitude *= 0.5;  // gain
    }
    
    return value / totalAmp; // 归一化到 [-1, 1]
}

// 脊化 FBM（山脉风格）
float ridgedFbm(vec2 p, uint seed, int octaves, float frequency) {
    float value = 0.0;
    float amplitude = 1.0;
    float totalAmp = 0.0;
    vec2 pos = p * frequency;
    float prev = 1.0;
    
    for (int i = 0; i < octaves; i++) {
        float n = valueNoise(pos, seed + uint(i) * 31u);
        n = 1.0 - abs(n);  // 脊化：取绝对值的补
        n = n * n;          // 平方锐化
        n *= prev;          // 抑制低频大振幅
        prev = n;
        
        value += n * amplitude;
        totalAmp += amplitude;
        pos *= 2.0;
        amplitude *= 0.5;
    }
    
    return value / totalAmp; // [0, 1]
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              群系采样
// ═══════════════════════════════════════════════════════════════════════════════

// 简单群系采样（基于低频噪声）
uint sampleBiome(vec2 worldPos) {
    if (biomeCount == 0u) return 0u;
    
    float n = fbm(worldPos, biomeSeed, 3, 0.002);
    // [-1,1] → [0, biomeCount)
    float normalized = (n + 1.0) * 0.5;
    uint biomeIdx = uint(normalized * float(biomeCount));
    return min(biomeIdx, biomeCount - 1u);
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              主函数
// ═══════════════════════════════════════════════════════════════════════════════

void main() {
    uint idx = gl_GlobalInvocationID.x;
    uint totalSamples = resolution * resolution;
    if (idx >= totalSamples) return;
    
    // 一维索引 → 二维网格坐标
    uint gridZ = idx / resolution;
    uint gridX = idx % resolution;
    
    // 网格坐标 → 世界坐标
    float step = chunkSize / float(resolution - 1u);
    float worldX = chunkOriginX + float(gridX) * step;
    float worldZ = chunkOriginZ + float(gridZ) * step;
    vec2 worldPos = vec2(worldX, worldZ);
    
    // ═══ 采样群系 ═══
    uint biomeIdx = sampleBiome(worldPos);
    BiomeParams biome = biomes[biomeIdx];
    
    // ═══ 生成高度 ═══
    float freq = 0.005 * biome.frequencyScale;
    int oct = int(biome.octaves);
    float height;
    
    if (biome.useRidged > 0.5) {
        height = ridgedFbm(worldPos, heightSeed, oct, freq);
        // ridgedFbm 已经在 [0, 1]
    } else {
        height = fbm(worldPos, heightSeed, oct, freq);
        // fbm 在 [-1, 1] → [0, 1]
        height = (height + 1.0) * 0.5;
    }
    
    height = biome.baseHeight + height * maxTerrainHeight * biome.heightAmplitude;
    
    // ═══ 输出 ═══
    heights[idx] = height;
    biomeIds[idx] = biomeIdx;
}
