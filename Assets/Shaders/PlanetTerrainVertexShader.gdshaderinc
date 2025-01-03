uniform float radius;
uniform float height_scale;
uniform int resolution;
uniform float maximum_lod;
uniform mat4 planet_transform_matrix;

uniform sampler2D height_map : repeat_disable, source_color;

uniform bool is_cube;
uniform bool is_culling;

uniform bool is_morphing;
uniform vec2 morph_range;

uniform vec3 camera_position;
uniform float fovy;
uniform float sub_factor;

const vec3 normals[6] = vec3[6](
    vec3(1.0, 0.0, 0.0),
    vec3(-1.0, 0.0, 0.0),
    vec3(0.0, 1.0, 0.0),
    vec3(0.0, -1.0, 0.0),
    vec3(0.0, 0.0, 1.0),
    vec3(0.0, 0.0, -1.0)
);

#include "functions.gdshaderinc"

varying flat uvec4 key;
varying flat uint flags;
varying vec3 cube_local_position;
varying vec3 sphere_local_position;
varying float morph;
varying float elevation;

void vertex() {
	key = floatBitsToUint(INSTANCE_CUSTOM);
	flags = key.w >> 29u;
	key.w &= 0xFu;
	vec3 normal = normals[key.z];
	VERTEX = (VERTEX + 1.0) / 2.0;
	
	vec2 quad_position = leaf_space_to_quad_space(VERTEX, key.xy);
	vec3 local_position = quad_space_to_polygon_space(quad_position, key.z, key.w);
	local_position = is_cube ? local_position : point_on_cube_to_point_on_sphere(local_position);
	morph = is_morphing ? calculate_morph(local_position, normal, key.xy) : 0.0;

	vec2 morphed_quad_position = leaf_space_to_quad_space(morph_vertex(VERTEX.xy, morph, int(key.z)), key.xy);
	vec3 morphed_local_position = quad_space_to_polygon_space(morphed_quad_position, key.z, key.w);

	cube_local_position = morphed_local_position;
	sphere_local_position = point_on_cube_to_point_on_sphere(morphed_local_position);
	morphed_local_position = is_cube ? cube_local_position : sphere_local_position;

	vec3 morphed_world_position = polygon_space_to_world_space(morphed_local_position, normal, false);

	elevation = textureLod(height_map, point_on_sphere_to_uv(morphed_local_position), 0.0).r;
	NORMAL = (vec4(morphed_local_position, 1) * get_rotation()).xyz;
	VERTEX = morphed_world_position + NORMAL * height_scale * elevation;
}