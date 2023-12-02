shader_type spatial;

void fragment() {
	ALBEDO = vec3(UV.x, UV.y, 0);
	//ALBEDO = ((INV_VIEW_MATRIX) * vec4(VERTEX, 1.0)).xyz;
}
