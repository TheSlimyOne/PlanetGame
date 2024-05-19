#[compute]
#version 450
#define sqrt2_2 0.707106781
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
    mat4 projectionMatrix;
    float CameraFOV;
    float CameraFarPlane;
    float CameraNearPlane;
    float radius;
    float subFactor;
};

layout(set = 0, binding = 7, std430) buffer restrict dummyData {
    mat4 data_list[];
};

layout(set = 0, binding = 8, std430) buffer restrict DistanceValues {
    float distance_values[];
};

struct Triangle {
    vec3 v0; // (0, 0)
    vec3 v1; // (0, 1)
    vec3 v2; // (1, 0)
    vec3 origin;
};

struct Key {
    uvec2 nodeID;
    uint meshPolygonID;
    uint rootID;
};

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
	float x2 = p.x * p.x;
	float y2 = p.y * p.y;
	float z2 = p.z * p.z;
	
	float x = p.x * sqrt(1.0 - (y2 + z2) / 2.0 + y2 * z2 / 3.0);
	float y = p.y * sqrt(1.0 - (z2 + x2) / 2.0 + z2 * x2 / 3.0);
	float z = p.z * sqrt(1.0 - (x2 + y2) / 2.0 + x2 * y2 / 3.0);

	return vec3(x, y, z);
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
/*
    +--+-------------+------------+-------------------+------------+--------------------------------+--------------------------------+
    |b |b >> 1 => b1 |b & 1 => b2 |(b1 ^ b2) ^ 1 => c |b1 ^ 1 => s |sqrt2_2 * (-1 + 2 * c)  => cos_ |sqrt2_2 * (-1 + 2 * s)  => sin_ |
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

uvec2 leftShift64(uvec2 nodeID, uint shift)
{
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

uvec2 rightShift64(uvec2 nodeID, uint shift)
{
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

vec3 localPointToWorldPoint(vec2 point, vec3 vertexA, vec3 vertexB, vec3 vertexC) {
    return radius * point_on_cube_to_point_on_sphere(vertexA * point.x + vertexB * point.y + vertexC * (1 - point.x - point.y));
}

vec3 calculateCentroid(Triangle t) {
    return (t.v0 + t.v1 + t.v2) / 3;
}

float calulateDistance(vec3 a, vec3 b) {
    return distance(a, b);
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
    vec2 point_a = (vec3(0, 0, 1) * transform_matrix).xy;
    vec2 point_b = (vec3(0, 1, 1) * transform_matrix).xy;
    vec2 point_c = (vec3(1, 0, 1) * transform_matrix).xy;
    vec2 point_d = (vec3(0.5, 0.5, 1) * transform_matrix).xy;

    uint vertexBaseIndex = meshPolygonID * 5;
    uint vertexKeyA = rootID;
    uint vertexKeyB = ((rootID >> 1) ^ 1) + ((rootID & 1) << 1);

    vec3 base_Triangle_a = (position_list[vertexBaseIndex + vertexKeyA + 1]).xyz;
    vec3 base_Triangle_b = (position_list[vertexBaseIndex + vertexKeyB + 1]).xyz;
    vec3 base_Triangle_c = (position_list[vertexBaseIndex]).xyz;

    Triangle t;
    t.v0 = localPointToWorldPoint(point_a, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.v1 = localPointToWorldPoint(point_b, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.v2 = localPointToWorldPoint(point_c, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.origin = localPointToWorldPoint(point_d, base_Triangle_a, base_Triangle_b, base_Triangle_c);

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

float f_angle(vec3 from, vec3 to)
{
    return atan(cross(from, to).length(), dot(from, to));
}

void calculateFrustumPlanes(out vec4 frustumPlanes[6]) {
    float tanHalfFOV = tan(CameraFOV * 0.5);

    vec3 forward = vec3(cameraToWorld[2]);
    vec3 up = vec3(cameraToWorld[1]);
    vec3 right = cross(up, forward);

    vec3 nearCenter = vec3(cameraToWorld[3]) + forward * CameraNearPlane;
    vec3 farCenter = vec3(cameraToWorld[3]) + forward * CameraFarPlane;

    // Calculate the normals pointing inside the frustum
    frustumPlanes[0] = vec4(normalize(cross(up, farCenter + right * tanHalfFOV * CameraFarPlane)), 0.0); // Right plane
    frustumPlanes[1] = vec4(normalize(cross(farCenter - right * tanHalfFOV * CameraFarPlane, up)), 0.0); // Left plane
    frustumPlanes[2] = vec4(normalize(cross(right, farCenter + up * tanHalfFOV * CameraFarPlane)), 0.0); // Top plane
    frustumPlanes[3] = vec4(normalize(cross(farCenter - up * tanHalfFOV * CameraFarPlane, right)), 0.0); // Bottom plane
    frustumPlanes[4] = vec4(forward, dot(nearCenter, forward)); // Near plane
    frustumPlanes[5] = vec4(-forward, -dot(farCenter, forward)); // Far plane
}

bool isInFrustum(Triangle t) {
    vec4 frustumPlanes[6];
    calculateFrustumPlanes(frustumPlanes);

    bool inFrustum = true;
    for (int i = 0; i < 6; i++) {
        if (dot(vec4(t.v0, 1), frustumPlanes[i]) < 0.0 || dot(vec4(t.v1, 1), frustumPlanes[i]) < 0.0 || dot(vec4(t.v2, 1), frustumPlanes[i]) < 0.0) {
            inFrustum = false;
            break;
        }
    }

    return inFrustum;
}

float angleBetweenVectors(vec3 u, vec3 v) {
    // Compute the dot product of u and v
    float dotProduct = dot(u, v);

    // Compute the magnitudes of u and v
    float magnitudeU = length(u);
    float magnitudeV = length(v);

    // Calculate the cosine of the angle
    float cosTheta = dotProduct / (magnitudeU * magnitudeV);

    // Clamp the cosine value to the range [-1, 1] to avoid any numerical issues
    cosTheta = clamp(cosTheta, -1.0, 1.0);

    // Return the angle in radians using the arccosine function
    return acos(cosTheta);
}

float sphericalDistance(vec3 a, vec3 b, float radius) {
    float dot_product = dot(normalize(a), normalize(b));
    dot_product = clamp(dot_product, -1.0, 1.0); // Clamp value to avoid any potential numerical issues.
    float angle = acos(dot_product);
    float arcLength = radius * angle; // Calculate the arc length by multiplying the angle by the sphere's radius.
    return arcLength;
}

void main() {
    uint leaf_count = uint(atomicExchange(primCount_full[read_index], primCount_full[read_index]));
    uint invocationID = gl_GlobalInvocationID.x;
    if (invocationID >= leaf_count)
        return;
    
    uvec4 key = read_list[invocationID];
    Triangle p_triangle = leafSpaceToWorldSpace(getParentKey(key));
    Triangle b_triangle = leafSpaceToWorldSpace(key);

    float current_LOD = getLevelInKey(key.xy);

    float p_target_LOD = calculateLOD (
        calulateDistance(p_triangle.origin, cameraToWorld[3].xyz),
        CameraFOV, // Must be in radians
        subFactor
    );

    float k_target_LOD = calculateLOD (
        calulateDistance(b_triangle.origin, cameraToWorld[3].xyz),
        CameraFOV, // Must be in radians
        subFactor
    );

    if (k_target_LOD > current_LOD) { // subdivide
        uvec4 children[4] = getChildKeys(key);
        for (int i = 0; i < 4; i++) {
            uint idx = atomicAdd(primCount_full[write_index], 1);
            write_full_list[idx] = children[i];
            distance_values[idx] = k_target_LOD;
        }    
    } 

    else if (p_target_LOD < current_LOD - 1 && key.xy != uvec2(0, 1)) { // merging
        if (isUpperLeftChild(key.xy)) {
            uint idx = atomicAdd(primCount_full[write_index], 1);
            write_full_list[idx] = getParentKey(key);
            distance_values[idx] = p_target_LOD;
        } else 
            return;

    } else {
        uint idx = atomicAdd(primCount_full[write_index], 1);
        write_full_list[idx] = key;
        distance_values[idx] = k_target_LOD;
    }

    
    // vec4 clip_space = projectionMatrix * (cameraToWorld * vec4(p_triangle.origin, 1.0));


    // vec3 ndc = clip_space.xyz / clip_space.w;

    // bool inFrustum = (ndc.x >= -1.0 && ndc.x <= 1.0 &&
    //                   ndc.y >= -1.0 && ndc.y <= 1.0 &&
    //                 //   ndc.z >= 0.0 && ndc.z <= 1.0);

    // if (!inFrustum)
    //     return;

    // uint idx = atomicAdd(primCount_culled[write_index], 1);
    // write_culled_list[idx] = key;
    

    
}