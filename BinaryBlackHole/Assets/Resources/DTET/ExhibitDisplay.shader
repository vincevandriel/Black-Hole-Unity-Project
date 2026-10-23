Shader "DTET/ExhibitDisplay"
{
    Properties{[PerRendererData] _MainTex("Scene radiance",2D)="black"{}}
    SubShader
    {
        Tags{"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend One Zero
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 positionHCS:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vertex(A i){V o;o.positionHCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            half4 Fragment(V i):SV_Target
            {float3 radiance=max(0,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb);return half4(1-exp(-radiance),1);}
            ENDHLSL
        }
    }
}
