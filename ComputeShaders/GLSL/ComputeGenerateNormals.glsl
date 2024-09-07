#[compute]
#version 450
#define PI 3.14159265359

// Sebastian Lague
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;
layout(set = 0, binding = 0, std430) buffer restrict heightMapData
{
    float radius;
    float heightScale;
};

layout(set = 0, binding = 1, r8) restrict uniform readonly image2D heightMap;
layout(set = 0, binding = 2, r8) restrict uniform readonly image2D heightGradient;
layout(set = 0, binding = 3, rgba32f) restrict uniform writeonly image2D normalMap;

ivec2 wrapIndex(ivec2 index) {
    ivec2 image_size = imageSize(heightMap);
	index.x = (index.x + image_size.x) % (image_size.x);
    
	return index;
}

float CalculateWorldHeight(ivec2 index) {
	float height = imageLoad(heightMap, index).r;
	return radius + height * 1;
}

vec3 uv_to_point_on_sphere(vec2 uv) {
    float longitude = (uv.x - 0.5) * 2 * PI;
    float latitude = (uv.y - 0.5) * PI;
    
    float y = sin(latitude);
	float r = cos(latitude);
	float x = -sin(longitude) * r;
	float z = cos(longitude) * r;
    
    return vec3(x, y, z);
}

vec3 CalculateWorldPoint(ivec2 index) {
    ivec2 image_size = imageSize(heightMap);
	vec2 uv = vec2(index) / vec2(image_size.x - 1, image_size.y - 1);

	float height = CalculateWorldHeight(index);
    
	vec3 p = uv_to_point_on_sphere(uv);
	return p * height;
}

void CalculateHeightData() {
    uvec2 invocationID = gl_GlobalInvocationID.xy;
    
    vec3 pos = CalculateWorldPoint(wrapIndex(ivec2(invocationID + ivec2(0, 0) )));
    vec3 posNorth = CalculateWorldPoint(wrapIndex(ivec2(invocationID + ivec2(0, 1) )));
    vec3 posSouth = CalculateWorldPoint(wrapIndex(ivec2(invocationID + ivec2(0, -1))));
    vec3 posWest =  CalculateWorldPoint(wrapIndex(ivec2(invocationID + ivec2(-1, 0))));
    vec3 posEast =  CalculateWorldPoint(wrapIndex(ivec2(invocationID + ivec2(1, 0) )));

    vec3 dirNorth = normalize(posNorth - posSouth);
    vec3 dirEast = normalize(posEast - posWest);
    vec3 normalVector = normalize(cross(dirEast, dirNorth));
  
    mat3 TBN = transpose(mat3(dirNorth, dirEast, normalVector));

    vec3 tangentSpaceNormal = TBN * normalize(-pos);

    tangentSpaceNormal = (tangentSpaceNormal + vec3(1.0)) * 0.5;
    // tangentSpaceNormal.y = 1 - tangentSpaceNormal.y;

    imageStore(normalMap, ivec2(invocationID), vec4(tangentSpaceNormal, 1));
}

void main() {
    CalculateHeightData();
}