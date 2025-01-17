shader_type spatial;
render_mode unshaded;

#include "Includes/PlanetTerrainVertexShader.gdshaderinc"

uniform int total_texture_subdivisions;
uniform float grid_size;
uniform int[32] texture_mapping;

vec3 hsl_to_rgb(vec3 c) {
	vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	vec2 sphere_uv_point = point_on_sphere_to_uv(sphere_local_position);

	float msb = float(get_lod_of_key(key.xy)) / 16.0;

	vec3 normal = normals[key.z];

	vec2 uv = get_polygon_uv(cube_local_position, normal);

	int lod = int(calculate_lod_to_cam(sphere_local_position, normal, false));

	int grid = texture_mapping[lod];

	float lod_grid_size = grid_size / pow(2.0, float(grid));

	vec2 cell_uv = floor(uv * lod_grid_size) / lod_grid_size;
	//vec2 tile = floor(polygon_uv / lod_grid_size);
	int packed_id = (grid << 3) | int(key.z);
	//float floatBits = packed_id / float(1 << 23);

	//ALBEDO = vec3(float(lod) / 16.0, float(key.z)/16.0, 0.0);
	ALBEDO = vec3(cell_uv, float(packed_id)/255.0);


}