#ifndef COMMON_HLSLI
#define COMMON_HLSLI
struct Particle
{
    float3 position;
    float3 forward;
    float3 data; //x = age, y = lifetime, z = random
    float4 color;
    float size;
    float alive;
};

uint InsertZeroBit(uint Value, uint BitIdx)
{
    uint Mask = BitIdx - 1;
    return (Value & ~Mask) << 1 | (Value & Mask);
}

#endif