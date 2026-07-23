shader_type spatial;

uniform sampler2D height_map : repeat_enable, filter_linear;
uniform float height_strength = 0.1;
uniform float height_offset = 0.0;

varying vec3 view_position;
varying float height_value;

void vertex()
{
	//height_value = textureLod(height_map, UV, 0.0).r;
//
	//vec3 radial_direction = normalize(VERTEX);
	//float displacement = height_offset + height_value * height_strength;
//
	//VERTEX += radial_direction * displacement;
//
	//view_position = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment()
{
	//vec3 tangent_x = dFdx(view_position);
	//vec3 tangent_y = dFdy(view_position);
//
	//vec3 calculated_normal = normalize(cross(tangent_x, tangent_y));
//
	//if (dot(calculated_normal, NORMAL) < 0.0)
		//calculated_normal = -calculated_normal;
//
	//NORMAL = calculated_normal;
//
	//ALBEDO = vec3(height_value);
	//ALBEDO = texture(height_map, UV).xyz;
	//ALBEDO = vec3(UV, 0);
	//ROUGHNESS = 1.0;
	ALBEDO = vec3(0, 0.25, 0);
}