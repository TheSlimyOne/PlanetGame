#[compute]
#version 450
#define sqrt2_2 0.707106781
#define sqrt2   1.414213562
#define PI      3.141592653

//Jad Khoury https://jadkhoury.github.io/files/MasterThesisFinal.pdf
layout(local_size_x = 8, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) buffer restrict AtomicCounterBuffer {
    uint primCount_full[16];
    uint primCount_culled[16];
};

layout(set = 0, binding = 1, std430) buffer restrict IndicesBlock {
    uint read_index;
    uint write_index;
};

layout(set = 0, binding = 2, std430) buffer restrict readonly ReadList {
    uvec4 read_list[];
};

layout(set = 0, binding = 3, std430) buffer restrict writeonly WriteFullList {
    uvec4 write_full_list[];
};

layout(set = 0, binding = 4, std430) buffer restrict writeonly WriteCulledList {
    uvec4 write_culled_list[];
};

layout(set = 0, binding = 5, std430) buffer restrict readonly Positions {
    vec4 position_list[];
};

layout(set = 0, binding = 6, std430) buffer restrict CameraData {
    
    mat4 cameraToWorld;
    float CameraFOV;
    float CameraFarPlane;
    float CameraNearPlane;
};
layout(set = 0, binding = 7, std430) buffer restrict dummyData {
    vec4 data_list[];
};

struct Triangle {
    vec3 v0; // (0, 0)
    vec3 v1; // (0, 1)
    vec3 v2; // (1, 0)
};

struct Key {
    uvec2 nodeID;
    uint meshPolygonID;
    uint rootID;
};

vec2 getTranslation(uint b1) {
    vec2 translation;
    translation.x = float(b1 & 0x1); 
    translation.y = float(b1 ^ 0x1);
    return translation * 0.5;
}

uint getRotation(uint b1b2, uint b1, uint b2) {
    uint a = (b1b2 ^ 0x2);
    uint b = (a | 0x1);
    uint c = (b1 ^ b2);
    return (b * c);
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
/*
    +--+-------------+------------+-------------------+------------+--------------------------------+--------------------------------+
    |b |b >> 1 => b1 |b & 1 => b2 |(b1 ^ b2) ^ 1 => c |b1 ^ 1 => s |sqrt2_2 * (-1 + 2 * c)) => cos_ |sqrt2_2 * (-1 + 2 * s)) => sin_ |
    +--+-------------+------------+-------------------+------------+--------------------------------+--------------------------------+
    |0 |0            |0           |0                  |1           |sqrt2_2                         |sqrt2_2                         |
    |1 |0            |1           |1                  |1           |-sqrt2_2                        |sqrt2_2                         |
    |2 |1            |0           |1                  |0           |-sqrt2_2                        |-sqrt2_2                        |
    |3 |1            |1           |0                  |0           |sqrt2_2                         |-sqrt2_2                        |
    +--+-------------+------------+-------------------+------------+--------------------------------+--------------------------------+
*/
vec2 quickPI_4(uint a) {
    int b = int(a & 3); 
    int b1 = b >> 1;  
    int b2 = b & 1;
    float cos_ = sqrt2_2 * (-1 + 2 * ((b1 ^ b2) ^ 1));
    float sin_ = sqrt2_2 * (-1 + 2 * (b1 ^ 1));
    return vec2(cos_, sin_);
}

vec2 rotate(uint rotationIndex, vec2 translation) {
    vec2 r;
    ivec2 trig = quickPI_2(rotationIndex);
    r.x = trig.x * translation.x - trig.y * translation.y;
    r.y = trig.y * translation.x + trig.x * translation.y;
    return r;
}

uint getBranching(uvec2 key, int level, int msb) {
    // Create the mask that will be shifted based on level
    uint mask = (0x3 << (msb % 32)) >> (level * 2);

    // Domain index is used to see if we are in 
    // the msb in the key or the lsb in the key
    int domain_index = (msb % 32) / 2; 

    if (msb >= 32)
    {
        if (domain_index < level)
        {
            mask = (0x3 << 30) >> ((level - 1 - domain_index) * 2);
            return (key.y & mask) >> (msb - (2 * level));
        }
        return (key.x & mask) >> ((msb % 32) - (2 * level));
    }    

    return (key.y & mask) >> (msb - (2 * level));
}

int findMSB64(uvec2 key) {
    return (key.x == 0) ? findMSB(key.y) : (findMSB(key.x) + 32);
}

uvec2 leftShift64(uvec2 nodeID, uint shift)
{
    uvec2 result = nodeID;
    //Extract the "shift" first bits of y and append them at the end of x
    result.x = result.x << shift;
    result.x |= result.y >> (32u - shift);
    result.y  = result.y << shift;
    return result;
}
uvec2 rightShift64(uvec2 nodeID, uint shift)
{
    uvec2 result = nodeID;
    //Extract the "shift" last bits of x and prepend them to y
    result.y = result.y >> shift;
    result.y |= result.x << (32u - shift);
    result.x = result.x >> shift;
    return result;
}


vec3 localPointToWorldPoint(vec2 point, vec3 vertexA, vec3 vertexB, vec3 vertexC) {
    return vertexA * point.x + vertexB * point.y + vertexC * (1 - point.x - point.y);
}

vec3 calculateCentroid(Triangle t) {
    return (t.v0 + t.v1 + t.v2) / 3;
}

float calulateDistance(vec3 a, vec3 b) {
    return distance(a, b);
}


Triangle createTriangle(mat3 transform_matrix, uint meshPolygonID, uint rootID) {
    Triangle t;
    vec2 point_a = (vec3(0, 0, 1) * transform_matrix).xy;
    vec2 point_b = (vec3(0, 1, 1) * transform_matrix).xy;
    vec2 point_c = (vec3(1, 0, 1) * transform_matrix).xy;

    uint vertexBaseIndex = meshPolygonID * 5;
    uint vertexKeyA = meshPolygonID;
    uint vertexKeyB = ((rootID >> 1) ^ 1) + ((rootID & 1) << 1);

    vec3 base_Triangle_a = (position_list[vertexBaseIndex + vertexKeyA + 1]).xyz;
    vec3 base_Triangle_b = (position_list[vertexBaseIndex + vertexKeyB + 1]).xyz;
    vec3 base_Triangle_c = (position_list[vertexBaseIndex]).xyz;

    t.v0 = localPointToWorldPoint(point_a, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.v1 = localPointToWorldPoint(point_b, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.v2 = localPointToWorldPoint(point_c, base_Triangle_a, base_Triangle_b, base_Triangle_c);

    return t;
}

Triangle leafSpaceToWorldSpace(uvec4 key) { 
    int msb = findMSB64(key.xy);
    vec2 translation = vec2(0,0);
    vec2 temp = vec2(0,0);
    uint theta = 0;
    float scale = 1.0;

    for (int i = 0; i < msb / 2; i++) {
        uint b1b2 = getBranching(key.xy, i, msb - 2);
        uint b1 = b1b2 >> 1;
        uint b2 = b1b2 & 0x01;
        temp.xy = scale * getTranslation(b1);

        translation += rotate(theta, temp.xy);
        theta += getRotation(b1b2, b1, b2);
        scale *= 0.5;
    }
    
    ivec2 trig = quickPI_2(theta);
    mat3 transform_matrix = mat3(
        trig.x * scale, -trig.y * scale, translation.x,
        trig.y * scale,  trig.x * scale, translation.y,
        0.0,              0.0,           1.0
    );

    return createTriangle(transform_matrix, key.z, key.w);   
}

vec4 getTransformation(uvec4 key) {
    int msb = findMSB64(key.xy);
    vec2 translation = vec2(0,0);
    vec2 temp = vec2(0,0);
    uint theta = 0;
    float scale = 1.0;

    for (int i = 0; i < msb / 2; i++) {
        uint b1b2 = getBranching(key.xy, i, msb - 2);
        uint b1 = b1b2 >> 1;
        uint b2 = b1b2 & 0x01;
        temp.xy = scale * getTranslation(b1);

        translation += rotate(theta, temp.xy);
        theta += getRotation(b1b2, b1, b2);
        scale *= 0.5;
    }

    return vec4(theta, scale, translation);
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
    return -log2(num/dom);
}

int getLevelInKey(uvec2 key) {
    return findMSB64(key);
}

uvec4 getParentKey(uvec4 key) {
    return uvec4(key.xy, rightShift64(key.wz, 2u));
}

uvec4[4] getChildKeys(uvec4 key) {
    uvec2 baseKey = leftShift64(key.wz, 2u);
    uvec4[] keys = {
        uvec4(key.xy, baseKey[0], baseKey[1] + 0),
        uvec4(key.xy, baseKey[0], baseKey[1] + 1),
        uvec4(key.xy, baseKey[0], baseKey[1] + 2),
        uvec4(key.xy, baseKey[0], baseKey[1] + 3)
    };
    return keys;

}

bool isUpperLeftChild(uvec2 key) {
    return (3 & key[1]) == 0;
}

void main() {
    uint leaf_count = uint(atomicExchange(primCount_full[read_index], primCount_full[read_index]));
    
    uint invocationID = gl_GlobalInvocationID.x;
    if (invocationID > leaf_count)
        return;
    
    uvec4 key = read_list[invocationID];
    Triangle t = leafSpaceToWorldSpace(key);
    int current_LOD = getLevelInKey(key.xy);
    float target_LOD = calculateLOD (
        calulateDistance(calculateCentroid(t), cameraToWorld[3].xyz),
        CameraFOV, // Must be in radians
        1000000.0
    );

    // if (target_LOD < current_LOD - 1) {
    //     if (isUpperLeftChild(key.xy)) {
    //         uint idx = atomicAdd(primCount_full[write_index], 1);
    //         write_full_list[idx] = getParentKey(key);
    //     }
    //     else {
    //         return;
    //     }
    // } else 
    if (target_LOD > current_LOD) {
        uvec4 children[4] = getChildKeys(key);
        for (int i = 0; i < 3; ++i) {
            uint idx = atomicAdd(primCount_full[write_index], 1);
            write_full_list[idx] = children[i];
        }
    } else {
        uint idx = atomicAdd(primCount_full[write_index], 1);
        write_full_list[idx] = key;
    }



    // float current_LOD = getLevelInKey();

    // uvec4 debug_key = uvec4(base4ToHex(0), base4ToHex(13333), 0, 0) ;
    // if (invocationID == 0) {
    //     Triangle t = leafSpaceToWorldSpace(debug_key);
    //     data_list[1].xyz = t.v0;
    //     data_list[2].xyz = t.v1;
    //     data_list[3].xyz = t.v2;
    //     data_list[4].xyz = vec3(CameraFOV, CameraFarPlane, CameraNearPlane);
    //     data_list[5].xyz = cameraToWorld[3].xyz;
    // }

    
    // data_list[invocationID].x = read_list.length();

    // atomicAdd(write_list[invocationID].w, position_list[key.z].x);

}