shader_type canvas_item;
render_mode unshaded;

uniform int index;
uniform sampler2DArray indirection_table : repeat_disable, source_color, filter_nearest;

void fragment() {
	vec4 table_value = texture(indirection_table, vec3(UV, float(index)), 0);
	//table_value.rgb = (table_value.rgb / 64.0) * 255.0;
	COLOR = vec4(table_value.xyz, 1);
}
