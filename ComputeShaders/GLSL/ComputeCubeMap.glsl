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

layout(set = 0, binding = 0) uniform sampler2DArray InputImage;
layout(set = 0, binding = 1, rgba8) uniform image2DArray Plane;
layout(set = 0, binding = 2, std430) buffer restrict NormalID {
    uint normal_id;
};
layout(set = 0, binding = 3, std430) buffer restrict Data {
    ivec2 tile_dimension;
};

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
    vec3 direction = normals[normal_id];

    int plane_tile_x = invocation_id.z % 2;
    int plane_tile_y = invocation_id.z / 2;
    vec2 full_image_uv = vec2(invocation_id) / vec2(2 * imageSize(Plane).xy);
    full_image_uv += vec2(0.5) * vec2(plane_tile_x, plane_tile_y);

    vec3 cube_position = to_cube_position(int(normal_id), full_image_uv);
    vec3 sphere_position = point_on_cube_to_point_on_sphere(cube_position);

    vec2 full_image_uv_spherical = point_on_sphere_to_uv(sphere_position);
    int tile_x = min(int(floor(full_image_uv_spherical.x * 4)), tile_dimension.x - 1);
    int tile_y = min(int(floor(full_image_uv_spherical.y * 2)), tile_dimension.y - 1);
    int tile_index = tile_y * 4 + tile_x;

    vec2 tile_size = 1.0 / vec2(tile_dimension);
    float tile_u = (full_image_uv_spherical.x - tile_x * tile_size.x) / tile_size.x;
    float tile_v = (full_image_uv_spherical.y - tile_y * tile_size.y) / tile_size.y;

    vec4 color = texture(InputImage, vec3(tile_u, tile_v, tile_index));

    imageStore(Plane, ivec3(invocation_id), color);




}