#[compute]
#version 450
#define PI 3.14159265359

const vec3 normals[6] = vec3[6](
    vec3(1.0, 0.0, 0.0),
    vec3(-1.0, 0.0, 0.0),
    vec3(0.0, 1.0, 0.0),
    vec3(0.0, -1.0, 0.0),
    vec3(0.0, 0.0, 1.0),
    vec3(0.0, 0.0, -1.0)
);

// Sebastian Lague
layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0) uniform sampler2D InputImage;
layout(set = 0, binding = 1, rgba32f) uniform image3D CubeMap;

vec2 point_on_sphere_to_uv(vec3 p) {
	float longitude = atan(p.x, p.z);
	float latitude = asin(-p.y);
	float u = (longitude / PI + 1.0) * 0.5;
	float v = latitude / PI + 0.5;
	return vec2(u, v);
}

vec3 point_on_cube_to_point_on_sphere(vec3 p) {
	vec3 square = p * p;
	return p * sqrt(1.0 - (square.yxx + square.zzy) / 2.0 + square.yxx * square.zzy / 3.0);
}

vec3 to_cube_position(int normal_id, vec2 uv) {
    vec3 point = vec3(0);
    switch(normal_id) {
        case 0:
            point = vec3(1.0, 1.0 - uv.y, 1.0 - uv.x);
            point.yz = 2 * point.yz - 1;
            break;
        case 1:
            point = vec3(-1.0, 1.0 - uv.y, uv.x);
            point.yz = 2 * point.yz - 1;
            break;
        case 2:
            point = vec3(1.0 - uv.x, 1.0, 1.0 - uv.y);
            point.xz = 2 * point.xz - 1;
            break;
        case 3:
            point = vec3(uv.x, -1.0, 1.0 - uv.y);
            point.xz = 2 * point.xz - 1;
            break;
        case 4:
            point = vec3(uv.x, 1.0 - uv.y, 1.0);
            point.xy = 2 * point.xy - 1;
            break;
        case 5:
            point = vec3(1.0 - uv.x, 1.0 - uv.y, -1.0);
            point.xy = 2 * point.xy - 1;
            break;
    }
    return point;
}

void main()
{
    ivec3 invocation_id = ivec3(gl_GlobalInvocationID.xyz);
    vec3 direction = normals[invocation_id.z];
    vec2 faceUV = vec2(invocation_id.xy) / vec2(imageSize(CubeMap).xy);
    vec3 cube_position = to_cube_position(int(invocation_id.z), faceUV);
    vec3 sphere_position = point_on_cube_to_point_on_sphere(cube_position);
    vec2 uv = point_on_sphere_to_uv(sphere_position);
    vec4 color = texture(InputImage, uv);
    imageStore(CubeMap, invocation_id, color);
}