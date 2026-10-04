Shader "DTET/ConceptualAccretion"
{
    Properties
    {
        _Tint ("Outer tint", Color) = (0.4, 0.8, 1, 1)
        _InnerTint ("Inner tint", Color) = (1, 0.6, 0.25, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.65
        _FlowSpeed ("Illustrative flow", Float) = 0.4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Conceptual disk"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _InnerTint;
                float _Opacity;
                float _FlowSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float radius = saturate(input.uv.x);
                float angle = input.uv.y * 6.2831853;
                float time = _Time.y * _FlowSpeed;
                float ribbon = sin(angle * 17.0 - time * 3.1 + radius * 14.0);
                float fine = sin(angle * 43.0 + time * 1.7 - radius * 27.0);
                float structure = 0.52 + 0.27 * ribbon + 0.13 * fine;
                float edge = smoothstep(0.01, 0.14, radius) * (1.0 - smoothstep(0.78, 1.0, radius));
                float hotspot = 1.0 + 0.6 * sin(angle + 0.8);
                float3 color = lerp(_InnerTint.rgb, _Tint.rgb, radius);
                float alpha = saturate(_Opacity * edge * structure * hotspot);
                return half4(color * (1.35 - 0.3 * radius), alpha);
            }
            ENDHLSL
        }
    }
}
