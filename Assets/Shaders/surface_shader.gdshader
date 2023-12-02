shader_type spatial;

uniform sampler2D albedo_map: repeat_disable;
uniform sampler2D height_map: repeat_disable;
uniform sampler2D detail_map: repeat_disable;
uniform float radius;
uniform float height_scale = 10;

vec2 pointOnSphereToUV(vec3 p) {
    p = normalize(p);
    float longitude = atan(p.x, p.z);
    float latitude = asin(-p.y);
    float u = (longitude / (2.0 * PI) + 0.5);
    float v = (latitude / PI) + 0.5;
    return vec2(u, v);
}
varying vec3 object_space_pos;
varying float height;
 
void vertex() {
	object_space_pos = VERTEX;
	vec2 uv = pointOnSphereToUV(object_space_pos);
 	height = textureLod(height_map, uv, 0.0).r;
	VERTEX += NORMAL * height_scale * height;
	
}

void fragment() {
	vec2 uv = pointOnSphereToUV(object_space_pos);
	vec3 surface_texture = textureLod(albedo_map, uv, 0.0).rgb;
	
	vec2 uv2 = object_space_pos.xz;
	vec2 uv3 = object_space_pos.xy;
	vec3 surface_detail_texture = textureLod(detail_map, uv2, 0.0).rgb;
	ALBEDO = surface_texture;
}