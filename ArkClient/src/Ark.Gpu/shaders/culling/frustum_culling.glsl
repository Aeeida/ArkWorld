#[compute]
#version 450

// ═══════════════════════════════════════════════════════════════════════════════
// GPU 视锥 + 遮挡剔除 — 1 线程 = 1 实体
// 输入：实体包围盒、相机视锥平面
// 输出：可见性标志（供 IndirectDraw 或 CPU 读取）
// ═══════════════════════════════════════════════════════════════════════════════

layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

// 实体 AABB
struct BoundingData {
    vec4 minBound;  // xyz = min, w = _pad
    vec4 maxBound;  // xyz = max, w = entityIndex（用于 indirect draw）
};

// 可见性结果
struct VisibilityResult {
    uint visible;       // 0 = culled, 1 = visible
    uint lodLevel;      // 0-3，基于距离
    float distanceSq;   // 到相机距离平方
    uint _pad;
};

layout(set = 0, binding = 0, std430) readonly buffer BoundingBuffer {
    BoundingData bounds[];
};

layout(set = 0, binding = 1, std430) writeonly buffer VisibilityBuffer {
    VisibilityResult results[];
};

// Indirect draw 计数（原子操作）
layout(set = 0, binding = 2, std430) buffer IndirectCount {
    uint visibleCount;
};

// 可见实体索引列表（用于 indirect draw）
layout(set = 0, binding = 3, std430) writeonly buffer VisibleIndices {
    uint indices[];
};

layout(push_constant) uniform PushConstants {
    vec4 frustumPlanes[6];  // 视锥 6 平面（xyz = normal, w = distance）
    vec4 cameraPos;         // xyz = 相机位置
    uint entityCount;
    float lodDistance0;     // LOD0 → LOD1 距离
    float lodDistance1;     // LOD1 → LOD2 距离
    float lodDistance2;     // LOD2 → LOD3/隐藏 距离
};

// 判断 AABB 是否在视锥内（6 平面测试）
bool isInFrustum(vec3 minB, vec3 maxB) {
    for (int i = 0; i < 6; i++) {
        vec3 normal = frustumPlanes[i].xyz;
        float d = frustumPlanes[i].w;
        
        // 找最靠近平面正向的顶点（P-vertex）
        vec3 pVertex = vec3(
            normal.x > 0.0 ? maxB.x : minB.x,
            normal.y > 0.0 ? maxB.y : minB.y,
            normal.z > 0.0 ? maxB.z : minB.z
        );
        
        // 如果 P-vertex 在平面外侧，整个 AABB 都在外
        if (dot(normal, pVertex) + d < 0.0) {
            return false;
        }
    }
    return true;
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= entityCount) return;

    vec3 minB = bounds[idx].minBound.xyz;
    vec3 maxB = bounds[idx].maxBound.xyz;
    
    // ═══ 视锥剔除 ═══
    bool inFrustum = isInFrustum(minB, maxB);
    
    // ═══ 距离计算（用 AABB 中心）═══
    vec3 center = (minB + maxB) * 0.5;
    vec3 toCamera = center - cameraPos.xyz;
    float distSq = dot(toCamera, toCamera);
    
    // ═══ LOD 等级 ═══
    uint lod = 0;
    if (distSq > lodDistance2 * lodDistance2) {
        lod = 3;  // 隐藏或最低 LOD
    } else if (distSq > lodDistance1 * lodDistance1) {
        lod = 2;
    } else if (distSq > lodDistance0 * lodDistance0) {
        lod = 1;
    }
    
    // ═══ 写结果 ═══
    uint visible = (inFrustum && lod < 3) ? 1u : 0u;
    
    results[idx].visible   = visible;
    results[idx].lodLevel  = lod;
    results[idx].distanceSq = distSq;
    
    // ═══ 如果可见，加入 indirect draw 列表 ═══
    if (visible == 1u) {
        uint slot = atomicAdd(visibleCount, 1u);
        indices[slot] = idx;
    }
}
