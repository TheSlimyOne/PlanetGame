shader_type spatial;

#include "PlanetTerrainVertexShader.gdshaderinc"

uniform int total_texture_subdivisions;
uniform float grid_size;

void fragment() {
	vec2 sphere_uv_point = point_on_sphere_to_uv(sphere_local_position);

	vec3 normal = normals[key.z];
	
	vec2 polygon_uv = get_polygon_uv(cube_local_position, normal);

	int lod = int(calculate_lod_to_cam(sphere_local_position, normal, false));

	int lod_ratio = min(lod / total_texture_subdivisions, total_texture_subdivisions - 1);

	float lod_grid_size = grid_size / pow(2.0, float(total_texture_subdivisions - 1 - lod_ratio));

	vec2 cell_uv = (fract(polygon_uv * lod_grid_size));

	ALBEDO = vec3(cell_uv, 0);
}