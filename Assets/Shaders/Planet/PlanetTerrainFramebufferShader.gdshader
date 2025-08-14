shader_type spatial;
render_mode unshaded;

#include "res://Assets/Shaders/Includes/PlanetTerrainVertexShader.gdshaderinc"

void fragment() {
	// Adjusting uvs to make sure the range is [0, 1)
	vec2 adjusted_uv = min(vertex_uv, vec2(0.9999999));

	int lod = int(calculate_lod_to_cam(sphere_local_position, camera_position, planet_transform_matrix, fovy, sub_factor, radius, maximum_lod));
	int mip_index = lod_to_mip_map[lod];

	float grid_size = pow(2.0, float(total_texture_subdivisions - 1));
	float lod_grid_size = grid_size / pow(2.0, float(mip_index));

	vec2 cell_uv = floor(adjusted_uv * lod_grid_size) / lod_grid_size;

	int packed_id = (mip_index << 4) | int(key.z);

	ALBEDO = vec3(cell_uv, float(packed_id));
}