#include "tesselation_functions.gdshaderinc"
#include "functions.gdshaderinc"

uniform float radius;
uniform float height_scale;

uniform int resolution;
uniform float maximum_lod;
uniform float minimum_lod;
uniform int tile_size;
uniform mat4 planet_transform_matrix;

uniform sampler2DArray height_map_tile_cache : repeat_disable, source_color, filter_linear;
uniform sampler2DArray terrain_indirection_table : repeat_disable, source_color, filter_nearest;


uniform bool is_cube;

uniform bool is_morphing;
uniform vec2 morph_range;
uniform vec3 camera_position;
uniform float fovy;
uniform float sub_factor;

varying flat uvec4 key;
varying flat uint flags;
varying float morph;

varying float elevation;
varying vec2 vertex_uv;

varying flat int lod;

uniform int total_texture_subdivisions;
uniform int[32] lod_to_mip_map;


void vertex() {
	key = floatBitsToUint(INSTANCE_CUSTOM);
	flags = key.w >> 29u;
	key.w &= 0xFu;

	mat3 quadtree_space = leaf_space_to_quadtree_space(key.xy);
	mat3 polygon_space = quadtree_space_to_polygon_space(key.z, key.w);

	vec2 quad_position = get_quadtree_space_point(VERTEX.xy, quadtree_space);
	vec3 cube_local_position = get_polygon_space_point(quad_position, polygon_space);
	vec3 sphere_local_position = point_on_cube_to_point_on_sphere(cube_local_position);
	vec3 local_point = is_cube ? cube_local_position : sphere_local_position;

	if (is_morphing) {
		morph = calculate_morph(local_point, key.xy, morph_range, camera_position, planet_transform_matrix, fovy, sub_factor, radius, maximum_lod, minimum_lod);
		vec2 morphed_vertex = morph_vertex(VERTEX.xy, morph, resolution);
		
		vec2 morphed_quad_position = get_quadtree_space_point(morphed_vertex, quadtree_space);
		vec3 morphed_cube_local_position = get_polygon_space_point(morphed_quad_position, polygon_space);
		vec3 morphed_sphere_local_position = point_on_cube_to_point_on_sphere(morphed_cube_local_position);

		local_point = is_cube ? morphed_cube_local_position : morphed_sphere_local_position;
		vertex_uv = get_cube_uv(int(key.z), morphed_cube_local_position);
		
		lod = int(calculate_lod_to_cam(morphed_sphere_local_position, camera_position, planet_transform_matrix, fovy, sub_factor, radius, maximum_lod, minimum_lod));

	}
	else {
		vertex_uv = get_cube_uv(int(key.z), cube_local_position);
		lod = int(calculate_lod_to_cam(sphere_local_position, camera_position, planet_transform_matrix, fovy, sub_factor, radius, maximum_lod, minimum_lod));

	}
	
	vec3 world_position = get_world_space_point(local_point, planet_transform_matrix);
	
	int mip_index = lod_to_mip_map[lod];
	vec3 tile_uv = get_tile_uv(terrain_indirection_table, mip_index, tile_size, vertex_uv, total_texture_subdivisions, int(key.z));
	elevation = get_tile(height_map_tile_cache, tile_uv).r;


	NORMAL = (vec4(local_point, 1) * get_rotation_from_matrix(planet_transform_matrix)).xyz;
	VERTEX = world_position + NORMAL * height_scale * elevation;
}