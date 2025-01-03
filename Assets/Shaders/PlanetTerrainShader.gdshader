shader_type spatial;

#include "PlanetTerrainVertexShader.gdshaderinc"

uniform float border_size;
uniform float center_size;
uniform int total_texture_subdivisions;
uniform float grid_size;
uniform int desired_chunk_size;

uniform sampler2D albedo_map : repeat_disable, source_color;
uniform sampler2D normal_map : repeat_disable, source_color;
uniform sampler2D tile_cache : repeat_disable, source_color;
uniform sampler2DArray indirection_table : repeat_disable, source_color;

uniform bool is_colorize_lod;
uniform bool is_texture_1D;

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

vec3 hslToRgb(vec3 c) {
	vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), c.y);
}

uniform float normal_strength;

void fragment() {
	float msb = float(get_lod_of_key(key.xy)) / 12.0;
	vec3 debug_color = hslToRgb(vec3(msb, 1.0, 0.5));
	float child = float(key.y & 3u);

	vec2 sphere_uv_point = point_on_sphere_to_uv(sphere_local_position);
	vec3 surface_texture = textureLod(albedo_map, is_texture_1D ? vec2(elevation, 0.0) : sphere_uv_point, 0.0).rgb;

	vec3 normal = normals[key.z];
	vec2 polygon_uv = get_polygon_uv(cube_local_position, normal);

	int lod = int(calculate_lod_to_cam(sphere_local_position, normal, false));

	// clamp lods out of bounds
	int lod_ratio = min(lod / total_texture_subdivisions, total_texture_subdivisions - 1);

	float lod_grid_size = grid_size / pow(2.0, float(total_texture_subdivisions - 1 - lod_ratio));
	int texture_idx = total_texture_subdivisions * int(key.z) + lod_ratio;

	vec2 cell_uv = (fract(polygon_uv * lod_grid_size));
	vec3 cell_id = vec3((floor(polygon_uv * lod_grid_size) + vec2(0.5)) / lod_grid_size, float(texture_idx));

	vec3 indirection_id = textureLod(indirection_table, cell_id, 0.0).xyz;

	vec2 cell_uv_min = indirection_id.xy;
	vec2 cell_uv_max = cell_uv_min.xy + (1.0 / (grid_size));

	vec2 grid_uv = cell_uv_min + (cell_uv_max - cell_uv_min) * cell_uv;

	vec2 cell = floor(polygon_uv * grid_size);
	debug_color = vec3(cell_uv, 0);
	
	NORMAL = height_to_normal(elevation, normal_strength, VERTEX, NORMAL);
	ALBEDO = is_colorize_lod ? debug_color : surface_texture;
	//ALBEDO = vec3(0);
	//if (get_normal_id(init_normal) == 2)
	//{
		//ALBEDO = is_colorize_lod ? textureLod(tile_cache, grid_uv, 0.0).rgb : vec3(cell_uv_min, 0);
//
	//}

}
