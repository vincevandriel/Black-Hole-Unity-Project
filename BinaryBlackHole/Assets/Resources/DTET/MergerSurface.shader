Shader "DTET/MergerSurface"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+5" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On ZTest LEqual Cull Front
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MergerSurface.hlsl"
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 world:TEXCOORD0; };
            struct SurfaceOutput { float4 color:SV_Target; float depth:SV_Depth; };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS=TransformWorldToHClip(output.world);
                return output;
            }
            SurfaceOutput Fragment(Varyings input)
            {
                float3 ro=_WorldSpaceCameraPos,rd=normalize(input.world-ro);
                float hit=MergerSurfaceHit(ro,rd);
                clip(1e19-hit);
                SurfaceOutput output; output.color=float4(0,0,0,1);
                if(hit<=_MergerShape.z)
                {
                    #if UNITY_REVERSED_Z
                    output.depth=1;
                    #else
                    output.depth=0;
                    #endif
                    return output;
                }
                float4 positionHCS=TransformWorldToHClip(ro+rd*hit);
                float depth=positionHCS.z/max(positionHCS.w,1e-8);
                #if !UNITY_REVERSED_Z
                depth=(depth-UNITY_NEAR_CLIP_VALUE)/(1-UNITY_NEAR_CLIP_VALUE);
                #endif
                output.depth=saturate(depth);
                return output;
            }
            ENDHLSL
        }
    }
}
