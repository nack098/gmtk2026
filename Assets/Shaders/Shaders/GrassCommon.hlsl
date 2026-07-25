#ifndef GRASS_COMMON_INCLUDED
#define GRASS_COMMON_INCLUDED

struct GrassData
{
    float id;          // 4 bytes  (Always 0.0)
    float3 rotation;   // 12 bytes (Pitch, Yaw, Roll in deg)
    float2 scale;      // 8 bytes  (Width, Height)
    float3 position;   // 12 bytes (World Pos)
    float3 normal;     // 12 bytes (Surface Up)
    float pad;         // 4 bytes
};

#endif
