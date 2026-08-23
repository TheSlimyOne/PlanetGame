shader_type spatial;

#include "res://Assets/Shaders/Includes/planet_vertex.gdshaderinc"

uniform sampler2DArray albedo_tile_cache : repeat_disable, source_color, filter_linear;

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
	int key_lod = get_lod_of_key(key.xy);
	return hsl_to_rgb(vec3(float(key_lod) / 16.0, 1.0, float(branching_index + 1) / 4.0));
}

void calculate_normal(vec3 base_normal, float height) {
	vec3 tangent = normalize(cross(base_normal, vec3(0, 1, 0)));
	vec3 bitangent = cross(base_normal, tangent);

}

vec3 color_base_normal_and_adjecenies(vec2 uv, float coverage, int normal_id) {
	vec3 color = vec3(0);

	ivec4 adjecent_normals = ADJACENT_NORMAL_IDS[normal_id];

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
void fragment() {
	int normal_id = int(key.z);
	int total_resolution_mip_count = low_resolution_mip_count + high_resolution_mip_count;
	int non_negative_mip_index = lod_to_mip_map[lod] + high_resolution_mip_count;

	vec2 adjusted_uv = clamp(plane_vertex_uv, vec2(0.0), vec2(1.0 - EPS));

	float grid_size = exp2(float(total_resolution_mip_count - 1));
	float lod_grid_size = grid_size / exp2(float(non_negative_mip_index));

	vec2 tile_coordinate = adjusted_uv * lod_grid_size;
	vec2 tile_local_uv = fract(tile_coordinate);
	vec2 grid_uv = floor(tile_coordinate) / lod_grid_size;

	int layer = total_resolution_mip_count * normal_id + non_negative_mip_index;
	
	ivec3 indirection_index = get_indirection_index(
		adjusted_uv,
		normal_id,
		low_resolution_mip_count,
		high_resolution_mip_count
	);

	uvec4 indirection_data = floatBitsToUint(texelFetch(
		terrain_indirection_table,
		indirection_index,
		0
	));

	vec3 tile_uv = get_tile_uv(
		terrain_indirection_table,
		plane_vertex_uv,
		normal_id,
		low_resolution_mip_count,
		high_resolution_mip_count
	);

	if (render_tile_uvs) {
		float depth = 1.0 - float(non_negative_mip_index) / float(total_resolution_mip_count - 1);
		vec3 output_color = vec3(1.0);

		switch (key.z) {
			case 0u: output_color = vec3(tile_local_uv.x, tile_local_uv.y, depth); break;
			case 1u: output_color = vec3(tile_local_uv.y, tile_local_uv.x, depth); break;
			case 2u: output_color = vec3(depth, tile_local_uv.x, tile_local_uv.y); break;
			case 3u: output_color = vec3(depth, tile_local_uv.y, tile_local_uv.x); break;
			case 4u: output_color = vec3(tile_local_uv.x, depth, tile_local_uv.y); break;
			case 5u: output_color = vec3(tile_local_uv.y, depth, tile_local_uv.x); break;
		}

		float edge_threshold = 1.0 / 64.0;
		bool is_tile_edge = tile_uv.x < edge_threshold || tile_uv.x > 1.0 - edge_threshold || tile_uv.y < edge_threshold || tile_uv.y > 1.0 - edge_threshold;

		ALBEDO = is_tile_edge ? vec3(0.5) : output_color;
	}
	else if (show_indirection_age) {
		const float TOTAL_INDIRECTION_SLOTS = 512.0;

		float slot = float(indirection_data.r);
		float age = slot / float(total_tile_slots);

		ALBEDO = vec3(age, 0.0, 0.0);
	}
	else if (show_in_cache) {
		vec3 in_cache = is_tile_in_cache(
			terrain_indirection_table,
			non_negative_mip_index,
			plane_vertex_uv,
			normal_id,
			low_resolution_mip_count,
			high_resolution_mip_count
		) ? vec3(0.0, 1.0, 0.0) : vec3(0.0);

		vec3 real_color = pow(get_tile(albedo_tile_cache, tile_uv).xyz, vec3(2.2));
		ALBEDO = mix(real_color, in_cache, 0.15);
	}
	else if (show_keys) {
		ALBEDO = displayBranches();
	}
	else {
		ALBEDO = pow(get_tile(albedo_tile_cache, tile_uv).xyz, vec3(2.2));
	}

	vec2 texel_size = 1.0 / vec2(textureSize(height_map_tile_cache, 0).xy);

	float up = get_tile(height_map_tile_cache, tile_uv + vec3(0.0, texel_size.y, 0.0)).r;
	float down = get_tile(height_map_tile_cache, tile_uv - vec3(0.0, texel_size.y, 0.0)).r;
	float left = get_tile(height_map_tile_cache, tile_uv - vec3(texel_size.x, 0.0, 0.0)).r;
	float right = get_tile(height_map_tile_cache, tile_uv + vec3(texel_size.x, 0.0, 0.0)).r;

	float height_x = right - left;
	float height_y = up - down;

	vec3 base_normal = normalize(NORMAL);
	vec3 tangent = normalize(cross(vec3(0.0, 1.0, 0.0), base_normal));
	vec3 bitangent = normalize(cross(base_normal, tangent));

	NORMAL = normalize(base_normal - tangent * height_x - bitangent * height_y);
}
