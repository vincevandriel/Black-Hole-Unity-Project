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

                float gasLum=0,depthMoment=0,trans=1;
                if(_WaveParameters.z>.5)
                {
                    float3 safe=sign(rd+1e-20)*max(abs(rd),1e-7);
                    float3 bounds=float3(_FlowBounds.x,_FlowBounds.y,_FlowBounds.x);
                    float3 a=(-bounds-ro)/safe,b=(bounds-ro)/safe;
                    float3 n=min(a,b),f=max(a,b);
                    float start=max(0,max(n.x,max(n.y,n.z))),end=min(f.x,min(f.y,f.z));
                    if(end>start)
                    {
                        float ds=(end-start)/24;
                        [loop] for(int k=0;k<24;k++)
                        {
                            float distance=start+(k+.5)*ds;
                            float3 position=ro+rd*distance;
                            float2 field;
                            float4 generated=0;
                            if(_GeneratedGas>.5) {generated=SampleGeneratedFluid(position);field=float2(generated.y,generated.y);}
                            else field=SampleReducedFlow(position);
                            float3 emission=_GeneratedGas>.5?GeneratedFluidDisplayEmission(position,generated)
                                :ReducedFlowDisplayEmission(position,field);
                            float opacity=_GeneratedGas>.5?_GeneratedOpacity:.16;
                            float exposure=_GeneratedGas>.5?_GeneratedExposure:2.5;
                            float absorbed=1-exp(-max(0,opacity*field.x*ds));
                            float contribution=trans*absorbed*Luma(emission)*exposure;
                            float depth=distance*dot(rd,_CameraForward.xyz);
                            gasLum+=contribution;depthMoment+=contribution*depth;
                            trans*=1-absorbed;
                        }
                    }
                }

                float2 wave=0;
                if(_Optics.y>0&&_WaveParameters.x>0)
                {
                    // Probe the outgoing spherical history at the ray's closest approach,
                    // or at foreground emission. Unlike a fixed camera-facing plane this
                    // stays finite when orbiting, looking sideways, or flying past the pair.
                    float closest=max(0,-dot(ro,rd));
                    float representative=gasLum>1e-7?min(closest,depthMoment/gasLum/max(.01,dot(rd,_CameraForward.xyz))):closest;
                    float3 probe=ro+rd*representative;
                    // An explicitly declared display compression brings the real waveform's
                    // long wavelengths into the scene. Its source timing is never accelerated
                    // or substituted with a decorative shader sine.
                    float radius=length(probe)*_WaveParameters.w;
                    float coordinate=radius/_WaveParameters.x;
                    if(coordinate<=1)
                    {
                        float4 history=tex2D(_WaveHistory,float2((coordinate*(_WaveHistory_TexelSize.z-1)+.5)*_WaveHistory_TexelSize.x,.5));
                        // No extrapolation or mixing across an unavailable history boundary.
                        if(history.b>.999)
                        {
                            float2 normalized=history.rg*_WaveParameters.y;
                            float2 e1=_Polarization.xy,e2=_Polarization.zw;
                            float2 local=float2(dot(probe,_WaveBasisX.xyz),dot(probe,_WaveBasisY.xyz))/max(1,_WaveBasisX.w);
                            float2 localDelta=Wave(local,normalized,_Optics.y);
                            float horizonFade=HorizonWaveFade(theta,_LensA,_LensRadii.x)
                                *HorizonWaveFade(theta,_LensB,_LensRadii.y)*HorizonWaveFade(theta,_LensR,_LensRadii.z);
                            wave=(localDelta.x*e1+localDelta.y*e2)*(1-smoothstep(.85,1,coordinate))*horizonFade;
                        }
                    }
                }
                float edge=smoothstep(0,.04,min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
                // Static mass deflection already moved only the immutable background star
                // centres in ExhibitSky. This pass can never reimage the gas with that lens.
                float2 displaced=uv+edge*wave/(2*_Optics.zw);
                float2 halfPixel=abs(_MainTex_TexelSize.xy)*.5;
                displaced=clamp(displaced,halfPixel,1-halfPixel);
                // The screen pass has no hidden data behind a hole. Do not duplicate an
                // already-composited proxy silhouette into neighbouring gas or stars.
                float2 displacedTheta=(displaced*2-1)*_Optics.zw;
                float3 displacedRay=normalize(_CameraForward.xyz+displacedTheta.x*_CameraRight.xyz+displacedTheta.y*_CameraUp.xyz);
                if(min(Hit(ro,displacedRay,_HorizonA),min(Hit(ro,displacedRay,_HorizonB),Hit(ro,displacedRay,_HorizonR)))<1e19)return original;
                return tex2D(_MainTex,displaced);
            }
            ENDHLSL
        }
    }
}
