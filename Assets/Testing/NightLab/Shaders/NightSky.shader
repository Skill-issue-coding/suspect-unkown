// Procedural night skybox. No textures needed: gradient, horizon light pollution,
// stars, milky way band, moon and drifting clouds are all generated in the shader.
Shader "SuspectUnknown/Skybox/Night"
{
    Properties
    {
        [Header(Gradient)]
        [HDR] _TopColor ("Zenith Color", Color) = (0.005, 0.01, 0.03, 1)
        [HDR] _HorizonColor ("Horizon Color", Color) = (0.03, 0.05, 0.1, 1)
        [HDR] _BottomColor ("Ground Color", Color) = (0.01, 0.01, 0.015, 1)
        _GradientExponent ("Gradient Exponent", Range(0.1, 4)) = 0.6

        [Header(Horizon Glow (light pollution))]
        [HDR] _HorizonGlowColor ("Glow Color", Color) = (0.15, 0.08, 0.03, 1)
        _HorizonGlowIntensity ("Glow Intensity", Range(0, 4)) = 0.5
        _HorizonGlowFalloff ("Glow Falloff", Range(1, 40)) = 8

        [Header(Stars)]
        _StarDensity ("Star Density", Range(50, 800)) = 300
        _StarAmount ("Star Amount", Range(0, 1)) = 0.35
        _StarSize ("Star Size", Range(0.05, 0.5)) = 0.18
        _StarBrightness ("Star Brightness", Range(0, 20)) = 4
        _StarTwinkleSpeed ("Twinkle Speed", Range(0, 10)) = 2

        [Header(Milky Way)]
        [HDR] _BandColor ("Band Color", Color) = (0.12, 0.13, 0.2, 1)
        _BandIntensity ("Band Intensity", Range(0, 4)) = 0
        _BandNormal ("Band Plane Normal", Vector) = (0.4, 0.3, 0.87, 0)
        _BandWidth ("Band Width", Range(1, 60)) = 12

        [Header(Moon)]
        _MoonDirection ("Moon Direction (set by Night Lab)", Vector) = (0.3, 0.5, 0.8, 0)
        [HDR] _MoonColor ("Moon Color", Color) = (1, 0.97, 0.9, 1)
        _MoonIntensity ("Moon Intensity", Range(0, 20)) = 4
        _MoonSize ("Moon Size (degrees)", Range(0.5, 15)) = 3
        [HDR] _MoonGlowColor ("Moon Glow Color", Color) = (0.3, 0.4, 0.6, 1)
        _MoonGlowIntensity ("Moon Glow Intensity", Range(0, 4)) = 0.6
        _MoonGlowExponent ("Moon Glow Tightness", Range(1, 512)) = 64

        [Header(Clouds)]
        [HDR] _CloudColor ("Cloud Color", Color) = (0.04, 0.05, 0.08, 1)
        _CloudCoverage ("Coverage", Range(0, 1)) = 0.3
        _CloudSoftness ("Softness", Range(0.01, 1)) = 0.35
        _CloudOpacity ("Opacity", Range(0, 1)) = 0.8
        _CloudScale ("Scale", Range(0.1, 5)) = 1.2
        _CloudSpeed ("Speed", Range(0, 0.2)) = 0.01
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor, _HorizonColor, _BottomColor;
                float _GradientExponent;
                float4 _HorizonGlowColor;
                float _HorizonGlowIntensity, _HorizonGlowFalloff;
                float _StarDensity, _StarAmount, _StarSize, _StarBrightness, _StarTwinkleSpeed;
                float4 _BandColor, _BandNormal;
                float _BandIntensity, _BandWidth;
                float4 _MoonDirection, _MoonColor, _MoonGlowColor;
                float _MoonIntensity, _MoonSize, _MoonGlowIntensity, _MoonGlowExponent;
                float4 _CloudColor;
                float _CloudCoverage, _CloudSoftness, _CloudOpacity, _CloudScale, _CloudSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash13(i + float3(0, 0, 0)), Hash13(i + float3(1, 0, 0)), u.x),
                         lerp(Hash13(i + float3(0, 1, 0)), Hash13(i + float3(1, 1, 0)), u.x), u.y),
                    lerp(lerp(Hash13(i + float3(0, 0, 1)), Hash13(i + float3(1, 0, 1)), u.x),
                         lerp(Hash13(i + float3(0, 1, 1)), Hash13(i + float3(1, 1, 1)), u.x), u.y),
                    u.z);
            }

            float Fbm(float3 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    sum += ValueNoise(p) * amplitude;
                    p = p * 2.03 + 17.1;
                    amplitude *= 0.5;
                }
                return sum;
            }

            float StarLayer(float3 dir, float density, float seed)
            {
                float3 p = dir * density + seed;
                float3 cell = floor(p);
                float3 h = Hash33(cell);
                if (h.x > _StarAmount)
                    return 0.0;

                // Project the jittered star point back onto the sphere so every star is round.
                float3 starDir = normalize(cell + 0.5 + (h - 0.5) * 0.6 - seed);
                float dist = length(dir - starDir) * density;
                float star = saturate(1.0 - dist / _StarSize);
                star *= star;

                float twinkle = 0.65 + 0.35 * sin(_Time.y * _StarTwinkleSpeed * (0.5 + h.y) + h.z * 100.0);
                return star * twinkle * (0.3 + 0.7 * h.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                float y = dir.y;
                float above = saturate(y);

                // Base gradient.
                float3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(above, _GradientExponent));
                color = lerp(color, _BottomColor.rgb, sqrt(saturate(-y * 4.0)));

                // City light pollution hugging the horizon.
                float glow = exp(-abs(y) * _HorizonGlowFalloff);
                color += _HorizonGlowColor.rgb * glow * _HorizonGlowIntensity;

                float skyMask = saturate(y * 6.0);

                // Clouds, projected onto a flat layer above the camera.
                float2 cloudUV = dir.xz / (y + 0.2) * _CloudScale;
                float3 cloudP = float3(cloudUV + _Time.y * _CloudSpeed * float2(1.0, 0.4), _Time.y * _CloudSpeed * 0.3);
                float cloudNoise = Fbm(cloudP * 2.0);
                float cloudThreshold = 1.0 - _CloudCoverage;
                float cloud = smoothstep(cloudThreshold - _CloudSoftness * 0.5, cloudThreshold + _CloudSoftness * 0.5, cloudNoise);
                cloud *= _CloudOpacity * skyMask * step(0.0001, _CloudCoverage);

                // Stars and milky way, hidden behind clouds and near the hazy horizon.
                float starMask = skyMask * saturate(y * 3.0) * (1.0 - cloud);
                float stars = StarLayer(dir, _StarDensity, 0.0) + StarLayer(dir, _StarDensity * 1.9, 37.0) * 0.6;
                color += stars * _StarBrightness * starMask;

                float3 bandNormal = normalize(_BandNormal.xyz + 1e-5);
                float band = exp(-pow(dot(dir, bandNormal), 2.0) * _BandWidth * _BandWidth * 0.1);
                band *= smoothstep(0.35, 0.8, Fbm(dir * 6.0));
                color += _BandColor.rgb * band * _BandIntensity * starMask;

                // Moon disc with subtle surface noise, plus a wide halo.
                float3 moonDir = normalize(_MoonDirection.xyz + 1e-5);
                float moonDot = dot(dir, moonDir);
                float moonRadius = radians(_MoonSize) * 0.5;
                float angle = acos(clamp(moonDot, -1.0, 1.0));
                float disc = 1.0 - smoothstep(moonRadius * 0.9, moonRadius, angle);
                float surface = 0.75 + 0.25 * Fbm(dir * 180.0);
                float moonHalo = pow(saturate(moonDot), _MoonGlowExponent) * _MoonGlowIntensity;
                float moonHaloWide = pow(saturate(moonDot), 6.0) * _MoonGlowIntensity * 0.15;

                float3 cloudColor = _CloudColor.rgb
                    + _HorizonGlowColor.rgb * glow * _HorizonGlowIntensity * 0.6
                    + _MoonGlowColor.rgb * (moonHalo + moonHaloWide) * 0.8;
                color = lerp(color, cloudColor, cloud);

                float moonVisibility = skyMask * (1.0 - cloud * 0.85);
                color += _MoonGlowColor.rgb * (moonHalo + moonHaloWide) * skyMask;
                color = lerp(color, _MoonColor.rgb * _MoonIntensity * surface, disc * moonVisibility);

                // Dither: dark gradients band badly without it.
                float noise = Hash13(float3(input.positionCS.xy, frac(_Time.y) * 61.0));
                color += (noise - 0.5) / 255.0;

                return half4(max(color, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
