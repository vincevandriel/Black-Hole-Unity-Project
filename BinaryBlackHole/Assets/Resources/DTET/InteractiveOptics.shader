Shader "DTET/InteractiveOptics"
{
    Properties
    {
        _MainTex("Linear astronomical image",2D)="black"{}
        _WaveHistory("Retarded signed signal samples",2D)="black"{}
        [HideInInspector] _OpticsFixture("Reference fixture",Float)=0
        [HideInInspector] _FixtureInput("Theta xy",Vector)=(0,0,0,0)
        [HideInInspector] _FixtureLens("Lens depth,mass,radius,source depth",Vector)=(100,.5,1,1000000)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Fragment
            #include "UnityCG.cginc"
            #include "ReducedBinaryFlow.hlsl"
            #include "GeneratedFluidSampling.hlsl"
            #include "BoundedBinaryLensing.hlsl"
            sampler2D _MainTex,_WaveHistory;
            float4 _MainTex_TexelSize,_WaveHistory_TexelSize;
            float4 _CameraOrigin,_CameraRight,_CameraUp,_CameraForward;
            float4 _LensA,_LensB,_LensR; // angular x,y; positive eye depth; mass fraction
            float4 _LensRadii;
            float4 _HorizonA,_HorizonB,_HorizonR; // world centre and areal proxy radius
            float4 _Optics; // lens gain, wave gain, tan(fov/2)*aspect, tan(fov/2)
            float4 _WaveParameters; // history radial extent, fixed normalization, gas enabled, display radial compression
            float4 _Polarization; // e1 screen xy, e2 screen xy
            float4 _WaveBasisX,_WaveBasisY; // world transverse basis; x.w is observer radius
            float _OpticsFixture;
            float _GeneratedGas;
            float4 _FixtureInput,_FixtureLens;

            float2 Limited(float2 v,float limit) { return v*min(1,limit/max(length(v),1e-12)); }
            float Behind(float lensDepth,float sourceDepth)
            { return lensDepth>0&&sourceDepth>lensDepth?saturate(1-lensDepth/sourceDepth):0; }
            float2 Lens(float2 theta,float depth,float mass,float radius,float geometry,float gain)
            {
                return BoundedLensOffset(theta,depth,mass,radius,geometry,gain);
            }
            float2 Wave(float2 theta,float2 signal,float gain)
            {
                signal=clamp(signal,-4,4);
                float scale=.055*clamp(gain,0,2)/(.18+length(theta));
                return Limited(scale*float2(signal.x*theta.x+signal.y*theta.y,signal.y*theta.x-signal.x*theta.y),.035);
            }
            float HorizonWaveFade(float2 theta,float4 lens,float radius)
            {
                if(lens.z<=.01||radius<=0||lens.w<=0)return 1;
                float impact=length(theta-lens.xy)*lens.z/max(radius,.001);
                return smoothstep(1.15,5,impact);
            }
            float Hit(float3 ro,float3 rd,float4 horizon)
            {
                if(horizon.w<=0)return 1e20;
                float3 delta=ro-horizon.xyz;float b=dot(delta,rd),c=dot(delta,delta)-horizon.w*horizon.w;
                if(c<=0)return 0;
                float disc=b*b-c;if(disc<0)return 1e20;
                float t=-b-sqrt(disc);return t>=0?t:1e20;
            }
            float Luma(float3 color) { return dot(max(color,0),float3(.2126,.7152,.0722)); }
            float4 Fragment(v2f_img input):SV_Target
            {
                if((_OpticsFixture>.5&&_OpticsFixture<2.5)||_OpticsFixture>3.5)
                {
                    float geometry=Behind(_FixtureLens.x,_FixtureLens.w);
                    float2 delta=Lens(_FixtureInput.xy,_FixtureLens.x,_FixtureLens.y,_FixtureLens.z,geometry,_Optics.x);
                    if(_OpticsFixture>1.5)delta=Wave(_FixtureInput.xy,_FixtureInput.zw,_Optics.y);
                    if(_OpticsFixture>3.5)delta=BoundedSkyApparentOffset(_FixtureInput.xy,_FixtureLens.x,_FixtureLens.y,_FixtureLens.z,_Optics.x);
                    return float4(delta,geometry,1);
                }
                float2 uv=input.uv;
                float4 original=tex2D(_MainTex,uv);
                if(_Optics.x<=0&&_Optics.y<=0)return original;
                float2 theta=(uv*2-1)*_Optics.zw;
                float3 rd=normalize(_CameraForward.xyz+theta.x*_CameraRight.xyz+theta.y*_CameraUp.xyz);
                float3 ro=_CameraOrigin.xyz;
                float hit=min(Hit(ro,rd,_HorizonA),min(Hit(ro,rd,_HorizonB),Hit(ro,rd,_HorizonR)));
                if(_OpticsFixture>2.5)return float4(hit<1e19?1:0,0,0,1);
                if(_Optics.y<=0)return original;
                // Keep equal-area proxy silhouettes stable. This does not calculate a relativistic shadow.
                if(hit<1e19)return original;

                // Production optics act on background source shaders before gas.
                return original;
            }
            ENDHLSL
        }
    }
}
