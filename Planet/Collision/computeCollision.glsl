#[compute]

#version 450

layout(local_size_x = 32, local_size_y = 32, local_size_z = 1) in;

layout(set = 0, binding = 0, r8) restrict uniform readonly image2D heightMap;

layout(set = 0, binding = 0, std430) buffer vectors{
    vec3 data[];
} floats;

float getHeight(int x, int y)
{
    vec4 pixel = imageLoad(heightMap, ivec2(x,y));
    return pixel.r;
}

void main() 
{
    ivec2 pos = ivec2(gl_GlobalInvocationID.xy);

     

    floats.data[gl_LocalInvocationID.x] += 100;
}