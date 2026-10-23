Shader "DTET/ReferenceTracer"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vertex(Attributes i)
            {
                Varyings o; float3 view=TransformWorldToView(TransformObjectToWorld(i.positionOS.xyz));
                view.xy+=i.uv*.055;
                o.pos=mul(UNITY_MATRIX_P,float4(view,1));o.uv=i.uv;return o;
            }
            half4 Fragment(Varyings i):SV_Target
            { return half4(.35,.95,1,.85*(1-smoothstep(.3,1,length(i.uv)))); }
            ENDHLSL
        }
    }
}
