shader_type spatial;
render_mode unshaded;

#include "res://Assets/Shaders/Includes/PlanetTerrainVertexShader.gdshaderinc"

vec3 hsl_to_rgb(vec3 c) {
	vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	// Adjusting uvs to make sure the range is [0, 1)
	vec2 adjusted_uv = min(vertex_uv, vec2(1.0) - vec2(1e-7));

	int lod = int(calculate_lod_to_cam(sphere_local_position, camera_position, planet_transform_matrix, fovy, sub_factor, radius, maximum_lod));
	int mip_index = lod_to_mip_map[lod];

	float grid_size = pow(2.0, float(total_texture_subdivisions - 1));
	float lod_grid_size = grid_size / pow(2.0, float(mip_index));

	vec2 cell_uv = floor(adjusted_uv * lod_grid_size) / lod_grid_size;

	int packed_id = (mip_index << 4) | int(key.z);

	//ALBEDO = vec3(float(lod) / 16.0, float(key.z)/16.0, 0.0);
	ALBEDO = vec3(cell_uv, float(packed_id)/255.0);


}