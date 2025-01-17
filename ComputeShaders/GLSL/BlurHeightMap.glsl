#[compute]
#version 450
#define PI 3.14159265359

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 0, r8) restrict uniform readonly image2D height_map;
layout(set = 0, binding = 1, r8) restrict uniform writeonly image2D blurred_height_map;
layout(set = 0, binding = 2, std430) buffer restrict image_padding
{
    int padding;
    float margin;
};
const int offset = 1;
const float kernel[9] = float[9](
    0.0, 0.25, 0.0,
    0.25, 0.5, 0.25,
    0.0, 0.25, 0.0 
);

void main() {
    ivec2 texture_size = imageSize(blurred_height_map);
    uvec2 invocation_id = gl_GlobalInvocationID.xy;
    
    if (invocation_id.x > texture_size.x || invocation_id.y > texture_size.y) return;

    vec4 result = vec4(0.0);
    ivec2 coord = ivec2(padding) + ivec2(invocation_id);
    vec4 center_pixel_color = imageLoad(height_map, coord);


    for (int y = -offset; y <= offset; ++y)
    {
        for (int x = -offset; x <= offset; ++x)
        {
            vec4 current_pixel_color = imageLoad(height_map, coord + ivec2(x, y));
            int kernel_index = (y + offset) * (2 * offset + 1) + (x + offset);
            float weight = kernel[kernel_index];

       
            result += current_pixel_color * weight;  

        }
    }


   
    imageStore(blurred_height_map, ivec2(invocation_id), vec4(result.xyz, 1));
}