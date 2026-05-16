shader_type canvas_item;
render_mode unshaded;

uniform sampler2D atlas_tex : filter_nearest, repeat_disable;

void fragment() {
	// INSTANCE_CUSTOM: xy = UV origin, zw = UV size (normalized 0..1)
	vec4 uv = INSTANCE_CUSTOM;
	vec2 atlas_uv = uv.xy + UV * uv.zw;
	COLOR = texture(atlas_tex, atlas_uv);
}
