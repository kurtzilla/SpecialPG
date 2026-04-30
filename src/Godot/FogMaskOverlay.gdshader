shader_type canvas_item;

uniform sampler2D fog_mask : filter_linear, repeat_disable;
uniform vec4 fog_color : source_color = vec4(0.04, 0.05, 0.08, 0.78);
uniform float edge_width_cells : hint_range(0.1, 4.0, 0.01) = 1.0;
uniform float edge_softness : hint_range(0.2, 6.0, 0.01) = 1.6;
uniform int blur_taps : hint_range(1, 12, 1) = 6;

void fragment() {
    vec2 mask_size = vec2(textureSize(fog_mask, 0));
    vec2 texel = vec2(1.0) / max(mask_size, vec2(1.0));
    float taps = float(max(1, blur_taps));
    float radius = max(0.0001, edge_width_cells);
    vec2 step_uv = texel * radius;

    float accum = 0.0;
    float weight = 0.0;
    for (int iy = -12; iy <= 12; iy++) {
        if (abs(iy) > blur_taps) {
            continue;
        }

        for (int ix = -12; ix <= 12; ix++) {
            if (abs(ix) > blur_taps) {
                continue;
            }

            vec2 off = vec2(float(ix), float(iy)) / taps;
            float w = exp(-dot(off, off) * 3.0);
            accum += texture(fog_mask, UV + off * step_uv).a * w;
            weight += w;
        }
    }

    float reveal = accum / max(weight, 0.0001);
    float fog_alpha = fog_color.a * (1.0 - pow(clamp(reveal, 0.0, 1.0), edge_softness));
    COLOR = vec4(fog_color.rgb, fog_alpha);
}
