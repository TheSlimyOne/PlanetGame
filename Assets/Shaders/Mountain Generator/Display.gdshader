shader_type spatial;
uniform sampler2D coarse_elevation_map : repeat_disable, source_color;
uniform sampler2D peak_density_map : repeat_disable, source_color;
uniform sampler2D bump_map : repeat_disable, source_color;
uniform float height_scale = 1;
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
varying vec2 uv;
varying float elevation;
void vertex() {
	uv = UV;
	elevation = textureLod(bump_map, uv, 0.0).x;
	VERTEX += NORMAL * elevation * height_scale;
	//vec3(0,1,0) * textureLod(bump_map, uv, 0.0).x * height_scale
}

void fragment() {
	vec3 coarse_elvation_value = texture(coarse_elevation_map, uv, 0.0).xyz;
	vec3 peak_density_value = textureLod(peak_density_map, uv, 0.0).xyz;
	ALBEDO = vec3(coarse_elvation_value.x, peak_density_value.x, 0);
	NORMAL = height_to_normal(elevation, 1, VERTEX, NORMAL);
}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
