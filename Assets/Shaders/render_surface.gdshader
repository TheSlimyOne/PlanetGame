shader_type spatial;
uniform sampler2D albedo_map;
uniform sampler2D height_map;
uniform sampler2D normal_map;

uniform float height_scale = 10;
uniform vec3 offset;

uniform float specular;
uniform float metallic;
uniform float roughness : hint_range(0,1);
uniform float refraction : hint_range(-16,16);


vec2 pointOnSphereToUV(vec3 p) {
    p = normalize(p);
    float longitude = atan(p.x, p.z);
    float latitude = asin(-p.y);
    float u = (longitude / (2.0 * PI) + 0.5);
    float v = (latitude / PI) + 0.5;
    return vec2(u, v);
}

void vertex() {
	vec2 uv = pointOnSphereToUV(VERTEX);
 	float height = textureLod(height_map, uv, 0.0).r;
	VERTEX += NORMAL * height_scale * height;
}

void fragment() {
	vec3 world_pos = ((INV_VIEW_MATRIX) * vec4(VERTEX, 1.0)).xyz;
	world_pos += offset;
	vec2 uv = pointOnSphereToUV(world_pos);
	vec3 height = textureLod(albedo_map, uv, 0.0).rgb;
	ALBEDO = height;


}
