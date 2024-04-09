#[compute]
#version 450
#define PI 3.14159265359

//Jad Khoury https://jadkhoury.github.io/files/MasterThesisFinal.pdf
layout(local_size_x = 16, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) buffer AtomicCounterBuffer {
    uint primCount_full[96];
    uint primCount_culled[96];
};


layout(set = 0, binding = 1, std430) buffer IndicesBlock {
    int read_index;
    int write_index;
};

layout(set = 0, binding = 2, std430) buffer readonly ReadList {
    uvec4 read_list[];
};

layout(set = 0, binding = 3, std430) buffer writeonly WriteList {
    uvec4 write_list[];
};

vec2 getTranslation(uint b1) {
    vec2 translation;
    translation.x = float(b1 & 0x1); 
    translation.y = float(b1 ^ 0x1);
    return translation * 0.5;
}

float getRotation(uint b1b2, uint b1, uint b2) {
    uint a = (b1b2 ^ 0x2);
    uint b = (a | 0x1);
    uint c = (b1 ^ b2);
    return (b * c) * PI * 0.5;
}

vec2 rotate(float theta, vec2 tr) {
    vec2 r;
    float cosT = cos(theta), sinT = sin(theta);
    r.x = cosT * tr.x - sinT * tr.y;
    r.y = sinT * tr.x + cosT * tr.y;
    return r;
}

void main() {
    int leaf_count = int(atomicExchange(primCount_full[read_index], primCount_full[read_index]));
    uint invocationID = gl_GlobalInvocationID.x;

    // if (invocationID > leaf_count)
        // return;
   
    // uvec4 key = read_list[invocationID];
    write_list[invocationID] = read_list[invocationID];

}