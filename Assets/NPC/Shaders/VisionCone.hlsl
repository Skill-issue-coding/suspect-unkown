#ifndef VISION_CONE_INCLUDED
#ifdef SHADER_TOOLS_LINT // editor linting only; Shader Graph provides these includes when it compiles
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#endif
#define VISION_CONE_INCLUDED

static const int SoftTaps = 16;

// Wall, cover start and shadow end at a continuous position across the cone (0 = left edge, 1 = right edge).
// The wall blends between the two nearest rays so slanted faces read as straight lines. The crouch-safe
// strip blends only where both rays see cover; next to open floor it would smear into a thin sliver
float3 RayAt(UnityTexture2D rayData, float rayCount, float across)
{
    float x = saturate(across) * rayCount;
    int left = (int)floor(x);
    int right = min(left + 1, (int)rayCount);
    float t = x - left;

    float3 a = rayData.Load(int3(left, 0, 0)).rgb;
    float3 b = rayData.Load(int3(right, 0, 0)).rgb;

    float wall = lerp(a.r, b.r, t);
    bool bothCovered = (a.b - a.g) > 0.0001 && (b.b - b.g) > 0.0001;
    float2 strip = bothCovered ? lerp(a.gb, b.gb, t) : (t < 0.5 ? a.gb : b.gb);

    return float3(wall, strip);
}

// Cuts the vision cone with the rays NpcVisionCone writes into _RayData, and marks the crouch-safe strip
// behind low cover. Softness is the edge width as a fraction of range; every fade is centred on its line
void ConeVisibility_float(float AngleFraction, float Distance, UnityTexture2D RayData, float RayCount,
                          float HalfFov, float Softness, out float Visible, out float Safe)
{
    float across = AngleFraction * 0.5 + 0.5;

    float spread = Softness / max(Distance, 0.001);
    spread = min(spread / (2.0 * HalfFov), 0.025);

    float seen = 0.0;
    float safe = 0.0;
    for (int k = 0; k < SoftTaps; k++)
    {
        float offset = (k / (SoftTaps - 1.0) * 2.0 - 1.0) * spread;
        float3 ray = RayAt(RayData, RayCount, across + offset);

        seen += saturate((ray.x - Distance) / Softness + 0.5);

        float pastStart = saturate((Distance - ray.y) / Softness + 0.5);
        float beforeEnd = saturate((ray.z - Distance) / Softness + 0.5);
        safe += (ray.z - ray.y) > 0.0001 ? pastStart * beforeEnd : 0.0;
    }

    Visible = seen / SoftTaps;
    Safe = safe / SoftTaps;
}

#endif