shader_type spatial;

#include "Includes/PlanetTerrainVertexShader.gdshaderinc"

uniform sampler2D albedo_map : repeat_disable, source_color;
uniform float normal_strength;
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

vec3 hsl_to_rgb(vec3 c) {
	vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	vec2 sphere_uv_point = point_on_sphere_to_uv(sphere_local_position);
	vec3 surface_texture = textureLod(albedo_map, is_texture_1D ? vec2(elevation, 0.0) : sphere_uv_point, 0.0).rgb;
	vec3 normal = normals[key.z];
	int lod = int(calculate_lod_to_cam(sphere_local_position, normal, false));
	vec3 debug_color = hsl_to_rgb(vec3(float(lod) / 16.0, 1.0, 0.5));

	NORMAL = height_to_normal(elevation, normal_strength, VERTEX, NORMAL);
	ALBEDO = surface_texture;
}
