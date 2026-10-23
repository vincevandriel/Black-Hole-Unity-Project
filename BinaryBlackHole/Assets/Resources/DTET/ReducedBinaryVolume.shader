Shader "DTET/ReducedBinaryVolume"
{
    Properties
    {
        _Steps("Ray segments",Float)=96
        _Exposure("False colour exposure",Float)=2.5
        _Opacity("Assumed gray opacity per rg",Float)=.16
        [HideInInspector] _FieldTest("CPU/GPU field fixture",Float)=0
        [HideInInspector] _FieldPoint("CPU/GPU fixture location",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend One OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Front
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ReducedBinaryFlow.hlsl"
            float4 _HalfExtent,_HorizonA,_HorizonB,_HorizonR;
            float _Steps,_Exposure,_Opacity,_FieldTest;
            float4 _FieldPoint;
            struct Attributes {float4 positionOS:POSITION;};
            struct Varyings {float4 positionHCS:SV_POSITION;float3 world:TEXCOORD0;};
            Varyings Vertex(Attributes i)
            {Varyings o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionHCS=TransformWorldToHClip(o.world);return o;}
            float Absorbed(float tau)
            {return tau<1e-3?tau*(1-tau*(.5-tau/6)):1-exp(-tau);}
            float HorizonHit(float3 ro,float3 rd,float4 horizon)
            {
                if(horizon.w<=0)return 1e20;
                float3 d=ro-horizon.xyz;float b=dot(d,rd);float disc=b*b-dot(d,d)+horizon.w*horizon.w;
                if(disc<0)return 1e20;
                float hit=-b-sqrt(disc);return hit>0?hit:1e20;
            }
            float4 Fragment(Varyings i):SV_Target
            {
                if(_FieldTest>.5) {float2 field=SampleReducedFlow(_FieldPoint.xyz);return float4(field,0,1);}
                float3 ro=_WorldSpaceCameraPos,rd=normalize(i.world-ro);
                float3 safe=sign(rd+1e-20)*max(abs(rd),1e-7);
                float3 a=(-_HalfExtent.xyz-ro)/safe,b=(_HalfExtent.xyz-ro)/safe;
                float3 n=min(a,b),f=max(a,b);
                float start=max(0,max(n.x,max(n.y,n.z))),end=min(f.x,min(f.y,f.z));
                if(end<=start)return 0;
                float hit=min(HorizonHit(ro,rd,_HorizonA),min(HorizonHit(ro,rd,_HorizonB),HorizonHit(ro,rd,_HorizonR)));
                bool blocked=hit<end;if(blocked)end=max(start,hit);
                int count=(int)clamp(_Steps,32,256);float ds=(end-start)/count;
                float opacity=0;float3 radiance=0;
                [loop]for(int k=0;k<count;k++)
                {
                    float midpoint=start+(k+.5)*ds;
                    float3 centre=ro+rd*midpoint;
                    // Narrow inner disks and streams need finer quadrature than the broad reservoir.
                    // Expand by one base segment so crossing a region boundary cannot skip refinement.
                    float cavity=_FlowReservoir.x+3+ds;
                    int subdivisions=(dot(centre.xz,centre.xz)<cavity*cavity && abs(centre.y)<6+ds)?4:1;
                    float subDs=ds/subdivisions;
                    [loop]for(int j=0;j<subdivisions;j++)
                    {
                        float3 p=ro+rd*(midpoint-.5*ds+(j+.5)*subDs);
                        float2 field=SampleReducedFlow(p);
                        float source=field.y/max(field.x,1e-12);
                        float temperature=sqrt(sqrt(max(source,0)));
                        float3 tint=lerp(float3(1,.28,.065),float3(.62,.83,1),saturate((temperature-.38)/.7));
                        float absorb=Absorbed(max(0,_Opacity*field.x*subDs));
                        radiance+=(1-opacity)*absorb*source*tint;opacity+=(1-opacity)*absorb;
                    }
                }
                // Linear premultiplied radiance; the display applies one final exposure map
                // after gas absorption has attenuated the background sky.
                return float4(radiance*_Exposure,blocked?1:opacity);
            }
            ENDHLSL
        }
    }
}
