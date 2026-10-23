Shader "DTET/ExhibitSky"
{
    Properties
    {
        _Exposure ("Relative display exposure", Float) = 12
        [HideInInspector] _RenderSize ("Render target dimensions", Vector) = (1600,900,0.000625,0.001111111)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 2.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Exposure;
                float4 _RenderSize;
            CBUFFER_END
            struct Attributes { float4 direction:POSITION; float2 corner:TEXCOORD0; float4 color:COLOR; };
            struct Varyings
            {
                float4 position:SV_POSITION;
                float2 localPixels:TEXCOORD0;
                float4 color:COLOR;
                float sigma:TEXCOORD1;
                float visible:TEXCOORD2;
            };
            Varyings Vertex(Attributes i)
            {
                Varyings o;
                // Rotation only. Translating freely around the binary produces no fake stellar parallax.
                float3 directionVS = mul((float3x3)UNITY_MATRIX_V, i.direction.xyz);
                o.visible = -directionVS.z - 0.00001;
                float4 center = mul(UNITY_MATRIX_P, float4(directionVS, 0));
                float heightScale = _RenderSize.y / 900.0;
                o.sigma = max(.65, (.65 + .55 * sqrt(sqrt(max(i.color.a, 0)))) * heightScale);
                o.localPixels = i.corner * (3 * o.sigma + .5);
                // All four corners have equal W, so the fragment coordinate is linear in pixels.
                o.position = float4(center.xy / max(.00001, center.w) + 2 * o.localPixels * _RenderSize.zw, 0, 1);
                o.color = float4(i.color.rgb, _Exposure * i.color.a * heightScale * heightScale);
                return o;
            }
            float ErfApprox(float x)
            {
                float a = abs(x), t = rcp(1 + .3275911 * a);
                float p = (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - .284496736) * t + .254829592) * t;
                return sign(x) * (1 - p * exp(-a * a));
            }
            float PixelInterval(float p, float sigma)
            {
                float bound = 3 * sigma, scale = rcp(1.4142135623730951 * sigma);
                return ErfApprox(clamp(p + .5, -bound, bound) * scale)
                    - ErfApprox(clamp(p - .5, -bound, bound) * scale);
            }
            float4 Fragment(Varyings i):SV_Target
            {
                clip(i.visible);
                // Pixel-area integration, rather than sampling a radial glow, preserves total flux
                // when the star crosses pixel boundaries or the capped render resolution changes.
                float mass = .25 * PixelInterval(i.localPixels.x, i.sigma)
                    * PixelInterval(i.localPixels.y, i.sigma) / .9946076967722628;
                return float4(i.color.rgb * (i.color.a * max(0, mass)), 0);
            }
            ENDHLSL
        }
    }
}
