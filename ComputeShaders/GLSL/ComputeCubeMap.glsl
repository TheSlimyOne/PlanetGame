#[compute]
#version 450
#define PI 3.14159265359

// Sebastian Lague
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 0) uniform sampler2D InputImage;
layout(set = 0, binding = 1, rgba32f) uniform image3D CubeMap;

vec3 toCubePosition(vec3 normal, vec2 uv)
{
    if (normal.x == 1 || normal.x == -1) return vec3(normal.x, uv.x, uv.y);
    if (normal.y == 1 || normal.y == -1) return vec3(uv.x, normal.y, uv.y);
    if (normal.z == 1 || normal.z == -1) return vec3(uv.x, uv.y, normal.z);
    return vec3(-1);
}

vec2 pointOnSphereToUV(vec3 p) {
	float longitude = atan(p.x, p.z);
	float latitude = asin(-p.y);
	float u = (longitude / PI + 1.0) * 0.5;
	float v = latitude / PI + 0.5;
	return vec2(u, v);
}

vec3 pointOnCubeToPointOnSphere(vec3 p) {
	vec3 square = p * p;
	return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

void main()
{
    ivec3 invocationID = ivec3(gl_GlobalInvocationID.xyz);
    
    vec3 direction = imageLoad(CubeMap, invocationID).xyz;
    vec2 faceUV = vec2(invocationID.xy) / vec2(imageSize(CubeMap).xy);
    faceUV = 2 * faceUV - 1;
    vec3 cubePosition = toCubePosition(direction, faceUV);
    vec3 spherePosition = pointOnCubeToPointOnSphere(cubePosition);
    vec2 uv = pointOnSphereToUV(spherePosition);
    vec4 color = texture(InputImage, uv);
    // color = vec4(uv, 0, 0);
    imageStore(CubeMap, invocationID, color);
}