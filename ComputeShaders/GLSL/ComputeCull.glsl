#[compute]
#version 450
#extension GL_EXT_shader_atomic_float2 : require

#define sqrt2   1.414213562
#define PI      3.141592653

//Jad Khoury https://jadkhoury.github.io/files/MasterThesisFinal.pdf
layout(local_size_x = 32, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) buffer restrict AtomicCounterBuffer {
    uint primCount_full[3];
    uint primCount_culled[3];
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

layout(set = 0, binding = 5, std430) buffer restrict readonly BaseTriangles {
    vec4 base_triangles[];
};

layout(set = 0, binding = 6, std430) buffer restrict readonly ExternalData {
    mat4 viewProjectionMatrix;         // 16 * 4 = 64 bytes
    mat4 planetTransformMatrix;
    vec4 cameraPosition;
    float cameraFOV;     
    float subFactor;
    float heightScale;
    float max_lod;
    float radius;

    float bias1;
    float bias2;
    float padding[2];
};

layout(set = 0, binding = 7) uniform sampler2D heightMap;

layout(set = 0, binding = 8, std430) buffer restrict OutputBuffer { 
    float data[]; 
} output_buffer;

layout(set = 0, binding = 9, std430) buffer restrict readonly Culling { 
    bool culling;
    bool paddingz;
};

struct Triangle {
    vec3 v0; // (0, 0)
    vec3 v1; // (0, 1)
    vec3 v2; // (1, 0)

    vec3 origin; // (0.5, 0.5)

    vec3 xNeighbor; // (0.5, -0.5)
    vec3 yNeighbor; // (-0.5, 0.5)
};

vec3 pointOnCubeToPointOnSphere(vec3 p) {
    vec3 square = p * p;
	return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

vec2 point_on_sphere_to_UV(vec3 p) {
    p = normalize(p);
    float longitude = atan(p.x, p.z);
    float latitude = asin(-p.y);
    float u = (longitude / PI + 1) * 0.5;
    float v = latitude / PI + 0.5;
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

// vec3 polygonSpaceToObjectSpaceCubical(vec2 point, vec3 vertexA, vec3 vertexB, vec3 vertexC) {
//     return vertexA * point.x + vertexB * point.y + vertexC * (1 - point.x - point.y);
// }

vec3 quadtreeSpaceToPolygonSpace(vec2 point, vec3 vertexA, vec3 vertexB, vec3 vertexC) {
    return vertexA * point.x + vertexB * point.y + vertexC * (1 - point.x - point.y);
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
    vec2 point_a = (vec3(0.3, 0.3, 1) * transform_matrix).xy;
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
    t.v0 = quadtreeSpaceToPolygonSpace(point_v0, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.v1 = quadtreeSpaceToPolygonSpace(point_v1, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.v2 = quadtreeSpaceToPolygonSpace(point_v2, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    
    t.origin = quadtreeSpaceToPolygonSpace(point_a, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.xNeighbor = quadtreeSpaceToPolygonSpace(point_b, base_Triangle_a, base_Triangle_b, base_Triangle_c);
    t.yNeighbor = quadtreeSpaceToPolygonSpace(point_c, base_Triangle_a, base_Triangle_b, base_Triangle_c);

    return t;
}

Triangle leafSpaceToPolygonSpace(uvec4 key) { 
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

float getScale(float idx) {
	return 1.0 / pow(2, idx);
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
    float num = dist * fovy;
    float dom = sqrt2 * factor;
    return clamp(-log2(num/dom), -1, 31);
}

vec3 applyRotation(vec3 point){
    mat4 rotationOnly = mat4(
        vec4(planetTransformMatrix[0].xyz / length(planetTransformMatrix[0].xyz), 0), 
        vec4(planetTransformMatrix[1].xyz / length(planetTransformMatrix[1].xyz), 0), 
        vec4(planetTransformMatrix[2].xyz / length(planetTransformMatrix[2].xyz), 0),
        vec4(0, 0, 0, 1)
    );

    return (vec4(point, 1) * rotationOnly).xyz;
}

vec3 polygonSpaceToObjectSpace(vec3 point) {
    vec2 uv = point_on_sphere_to_UV(point);
    float height = texture(heightMap, uv).x;
    mat4 rotationOnly = mat4(
        vec4(planetTransformMatrix[0].xyz / length(planetTransformMatrix[0].xyz), 0), 
        vec4(planetTransformMatrix[1].xyz / length(planetTransformMatrix[1].xyz), 0), 
        vec4(planetTransformMatrix[2].xyz / length(planetTransformMatrix[2].xyz), 0),
        vec4(0, 0, 0, 1)
    );
    vec3 normal = (vec4(point, 1) * rotationOnly).xyz;
    vec3 objectPoint = (vec4(point, 1) * planetTransformMatrix).xyz;
    objectPoint += (normal * height * heightScale);
    return objectPoint;
}

vec3 polygonSpaceToObjectSpaceIgnoreHeight(vec3 point) {
    mat4 rotationOnly = mat4(
        vec4(planetTransformMatrix[0].xyz / length(planetTransformMatrix[0].xyz), 0), 
        vec4(planetTransformMatrix[1].xyz / length(planetTransformMatrix[1].xyz), 0), 
        vec4(planetTransformMatrix[2].xyz / length(planetTransformMatrix[2].xyz), 0),
        vec4(0, 0, 0, 1)
    );
    return (vec4(point, 1) * planetTransformMatrix).xyz;
}

float distanceFromCamIgnoreHeight(vec3 from) {
    return distance(polygonSpaceToObjectSpaceIgnoreHeight(from), cameraPosition.xyz);
}

float distanceFromCam(vec3 from) {
    return distance(polygonSpaceToObjectSpace(from), cameraPosition.xyz);
}

float calculateLODToCam(vec3 from) {

    return calculateLOD(
        distanceFromCam(pointOnCubeToPointOnSphere(from)),
        cameraFOV,
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

vec4 isTJunction(uvec4 key, Triangle parent_triangle, float lod) {
    uint b1b2 = key.y & 0x3;
    vec3 neighbour;

    if (key.xy == uvec2(0,1))
    {
        return vec4(0,0,0,0);
    }

    vec4 color;
    
    if ((b1b2 >> 1) == 0) { // Check Y neighbour
        neighbour = parent_triangle.yNeighbor;
        color = vec4(0,1,0,0);
    }
    else if ((b1b2 >> 1) == 1) { // Check X neighbour
        neighbour = parent_triangle.xNeighbor;
        color = vec4(1,0,0,0);
    }
    float neighbour_lod = calculateLODToCam(neighbour);
    return neighbour_lod < lod - 1 ? color : vec4(0,0,0,0);
}

uint getJunctionFlags(uvec4 key, Triangle parent_triangle, float lod) {
    uint b1b2 = key.y & 0x3;
    vec3 neighbour;

    if (key.xy == uvec2(0,1)) {
        return 4 << 29;
    }

    if ((b1b2 >> 1) == 0) // Check Y neighbour
        neighbour = parent_triangle.yNeighbor;
    else if ((b1b2 >> 1) == 1) // Check X neighbour
        neighbour = parent_triangle.xNeighbor;
    

    float neighbour_lod = calculateLODToCam(neighbour);

    if (neighbour_lod < lod - 1) {
        return b1b2 << 29;
    }
    return 4 << 29;
   
}

bool PointInFrustum(vec3 point) { 


    vec4 clipSpacePoint = viewProjectionMatrix * vec4(point, 1);
    vec3 ndcPoint = clipSpacePoint.xyz / clipSpacePoint.w;

    return ndcPoint.x >= -1.0 && ndcPoint.x <= 1.0 &&
           ndcPoint.y >= -1.0 && ndcPoint.y <= 1.0 &&
           ndcPoint.z >= -1.0 && ndcPoint.z <= 1.0;
}

vec3 getPlanetOrigin() {
    return planetTransformMatrix[3].xyz;
}

ivec3 getInitNormal(uint polygon_id) {
	uint vertexBaseIndex = polygon_id * 5u;
	return ivec3(base_triangles[vertexBaseIndex].xyz);
}

vec3 getCameraForward() {
    return normalize(-vec3(viewProjectionMatrix[0][2], viewProjectionMatrix[1][2], viewProjectionMatrix[2][2]));
}

bool InHorizon(vec3 point) {
    vec3 sphericalPoint = pointOnCubeToPointOnSphere(point);
    float distanceFromPlanet = distanceFromCamIgnoreHeight(getPlanetOrigin());
    float distanceFromHorizon = sqrt(pow(distanceFromPlanet, 2) - pow(radius, 2));
    vec3 toPlanetFromCam = getPlanetOrigin() - cameraPosition.xyz;
    vec3 objectPointIgnoreHeight = polygonSpaceToObjectSpaceIgnoreHeight(sphericalPoint);
    vec3 objectPoint = polygonSpaceToObjectSpace(sphericalPoint);
    vec3 toPointOnSphereFromCam = objectPointIgnoreHeight - cameraPosition.xyz;
    float angleOfHorizon = acos(distanceFromHorizon / distanceFromPlanet);
    float angleFromPointToCamToPlanet = acos(dot(normalize(toPointOnSphereFromCam), normalize(toPlanetFromCam)));
    
    // if within view
    // if within horizon's circle
    // if within horizon
    bool b0 = PointInFrustum(objectPointIgnoreHeight) || PointInFrustum(objectPoint);
    bool b1 = (distanceFromHorizon + (radius/2.0) * bias1) >= length(toPointOnSphereFromCam);
    bool b2 = angleFromPointToCamToPlanet + bias2 <= angleOfHorizon;
    if ((b0 && b1 && b2) || (b0 && b1) || (b0 && !b2))
        return true;
    return false;
}

bool TriangleInFrustum(Triangle triangle) {
    return InHorizon(triangle.v0) || InHorizon(triangle.v1) || InHorizon(triangle.v2) || InHorizon(triangle.origin);
}

void set_multimesh_data(uint index, uvec4 key) {
    output_buffer.data[20 * index + 0] = 1.0;
    output_buffer.data[20 * index + 1] = 0.0;
    output_buffer.data[20 * index + 2] = 0.0;
    output_buffer.data[20 * index + 3] = 0.0;

    output_buffer.data[20 * index + 4] = 0.0;
    output_buffer.data[20 * index + 5] = 1.0;
    output_buffer.data[20 * index + 6] = 0.0;
    output_buffer.data[20 * index + 7] = 0.0;

    output_buffer.data[20 * index + 8] = 0.0;
    output_buffer.data[20 * index + 9] = 0.0;
    output_buffer.data[20 * index + 10] = 1.0;
    output_buffer.data[20 * index + 11] = 0.0;

    // Not currently using
    output_buffer.data[20 * index + 12] = 1.0;
    output_buffer.data[20 * index + 13] = 0.0;
    output_buffer.data[20 * index + 14] = 0.0;
    output_buffer.data[20 * index + 15] = 1.0;

    // Setting the key data
    output_buffer.data[20 * index + 16] = uintBitsToFloat(key.x);
    output_buffer.data[20 * index + 17] = uintBitsToFloat(key.y);
    output_buffer.data[20 * index + 18] = uintBitsToFloat(key.z);
    output_buffer.data[20 * index + 19] = uintBitsToFloat(key.w);

    // 
}

void cull_key(uvec4 key, Triangle triangle, float lod) {

    if (culling && TriangleInFrustum(triangle)) {
        uint write_culled_index = atomicAdd(primCount_culled[write_index], 1);
        set_multimesh_data(write_culled_index, key);

        // if (paging)
        // {
            
        // }
    }
    else if (!culling)
    {
        uint write_culled_index = atomicAdd(primCount_culled[write_index], 1);
        set_multimesh_data(write_culled_index, key);
    }
    imageAtomicMax(GlobalKeyData, ivec2(0, 0), culling ? 0 : 1);
    // imageAtomicMax(GlobalKeyData, ivec2(0, 0), getLevelInKey(key.xy));
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

    Triangle triangle = leafSpaceToPolygonSpace(key);
    Triangle parent_triangle = leafSpaceToPolygonSpace(parent_key);
    Triangle grand_parent_triangle = leafSpaceToPolygonSpace(grand_parent_key);

    float current_LOD = getLevelInKey(key.xy);

    float parent_target_LOD = calculateLODToCam(parent_triangle.origin);
    float target_LOD = calculateLODToCam(triangle.origin);
  
    if (target_LOD > current_LOD && current_LOD < max_lod) { // subdivide
        uvec4 children_keys[4] = getChildKeys(key);
        for (int i = 0; i < 4; i++) {
            uint write_full_index = atomicAdd(primCount_full[write_index], 1);
            Triangle child_triangle = leafSpaceToPolygonSpace(children_keys[i]);
            
            children_keys[i].w |= getJunctionFlags(children_keys[i], triangle, current_LOD + 1);
            
            write_full_list[write_full_index] = children_keys[i];
     
            cull_key(children_keys[i], child_triangle, current_LOD + 1);

        }
    } else if (parent_target_LOD < current_LOD - 1 && current_LOD > 0) { // merging
        if (isUpperLeftChild(key.xy)) {
            uint write_full_index = atomicAdd(primCount_full[write_index], 1);

            parent_key.w |= getJunctionFlags(parent_key, grand_parent_triangle, current_LOD - 1);
            write_full_list[write_full_index] = parent_key;

            cull_key(parent_key, parent_triangle, current_LOD - 1);

        }
    } else {
        uint write_full_index = atomicAdd(primCount_full[write_index], 1);

        key.w |= getJunctionFlags(key, parent_triangle, current_LOD);
        write_full_list[write_full_index] = key;
        
     
        cull_key(key, triangle, current_LOD);
 
    }
}