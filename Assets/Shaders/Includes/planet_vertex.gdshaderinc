#include "tesselation_functions.gdshaderinc"
#include "functions.gdshaderinc"

uniform float radius;
uniform float height_scale;

uniform int resolution;
uniform float maximum_lod;
uniform float minimum_lod;
uniform mat4 planet_transform_matrix;

uniform float current_lod;

uniform sampler2DArray height_map_tile_cache : repeat_disable, source_color, filter_linear;
uniform sampler2DArray terrain_indirection_table : repeat_disable, source_color, filter_nearest;

uniform bool is_cube;
uniform bool is_morphing;
uniform bool render_tile_uvs;
uniform bool show_in_cache;
uniform bool show_keys;
uniform bool show_indirection_age;
uniform uint total_tile_slots;

uniform vec2 morph_range;
uniform vec3 camera_position;
uniform float fovy;
uniform float sub_factor;

varying flat uvec4 key;
varying flat uint flags;
varying float morph;

varying float elevation;
varying vec2 plane_vertex_uv;

varying flat int lod;

uniform int low_resolution_mip_count;
uniform int high_resolution_mip_count;
uniform int[32] lod_to_mip_map;

void vertex() {
	key = floatBitsToUint(INSTANCE_CUSTOM);
	flags = key.w >> 29u;
	key.w &= 0xFu;
	
	float vertex_spacing = 1.0 / float(resolution - 1);
	if (VERTEX.x == 0.0 && (flags == 0u || flags == 2u))
	{
		int vertex_index = int(VERTEX.y * float(resolution - 1));
		if ((vertex_index & 1) == 1)
			VERTEX.y -= vertex_spacing;
	} else if (VERTEX.y == 0.0 && (flags == 1u || flags == 3u))
	{
		int vertex_index = int(VERTEX.x * float(resolution - 1));
		if ((vertex_index & 1) == 1)
			VERTEX.x -= vertex_spacing;
	}

	mat3 quadtree_space = leaf_space_to_quadtree_space(key.xy);
	mat3 polygon_space = quadtree_space_to_polygon_space(key.z, key.w);

	vec2 quad_position = get_quadtree_space_point(VERTEX.xy, quadtree_space);
	vec3 cube_local_position = get_polygon_space_point(quad_position, polygon_space);
	vec3 sphere_local_position = point_on_cube_to_point_on_sphere(cube_local_position);
	vec3 local_point = is_cube ? cube_local_position : sphere_local_position;
	
	if (is_morphing && false) {
		morph = calculate_morph(local_point, key.xy, morph_range, camera_position, planet_transform_matrix, fovy, sub_factor, radius, maximum_lod, minimum_lod);
		vec2 morphed_vertex = morph_vertex(VERTEX.xy, morph, resolution);
		
		vec2 morphed_quad_position = get_quadtree_space_point(morphed_vertex, quadtree_space);
		vec3 morphed_cube_local_position = get_polygon_space_point(morphed_quad_position, polygon_space);
		vec3 morphed_sphere_local_position = point_on_cube_to_point_on_sphere(morphed_cube_local_position);

		local_point = is_cube ? morphed_cube_local_position : morphed_sphere_local_position;
		plane_vertex_uv = get_plane_uv(int(key.z), morphed_cube_local_position);
	}
	else {
		plane_vertex_uv = get_plane_uv(int(key.z), cube_local_position);
	}
	
	lod = get_lod_of_key(key.xy);
	int non_negative_mip_index = lod_to_mip_map[lod] + high_resolution_mip_count;
	int total_resolution_mip_count = low_resolution_mip_count + high_resolution_mip_count;
	float lod_scale = pow(2.0, float(int(current_lod)));
	
	vec3 world_position = get_world_space_point(local_point, planet_transform_matrix);
	vec3 tile_uv = get_tile_uv(terrain_indirection_table, plane_vertex_uv, int(key.z), low_resolution_mip_count, high_resolution_mip_count);
	elevation = get_tile(height_map_tile_cache, tile_uv).r;
	
	NORMAL = (vec4(local_point, 1) * get_rotation_from_matrix(planet_transform_matrix)).xyz;
	VERTEX = world_position + NORMAL * height_scale * elevation;
	//VERTEX *= lod_scale;
}