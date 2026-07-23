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

vec3 to_checkers(vec2 uv, float size) {
	vec2 grid = floor(uv * size);
	vec3 color = mod(grid.x + grid.y, 2.0) == 0.0
	? vec3(1.0, 0.0, 0.0)
	: vec3(0.0, 1.0, 0.0);
    return color;
}

vec3 to_cells(vec2 uv, float size) {
	vec2 cell_uv = floor(uv * size);
	return vec3(cell_uv / (size - 1.0), 0.0);
}

vec3 displayBranches() {
	int root_id = int(key.z);
	int msb = find_msb_64(key.xy);
	int branching_index = int(get_branching(key.xy, (msb / 2) - 1, msb));
	return hsl_to_rgb(vec3(float(lod) / 16.0, 1.0, float(branching_index + 1) / 4.0));
}

void calculate_normal(vec3 base_normal, float height) {
	vec3 tangent = normalize(cross(base_normal, vec3(0, 1, 0)));
	vec3 bitangent = cross(base_normal, tangent);

}

vec3 color_base_normal_and_adjecenies(vec2 uv, float coverage, int normal_id) {
	vec3 color = vec3(0);

	int[4] adjecent_normals = get_adjecent_normal_ids(normal_id);

	float left = uv.x;
	float right = 1.0 - uv.x;
	float bottom = uv.y;
	float top = 1.0 - uv.y;

	float d = min(min(left, right), min(bottom, top));
	if (d < coverage) {
		if (left == d)
			color = normals_to_colors[adjecent_normals[0]];
		else if (bottom == d)
			color = normals_to_colors[adjecent_normals[1]];
		else if (right == d)
			color = normals_to_colors[adjecent_normals[2]];
		else if (top == d)
			color = normals_to_colors[adjecent_normals[3]];
	} else {
		color = normals_to_colors[normal_id];
	}
	return color;
}

bool in_bounds(vec3 p) { return max(abs(p.x), max(abs(p.y), abs(p.z))) <= 1.0; }

vec3 compute_sphere_normal_from_heightmap( vec2 uv, int normal_id, vec3 vertex_position, int lod_index) {
	int[4] adjecent_normals_ids = get_adjecent_normal_ids(normal_id);
	float step_amount = 0.0001;
	vec3 normal = normals[normal_id];
	float[4] heights;
	for (int i = 0; i < 4; i++) {
		vec3 adjecent_normal = normals[adjecent_normals_ids[i]];
		vec3 new_cube_position = vertex_position + adjecent_normal * step_amount;
		int selected_normal_id = normal_id;
		if (!in_bounds(new_cube_position)) {
			new_cube_position = vertex_position - normal * step_amount;
			selected_normal_id = adjecent_normals_ids[i];
		}
		vec2 uv = get_cube_uv(selected_normal_id, new_cube_position);
		vec3 tile_uv = get_tile_uv(terrain_indirection_table, lod_index, tile_size, uv, total_texture_subdivisions, selected_normal_id);
			heights[i] = get_tile(height_map_tile_cache, tile_uv).r;
	}
	float hL = heights[0];
	float hR = heights[1];
	float hU = heights[2];
	float hD = heights[3];

	float dx = hR - hL;
	float dy = hU - hD;

	return vec3(dx, dy, 0);
}

void fragment() {
	vec3 normal = normals[key.z];
	int normal_id = int(key.z);
	int lod_index = lod_to_mip_map[lod];

	vec3 tile_uv = get_tile_uv(terrain_indirection_table, lod_index, tile_size, vertex_uv, total_texture_subdivisions, int(key.z));

	if (render_tile_uvs)
	{
		ALBEDO = vec3(tile_uv.xy, 0);
	}
	else
	{
		ALBEDO = pow(get_tile(albedo_tile_cache, tile_uv).xyz, vec3(2.2));
	}

	int[4] adjecent_normals_ids = get_adjecent_normal_ids(normal_id);
	float step_amount = 0.0001;
	float[4] heights;

	vec3 cube_local_position = get_cube_point(normal_id, vertex_uv);
	//ALBEDO = compute_sphere_normal_from_heightmap(vertex_uv, normal_id, cube_local_position, lod_index);
	//vec3 sphere_local_position = point_on_cube_to_point_on_sphere(cube_local_position);
	/*
	vec3 color = vec3(1, 0, 0);
	if (lod % 2 == 0)
	{
		color = vec3(0, 0, 1);
	}
	int bit = find_msb_64(key.xy);
	ALBEDO = hsl_to_rgb(vec3(float(lod) / 16.0, 1, 1));
	*/
}
