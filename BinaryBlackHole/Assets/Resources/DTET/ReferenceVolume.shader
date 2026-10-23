Shader "DTET/ReferenceVolume"
{
    Properties { _OpacityScale ("Gray opacity multiplier", Float) = 1 _Exposure ("False colour exposure", Float) = 3 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Front
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE3D(_Lower); SAMPLER(sampler_Lower);
            TEXTURE3D(_Upper); SAMPLER(sampler_Upper);
            CBUFFER_START(UnityPerMaterial)
                float _Blend, _OpacityScale, _Exposure, _Extinction, _Steps, _SlabTest;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 world:TEXCOORD0; };
            Varyings Vertex(Attributes i)
            {
                Varyings o; o.world=TransformObjectToWorld(i.positionOS.xyz);
                o.positionHCS=TransformWorldToHClip(o.world); return o;
            }
            float EmissionFraction(float tau)
            { return tau<1e-3 ? tau*(1-tau*(.5-tau/6)) : 1-exp(-tau); }
            float4 Fragment(Varyings input):SV_Target
            {
                float3 ro=_WorldSpaceCameraPos, rd=normalize(input.world-ro);
                // Isolated coordinate-inspection box. All path lengths below are in rg.
                float3 safe=sign(rd+1e-20)*max(abs(rd),1e-7);
                float3 a=(-float3(20,6,20)-ro)/safe, b=(float3(20,6,20)-ro)/safe;
                float3 near3=min(a,b), far3=max(a,b);
                float nearT=max(0,max(near3.x,max(near3.y,near3.z)));
                float farT=min(far3.x,min(far3.y,far3.z));
                if(farT<=nearT) return 0;
                // The radius-2 sphere is only a coordinate horizon proxy in this straight-ray view.
                float q=dot(ro,rd), discriminant=q*q-dot(ro,ro)+4;
                bool blocked=false;
                if(_SlabTest<.5 && discriminant>=0)
                {
                    float hit=-q-sqrt(discriminant);
                    if(hit>nearT && hit<farT) { farT=hit; blocked=true; }
                }
                int count=(int)clamp(_Steps,32,384);
                float ds=(farT-nearT)/count, trans=1, opacity=0;
                float3 radiance=0;
                [loop] for(int k=0;k<count;k++)
                {
                    float3 p=ro+rd*(nearT+(k+.5)*ds);
                    float3 uv=p/float3(40,12,40)+.5;
                    float4 f=lerp(SAMPLE_TEXTURE3D_LOD(_Lower,sampler_Lower,uv,0),SAMPLE_TEXTURE3D_LOD(_Upper,sampler_Upper,uv,0),_Blend);
                    if(_SlabTest>.5) f=float4(1,1,0,0);
                    float rho=max(0,f.r), temp=f.g/max(rho,1e-10);
                    // Gray LTE source B_bol=sigma*T^4/pi, normalized to B_bol(T0).
                    // RGB is an explicit false-colour palette, not a visible-light spectrum.
                    float thermal=pow(max(temp,0),4);
                    float3 palette=lerp(float3(1,.19,.035),float3(.65,.85,1),saturate((temp-.45)/.85));
                    if(_SlabTest>.5) palette=1;
                    float absorb=EmissionFraction(max(0,_OpacityScale)*_Extinction*rho*ds);
                    radiance+=trans*absorb*thermal*palette;
                    opacity+=trans*absorb;
                    trans=1-opacity;
                }
                float3 scaled=radiance*_Exposure;
                float3 mapped=float3(EmissionFraction(scaled.r),EmissionFraction(scaled.g),EmissionFraction(scaled.b));
                return float4(mapped,blocked?1:opacity);
            }
            ENDHLSL
        }
    }
}
