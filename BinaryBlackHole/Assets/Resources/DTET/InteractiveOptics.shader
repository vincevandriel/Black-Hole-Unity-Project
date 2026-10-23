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
            sampler2D _MainTex,_WaveHistory;
            float4 _MainTex_TexelSize;
            float4 _CameraOrigin,_CameraRight,_CameraUp,_CameraForward;
            float4 _LensA,_LensB,_LensR; // angular x,y; positive eye depth; mass fraction
            float4 _LensRadii;
            float4 _HorizonA,_HorizonB,_HorizonR; // world centre and areal proxy radius
            float4 _Optics; // lens gain, wave gain, tan(fov/2)*aspect, tan(fov/2)
            float4 _WaveParameters; // radial extent, fixed signal normalization, gas enabled, source centre eye depth
            float4 _Polarization; // e1 screen xy, e2 screen xy
            float4 _SystemAngle;
            float _OpticsFixture;
            float4 _FixtureInput,_FixtureLens;

            float2 Limited(float2 v,float limit) { return v*min(1,limit/max(length(v),1e-12)); }
            float Behind(float lensDepth,float sourceDepth)
            { return lensDepth>0&&sourceDepth>lensDepth?saturate(1-lensDepth/sourceDepth):0; }
            float2 Lens(float2 theta,float depth,float mass,float radius,float geometry,float gain)
            {
                if(depth<=.01||mass<=0||gain<=0||geometry<=0)return 0;
                float soft=max(.75*max(0,radius),.15*mass)/depth;
                return Limited(-4*mass*geometry*min(gain,2)*theta/(depth*max(1e-12,dot(theta,theta)+soft*soft)),.16);
            }
            float2 Wave(float2 theta,float2 signal,float gain)
            {
                signal=clamp(signal,-4,4);
                return Limited(.025*clamp(gain,0,2)*float2(signal.x*theta.x+signal.y*theta.y,signal.y*theta.x-signal.x*theta.y),.035);
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
                if(_OpticsFixture>.5)
                {
                    float geometry=Behind(_FixtureLens.x,_FixtureLens.w);
                    float2 delta=Lens(_FixtureInput.xy,_FixtureLens.x,_FixtureLens.y,_FixtureLens.z,geometry,_Optics.x);
                    if(_OpticsFixture>1.5)delta=Wave(_FixtureInput.xy,_FixtureInput.zw,_Optics.y);
                    return float4(delta,geometry,1);
                }
                float2 uv=input.uv;
                float4 original=tex2D(_MainTex,uv);
                if(_Optics.x<=0&&_Optics.y<=0)return original;
                float2 theta=(uv*2-1)*_Optics.zw;
                float3 rd=normalize(_CameraForward.xyz+theta.x*_CameraRight.xyz+theta.y*_CameraUp.xyz);
                float3 ro=_CameraOrigin.xyz;
                float hit=min(Hit(ro,rd,_HorizonA),min(Hit(ro,rd,_HorizonB),Hit(ro,rd,_HorizonR)));
                // Keep equal-area proxy silhouettes stable. This does not calculate a relativistic shadow.
                if(hit<1e19)return original;

                float3 weights=float3(1,1,1);
                float gasLum=0,depthMoment=0,trans=1;
                float3 behindLum=0;
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
                            float2 field=SampleReducedFlow(ro+rd*distance);
                            float source=field.y/max(field.x,1e-12);
                            float temperature=sqrt(sqrt(max(source,0)));
                            float3 tint=lerp(float3(1,.28,.065),float3(.62,.83,1),saturate((temperature-.38)/.7));
                            float absorbed=1-exp(-max(0,.16*field.x*ds));
                            float contribution=trans*absorbed*source*Luma(tint)*2.5;
                            float depth=distance*dot(rd,_CameraForward.xyz);
                            gasLum+=contribution;depthMoment+=contribution*depth;
                            behindLum+=contribution*float3(Behind(_LensA.z,depth),Behind(_LensB.z,depth),Behind(_LensR.z,depth));
                            trans*=1-absorbed;
                        }
                    }
                    // Weighted finite-source geometry avoids treating all foreground gas as background.
                    // Transparent empty pixels must retain full lensing: otherwise a star could
                    // disappear from its old pixel but never appear at its newly displaced location.
                    weights=gasLum>1e-8?lerp(saturate(behindLum/gasLum),float3(1,1,1),trans):1;
                }
                float2 lens=Lens(theta-_LensA.xy,_LensA.z,_LensA.w,_LensRadii.x,weights.x,_Optics.x)
                    +Lens(theta-_LensB.xy,_LensB.z,_LensB.w,_LensRadii.y,weights.y,_Optics.x)
                    +Lens(theta-_LensR.xy,_LensR.z,_LensR.w,_LensRadii.z,weights.z,_Optics.x);
                lens=Limited(lens,.22);

                float2 wave=0;
                if(_Optics.y>0&&_WaveParameters.x>0&&_WaveParameters.w>.01)
                {
                    float planeDistance=_WaveParameters.w/max(.01,dot(rd,_CameraForward.xyz));
                    float representative=gasLum>1e-7?min(planeDistance,depthMoment/gasLum/max(.01,dot(rd,_CameraForward.xyz))):planeDistance;
                    float radius=length(ro+rd*representative);
                    float coordinate=radius/_WaveParameters.x;
                    if(coordinate<=1)
                    {
                        float4 history=tex2D(_WaveHistory,float2((coordinate*255+.5)/256,.5));
                        // No extrapolation or mixing across an unavailable history boundary.
                        if(history.b>.999)
                        {
                            float2 normalized=history.rg*_WaveParameters.y;
                            float2 e1=_Polarization.xy,e2=_Polarization.zw;
                            float2 relativeTheta=theta-_SystemAngle.xy;
                            float2 local=float2(dot(relativeTheta,e1),dot(relativeTheta,e2));
                            float2 localDelta=Wave(local,normalized,_Optics.y);
                            wave=(localDelta.x*e1+localDelta.y*e2)*(1-smoothstep(.85,1,coordinate));
                        }
                    }
                }
                float edge=smoothstep(0,.04,min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)));
                float2 displaced=uv+edge*(lens+wave)/(2*_Optics.zw);
                float2 halfPixel=abs(_MainTex_TexelSize.xy)*.5;
                displaced=clamp(displaced,halfPixel,1-halfPixel);
                return tex2D(_MainTex,displaced);
            }
            ENDHLSL
        }
    }
}
