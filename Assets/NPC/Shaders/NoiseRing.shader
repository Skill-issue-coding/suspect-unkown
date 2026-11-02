// Draws one noise ring on a flat quad: a band of fixed width in metres at the quad's edge, however large the
// quad is scaled. NoiseRings sets _Radius (the quad's current radius in metres), _Width and _BaseColor per
// ring through a MaterialPropertyBlock. A scaled texture would thicken and blur the line as the ring grows
Shader "Suspect Unknown/Noise Ring"
{
    Properties
    {
        [HideInInspector] _BaseColor("Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Radius("Radius (m)", Float) = 1
        [HideInInspector] _Width("Line width (m)", Float) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "NoiseRing"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Radius;
                float _Width;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Metres from the ring's centre: uv runs 0 to 1 across the quad, which is 2 × _Radius wide
                float distance = length(input.uv - 0.5) * 2.0 * _Radius;

                // Signed metres from the band, negative inside it. The band's outer edge is the quad's edge
                float halfWidth = _Width * 0.5;
                float fromBand = abs(distance - (_Radius - halfWidth)) - halfWidth;

                // About one pixel of soft edge at any zoom, so the line stays sharp without jagged steps
                float pixel = max(fwidth(distance), 1e-4);
                half coverage = 1.0 - saturate(fromBand / pixel + 0.5);

                return half4(_BaseColor.rgb, _BaseColor.a * coverage);
            }
            ENDHLSL
        }
    }
}
