Shader "DTET/ReducedDensitySamples"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct Varyings{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            Varyings Vertex(Attributes i)
            {
                Varyings o;o.pos=TransformObjectToHClip(i.positionOS.xyz);
                o.pos.xy+=i.uv*2.2*2/_ScreenParams.xy*o.pos.w;o.uv=i.uv;o.color=i.color;return o;
            }
            half4 Fragment(Varyings i):SV_Target
            {return half4(i.color.rgb,i.color.a*(1-smoothstep(.25,1,length(i.uv))));}
            ENDHLSL
        }
    }
}
