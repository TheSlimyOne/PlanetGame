#[compute]
#version 450
#extension GL_EXT_shader_atomic_float2 : require
#define sqrt2   1.414213562
#define PI      3.141592653

//Jad Khoury https://jadkhoury.github.io/files/MasterThesisFinal.pdf
layout(local_size_x = 32, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) buffer restrict AtomicCounterBuffer {
    uint primCount_full[16];
    uint primCount_culled[16];
};

layout(set = 0, binding = 1, std430) buffer restrict readonly IndicesBlock {
    uint read_index;
    uint write_index;
    uint delete_index;
    uint maximum_nodes;
};

layout(set = 0, binding = 2, std430) buffer restrict readonly ReadList {
    uvec4 read_list[];
};

layout(set = 0, binding = 3, r32f) restrict uniform image2D GlobalKeyData;
layout(set = 0, binding = 4, std430) buffer restrict writeonly WriteFullList {
    uvec4 write_full_list[];
};

layout(set = 0, binding = 5, std430) buffer restrict writeonly WriteCulledList {
    uvec4 write_culled_list[];
};

layout(set = 0, binding = 6, std430) buffer restrict readonly BaseTriangles {
    vec4 base_triangles[];
};

layout(set = 0, binding = 7, std430) buffer restrict readonly ExternalData {
    mat4 viewProjectionMatrix;         // 16 * 4 = 64 bytes
    vec4 cameraPosition;
    mat4 planetTransformMatrix;
    float CameraFOV;     
    float subFactor;
    float morphFactor;
    float padding;
};

layout(set = 0, binding = 8, std430) buffer restrict Debug {
    bool renderAll;
};

layout(set = 0, binding = 9, r8) restrict uniform readonly image2D heightMap;
layout(set = 0, binding = 10, r8) restrict uniform readonly image2D heightGradient;
layout(set = 0, binding = 11, rgba32f) restrict writeonly uniform image2D DisplayKeys;

struct Triangle {
    vec3 v0; // (0, 0)
    vec3 v1; // (0, 1)
    vec3 v2; // (1, 0)

    vec3 origin; // (0.5, 0.5)

    vec3 xNeighbor; // (0.5, -0.5)
    vec3 yNeighbor; // (-0.5, 0.5)
};

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
    vec3 square = p * p;
	return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

vec2 point_on_sphere_to_UV(vec3 p) {
    p = normalize(p);
    float longitude = atan(p.x, p.z);
    float latitude = asin(-p.y);
    float u = (longitude / (2.0 * PI) + 0.5);
    float v = (latitude / PI) + 0.5;
    return vec2(u, v);
}

vec2 getTranslation(uint b1) {
    vec2 translation;
    translation.x = float(b1 & 0x1); 
    translation.y = float(b1 ^ 0x1);
    return translation * 0.5;
}

int getRotation(uint b1b2, uint b1, uint b2) {
    uint a = (b1b2 ^ 0x2);
    uint b = (a | 0x1);
    uint c = (b1 ^ b2);
    return int(b * c);
}

/*
    +--+-------------+------------+------------+---------+--------+-----------------------+---------------------+
    |b |b >> 1 => b1 |b & 1 => b2 |b2^1 => bn2 |b1 & bn2 |b1 & b2 |bn2 - (2 * (b1 & bn2)) |b2 - (2 * (b1 & b2)) |
    +--+-------------+------------+------------+---------+--------+-----------------------+---------------------+
    |0 |0            |0           |1           |0        |0       |1                      |0                    |
    |1 |0            |1           |0           |0        |0       |0                      |1                    |
    |2 |1            |0           |1           |1        |0       |-1                     |0                    |
    |3 |1            |1           |0           |0        |1       |0                      |-1                   |
    +--+-------------+------------+------------+---------+--------+-----------------------+---------------------+
*/
ivec2 quickPI_2(uint a) {
    int b = int(a & 3);
    int b1 = b >> 1;    
    int b2 = b & 1;
    int bn2 = b2 ^ 1;
    int c = bn2 - (2 * (b1 & bn2));
    int s = b2 - (2 * (b1 & b2));
    return ivec2(c, s);
}

vec2 rotate(uint rotationIndex, vec2 translation) {
    vec2 r;
    ivec2 trig = quickPI_2(rotationIndex);
    r.x = trig.x * translation.x - trig.y * translation.y;
    r.y = trig.y * translation.x + trig.x * translation.y;
    return r;
}

uvec2 leftShift64(uvec2 nodeID, uint shift) {
    uvec2 result;
    if (shift == 0) return nodeID;

    if (shift < 32)
    {
        result.x = (nodeID.x << shift) | (nodeID.y >> (32 - shift));
        result.y = nodeID.y << shift;
    }
    else
    {
        result.x = nodeID.y << (shift - 32);
        result.y = 0u;
    }

    return result;
}

uvec2 rightShift64(uvec2 nodeID, uint shift) {
    uvec2 result;
    if (shift == 0) return nodeID;

    if (shift < 32)
    {
        result.y = (nodeID.y >> shift) | (nodeID.x << (32 - shift));
        result.x = nodeID.x >> shift;
    }
    else
    {
        result.y = nodeID.x >> (shift - 32);
        result.x = 0u;
    }

    return result;
}

/*
    msb - 2 => is the offset to ignore the leading 01
    level * 2 => level is used as an index. Used to tell how much to shift over to get the bits in question. 
    Multiplied by 2 so to shift over two each time.
*/
uint getBranching(uvec2 key, int level, int msb) {
    return (rightShift64(key, (msb - 2) - (level * 2)).y & 0x3); 
}

int findMSB64(uvec2 key) {
    return (key.x == 0) ? findMSB(key.y) : (findMSB(key.x) + 32);
}

vec3 localPointToWorldPointCubical(vec2 point, vec3 vertexA, vec3 vertexB, vec3 vertexC) {
    return vertexA * point.x + vertexB * point.y + vertexC * (1 - point.x - point.y);
}

vec3 localPointToWorldPointSpherical(vec2 point, vec3 vertexA, vec3 vertexB, vec3 vertexC) {
    return point_on_cube_to_point_on_sphere(vertexA * point.x + vertexB * point.y + vertexC * (1 - point.x - point.y));
}

/*
    +-------+-------+
    | Key A | Key B |
    +-------+-------+
    |     0 |     1 |
    |     1 |     3 |
    |     2 |     0 |
    |     3 |     2 |
    +-------+-------+
*/
Triangle createTriangle(mat3 transform_matrix, uint meshPolygonID, uint rootID) {
    vec2 point_a = (vec3(0.5, 0.5, 1) * transform_matrix).xy;
    vec2 point_b = (vec3(0.5, -0.5, 1) * transform_matrix).xy;
    vec2 point_c = (vec3(-0.5, 0.5, 1) * transform_matrix).xy;

    vec2 point_v0 = (vec3(0, 0, 1) * transform_matrix).xy;
    vec2 point_v1 = (vec3(0, 1, 1) * transform_matrix).xy;
    vec2 point_v2 = (vec3(1, 0, 1) * transform_matrix).xy;

    uint vertexBaseIndex = meshPolygonID * 5;
    uint vertexKeyA = rootID;
    uint vertexKeyB = ((rootID >> 1) ^ 1) + ((rootID & 1) << 1);

    vec3 base_Triangle_a = (base_triangles[vertexBaseIndex + vertexKeyA + 1]).xyz;
    vec3 base_Triangle_b = (base_triangles[vertexBaseIndex + vertexKeyB + 1]).xyz;
    vec3 base_Triangle_c = (base_triangles[vertexBaseIndex]).xyz;
    
    Triangle t;
    t.v0 = localPointToWorldPointSpherical(point_v0, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.v1 = localPointToWorldPointSpherical(point_v1, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.v2 = localPointToWorldPointSpherical(point_v2, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    
    t.origin = localPointToWorldPointSpherical(point_a, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.xNeighbor = localPointToWorldPointSpherical(point_b, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.yNeighbor = localPointToWorldPointSpherical(point_c, base_Triangle_a, base_Triangle_b, base_Triangle_c);

    return t;
}

Triangle leafSpaceToWorldSpace(uvec4 key) { 
    int msb = findMSB64(key.xy);
    vec2 translation = vec2(0,0);
    vec2 temp;
    int theta = 0;
    float scale = 1.0;

    for (int i = 0; i < msb / 2; i++) {
        uint b1b2 = getBranching(key.xy, i, msb);
        uint b1 = b1b2 >> 1;
        uint b2 = b1b2 & 1;
        temp = scale * getTranslation(b1);

        translation += rotate(theta, temp);
        theta += getRotation(b1b2, b1, b2);
        scale *= 0.5;
    }
    
    ivec2 trig = quickPI_2(theta);
    mat3 transform_matrix = mat3(
        vec3(float(trig.x) * scale, float(-trig.y) * scale, translation.x),
        vec3(float(trig.y) * scale, float(trig.x)  * scale, translation.y),
        vec3(0.0,                   0.0,                    1.0)
    );

    return createTriangle(transform_matrix, key.z, key.w);   
}

vec4 getTransformation(uvec4 key) {
    int msb = findMSB64(key.xy);
    vec2 translation = vec2(0,0);
    vec2 temp;
    uint theta = 0;
    float scale = 1.0;

    for (int i = 0; i < msb / 2; i++) {
        uint b1b2 = getBranching(key.xy, i, msb);
        uint b1 = b1b2 >> 1;
        uint b2 = b1b2 & 1;
        temp.xy = scale * getTranslation(b1);

        translation += rotate(theta, temp.xy);
        theta += getRotation(b1b2, b1, b2);
        scale *= 0.5;
    }

    return vec4(theta, scale, translation);
}

float getScale(uvec4 key) {
    return pow(0.5, findMSB64(key.xy) / 2);
}

uint base4ToHex(uint base4Value) {
    uint result = 0u;
    uint base = 1u;
    
    while (base4Value != 0u) {
        uint digit = base4Value % 10u; // Extract the last digit
        result += digit * base;        // Multiply the digit by the current base and add to the result
        base *= 4u;                    // Increase the base by a power of 4
        base4Value /= 10u;             // Move to the next digit
    }

    return result;
}

float calculateLOD(float dist, float fovy, float factor) {
    float num = dist * tan(fovy/2);
    float dom = sqrt2 * factor;
    return clamp(-log2(num/dom), -1, 31);
}

float calculateLODToCam(vec3 from) {
    return calculateLOD(
        distance((vec4(from, 1) * planetTransformMatrix).xyz, cameraPosition.xyz),
        CameraFOV, // Must be in radians
        subFactor
    );
}

int getLevelInKey(uvec2 key) {
    return findMSB64(key) / 2;
}

uvec4 getParentKey(uvec4 key) {
    return uvec4(rightShift64(key.xy, 2u), key.zw);
}

uvec4[4] getChildKeys(uvec4 key) {
    uvec2 baseKey = leftShift64(key.xy, 2u);
    uvec4[] keys = {
        uvec4(baseKey.x, baseKey.y | 0, key.zw),
        uvec4(baseKey.x, baseKey.y | 1, key.zw),
        uvec4(baseKey.x, baseKey.y | 2, key.zw),
        uvec4(baseKey.x, baseKey.y | 3, key.zw)
    };
    return keys;
}

bool isUpperLeftChild(uvec2 key) {
    return (3 & key[1]) == 0;
}

vec4 isTJunction(uvec4 key, Triangle triangle, float lod) {
    uint b1b2 = key.y & 0x3;
    vec3 neighbour;

    if (key.xy == uvec2(0,1))
    {
        return vec4(0,0,0,0);
    }

    vec4 color;
    
    if ((b1b2 >> 1) == 0) { // Check Y neighbour
        neighbour = triangle.yNeighbor;
        color = vec4(0,1,0,0);
    }
    else if ((b1b2 >> 1) == 1) { // Check X neighbour
        neighbour = triangle.xNeighbor;
        color = vec4(1,0,0,0);
    }
    float neighbour_lod = calculateLODToCam(neighbour);
    return lod - 1 > neighbour_lod ? color : vec4(0,0,0,0);
}

uint getJunctionFlags(uvec4 key, Triangle triangle, float lod) {
    uint b1b2 = key.y & 0x3;
    vec3 neighbour;

    if (key.xy == uvec2(0,1))
    {
        return 4 << 29;
    }

    if ((b1b2 >> 1) == 0) // Check Y neighbour
        neighbour = triangle.yNeighbor;
    else if ((b1b2 >> 1) == 1) // Check X neighbour
        neighbour = triangle.xNeighbor;
    

    float neighbour_lod = calculateLODToCam(neighbour);

    if (lod - 1 > neighbour_lod)
    {
        return b1b2 << 29;
    }
    return 4 << 29;
   
}

uint getMorphFactor(vec3 point) {
    float lod = calculateLODToCam(point);
    float morphFactor = clamp((abs(fract(lod) - 1) - (1 - morphFactor))/morphFactor, 0, 1);
    uint packedHalf = packHalf2x16(vec2(morphFactor, 0.0));
    uint halfFloat = packedHalf & 0xFFFF;

    // return halfFloat << 4;
    return halfFloat << 4;
}

bool PointInFrustum(vec3 point) {
    vec4 clipSpacePoint = viewProjectionMatrix * (vec4(point, 1) * planetTransformMatrix);
    vec3 ndcPoint = clipSpacePoint.xyz / clipSpacePoint.w;
    // return true;

    return ndcPoint.x >= -1.0 && ndcPoint.x <= 1.0 &&
           ndcPoint.y >= -1.0 && ndcPoint.y <= 1.0 &&
           ndcPoint.z >= -1.0 && ndcPoint.z <= 1.0;
}

bool TriangleInFrustum(Triangle triangle) {
    return PointInFrustum(triangle.v0) ||
           PointInFrustum(triangle.v1) ||
           PointInFrustum(triangle.v2);
}

ivec2 getKeyCoordinate(uint idx) {
    ivec2 image_size = imageSize(DisplayKeys);
    return ivec2(idx % image_size.x, idx / image_size.y);
}

void cull_key(uvec4 key, Triangle triangle, float lod) {
    if (renderAll || TriangleInFrustum(triangle)) {
        uint write_culled_index = atomicAdd(primCount_culled[write_index], 1);
        write_culled_list[write_culled_index] = key;
        imageStore(DisplayKeys, getKeyCoordinate(write_culled_index), uintBitsToFloat(key));
    }
    imageAtomicMax(GlobalKeyData, ivec2(0, 0), getLevelInKey(key.xy));
}

void main() {
    uint leaf_count = uint(atomicExchange(primCount_full[read_index], primCount_full[read_index]));
    uint invocationID = gl_GlobalInvocationID.x;
    if (invocationID >= leaf_count)
        return;
    
    uvec4 key = read_list[invocationID];
    key.w &= 0xFu;

    uvec4 parent_key = getParentKey(key);
    uvec4 grand_parent_key = getParentKey(parent_key);

    Triangle triangle = leafSpaceToWorldSpace(key);
    Triangle parent_triangle = leafSpaceToWorldSpace(parent_key);
    Triangle grand_parent_triangle = leafSpaceToWorldSpace(grand_parent_key);

    float current_LOD = getLevelInKey(key.xy);

    float parent_target_LOD = calculateLODToCam(parent_triangle.origin);
    float target_LOD = calculateLODToCam(triangle.origin);
 
    if (target_LOD > current_LOD) { // subdivide
        uvec4 children_keys[4] = getChildKeys(key);
        for (int i = 0; i < 4; i++) {
            uint idx = atomicAdd(primCount_full[write_index], 1);
            Triangle child_triangle = leafSpaceToWorldSpace(children_keys[i]);
            
            children_keys[i].w |= getJunctionFlags(children_keys[i], triangle, current_LOD + 1);
            children_keys[i].w |= getMorphFactor(child_triangle.origin);
            
            write_full_list[idx] = children_keys[i];
            cull_key(children_keys[i], child_triangle, current_LOD + 1);
        }
    } else if (parent_target_LOD < current_LOD - 1 && key.xy != uvec2(0, 1)) { // merging
        if (isUpperLeftChild(key.xy)) {
            uint idx = atomicAdd(primCount_full[write_index], 1);

            parent_key.w |= getJunctionFlags(parent_key, grand_parent_triangle, current_LOD - 1);
            parent_key.w |= getMorphFactor(parent_triangle.origin);

            write_full_list[idx] = parent_key;
            cull_key(parent_key, parent_triangle, current_LOD - 1);
        }
    } else {
        uint idx = atomicAdd(primCount_full[write_index], 1);

        key.w |= getJunctionFlags(key, parent_triangle, current_LOD);
        key.w |= getMorphFactor(triangle.origin);
        
        write_full_list[idx] = key;
        cull_key(key, triangle, current_LOD);
    }
}