Shader "DTET/GeneratedFluidVolume"
{
    Properties
    {
        _Steps("Ray segments",Float)=96
        _GeneratedOpacity("Assumed gray opacity",Float)=.16
        _GeneratedExposure("Illustrative emission exposure",Float)=2.5
        [HideInInspector] _FieldTest("CPU/GPU field fixture",Float)=0
        [HideInInspector] _FieldPoint("Fixture point",Vector)=(0,0,0,0)
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
            #include "GeneratedFluidSampling.hlsl"
            float4 _HalfExtent, _HorizonA, _HorizonB, _HorizonR, _FieldPoint;
            float _Steps, _FieldTest;
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 world:TEXCOORD0; };
            Varyings Vertex(Attributes i)
            { Varyings o; o.world=TransformObjectToWorld(i.positionOS.xyz); o.positionHCS=TransformWorldToHClip(o.world); return o; }
            float HorizonHit(float3 ro,float3 rd,float4 h)
            {
                if(h.w<=0)return 1e20;
                float3 p=ro-h.xyz; float b=dot(p,rd),c=dot(p,p)-h.w*h.w;
                if(c<=0)return 0;
                float d=b*b-c;if(d<0)return 1e20;
                float t=-b-sqrt(d);return t>=0?t:1e20;
            }
            float4 Fragment(Varyings i):SV_Target
            {
                if(_FieldTest>.5)return SampleGeneratedFluid(_FieldPoint.xyz);
                if(_GeneratedGrid.w<.5)return 0;
                float3 ro=_WorldSpaceCameraPos,rd=normalize(i.world-ro);
                float3 safe=sign(rd+1e-20)*max(abs(rd),1e-7);
                float3 a=(-_HalfExtent.xyz-ro)/safe,b=(_HalfExtent.xyz-ro)/safe;
                float3 near=min(a,b),far=max(a,b);
                float start=max(0,max(near.x,max(near.y,near.z))),end=min(far.x,min(far.y,far.z));
                if(end<=start)return 0;
                float hit=min(HorizonHit(ro,rd,_HorizonA),min(HorizonHit(ro,rd,_HorizonB),HorizonHit(ro,rd,_HorizonR)));
                bool blocked=hit<end;if(blocked)end=max(start,hit);
                int count=(int)clamp(_Steps,32,256);float ds=(end-start)/count;
                float opacity=0;float3 radiance=0;
                [loop]for(int k=0;k<count;k++)
                {
                    float4 field=SampleGeneratedFluid(ro+rd*(start+(k+.5)*ds));
                    float tau=max(0,_GeneratedOpacity*field.y*ds);
                    float absorb=tau<1e-3?tau*(1-tau*(.5-tau/6)):1-exp(-tau);
                    radiance+=(1-opacity)*absorb*GeneratedFluidTint(field.zw);
                    opacity+=(1-opacity)*absorb;
                }
                return float4(radiance*_GeneratedExposure,blocked?1:opacity);
            }
            ENDHLSL
        }
    }
}
