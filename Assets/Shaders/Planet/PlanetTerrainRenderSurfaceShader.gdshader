shader_type spatial;

#include "res://Assets/Shaders/Includes/PlanetTerrainVertexShader.gdshaderinc"

uniform sampler2DArray albedo_tile_cache : repeat_disable, source_color, filter_linear;
uniform float normal_strength;
uniform bool render_frame_buffer_on_top;
uniform bool render_padded_uvs;
uniform bool render_tile_uvs;

vec3 height_to_normal(float height, float strength, vec3 vertex, vec3 normal) {
	vec3 d_x = dFdx(vertex);
	vec3 d_y = dFdy(vertex);

	vec3 cross_x = cross(normal, d_x);
	vec3 cross_y = cross(d_y, normal);

	float d_t = dot(d_x, cross_y);
	float sign = d_t < 0.0 ? -1.0 : 1.0;
	float surface = sign / max(1.192093e-12, abs(d_t));

	float d_hdx = dFdx(height);
	float d_hdy = dFdy(height);

	vec3 surface_gradient = surface * (d_hdx * cross_y + d_hdy * cross_x);

	return normalize(normal - strength * surface_gradient);
}

vec3 hsl_to_rgb(vec3 c) {
	vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), c.y);
}

float checker(vec2 _uv, float size) {
    vec2 grid = floor(_uv * size);
    return mod(grid.x + grid.y, 2.0);
}

void fragment() {
	//vec3 debug_color = hsl_to_rgb(vec3(float(lod) / 16.0, 1.0, 0.5));
	vec3 normal = normals[key.z];
	int normal_id = get_normal_id(ivec3(normal));
	//NORMAL = height_to_normal(elevation, normal_strength, VERTEX, NORMAL);


	int lod = int(calculate_lod_to_cam(morphed_sphere_local_position, camera_position, planet_transform_matrix, fovy, sub_factor, radius, maximum_lod));
	int lod_index = lod_to_mip_map[lod];

	vec3 tile_uv = vec3(0);
	//if (render_padded_uvs)
	//{
	tile_uv = get_tile_uv(terrain_indirection_table, lod_index, tile_padding, tile_size, vertex_uv, total_texture_subdivisions, int(key.z));

	//} else {
		//tile_uv = get_tile_uv(terrain_indirection_table, lod_index, 0, vertex_uv, total_texture_subdivisions, int(key.z));
	//}
	if (render_tile_uvs)
	{
		ALBEDO = vec3(tile_uv.xy, 0);
	}
	else
	{
		ALBEDO = pow(get_tile(albedo_tile_cache, tile_uv).xyz, vec3(2.2));
	}
	
	//vec2 wave = 0.5 + 0.5 * sin( (vertex_uv * 40.0) + TIME * 4.0);
	//vec3 uv_color = vec3(vertex_uv, 0.0);
	//ALBEDO = mix(uv_color, vec3(wave, 1.0), 0.5);
	//ALBEDO = vec3(tile_uv.xy, 0);

	//ALBEDO = pow(get_tile(height_map_tile_cache, tile_uv).xyz, vec3(2.2));
	//ALBEDO = vec3(vertex_uv.xy, 0);
	//vec3 a = get_sphere_normal(height_map_tile_cache, vec3(vertex_uv, float(key.z)), normal_id, normal_strength, get_rotation_from_matrix(planet_transform_matrix) );
	//ALBEDO = a;
	//ALBEDO = point_to_point_on_cube(normal_id, vertex_uv);

	//ALBEDO = vec3(dot(NORMAL, a), 0, 0);
 	//float lod_grid_size = grid_size / pow(2.0, float(lod_index));
    //vec2 cell_uv = floor(uv * lod_grid_size) / lod_grid_size;

	//ALBEDO = render_frame_buffer_on_top ? mix(ALBEDO, vec3(cell_uv, 0), 0.75) : ALBEDO;
	//
	//float checker_value = checker(uv, lod_grid_size);
	//vec3 checker_color = mix(ALBEDO, ALBEDO * 0.5, checker_value);
	//ALBEDO = render_frame_buffer_on_top ? mix(checker_color, vec3(cell_uv, 0), 0.75) : checker_color;
}
