Shader "DTET/CurvedDiskVolume"
{
    Properties
    {
        _GeneratedOpacity("Assumed gray opacity",Float)=8
        _GeneratedExposure("Illustrative emission exposure",Float)=1.6
        _DopplerStrength("Velocity-dependent beaming",Float)=1
        _MaterialDetailStrength("Transported material emission contrast",Range(0,1))=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend One OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GeneratedFluidSampling.hlsl"
            #include "MergerSurface.hlsl"
            #include "CurvedDiskOptics.hlsl"
            struct Attributes {float4 position:POSITION;};
            struct Varyings {float4 position:SV_POSITION;float2 screen:TEXCOORD0;};
            Varyings Vertex(Attributes input)
            {
                Varyings output;output.position=float4(input.position.xy,UNITY_RAW_FAR_CLIP_VALUE,1);
                output.screen=input.position.xy;return output;
            }
            float3 DiskViewRay(float2 screen)
            {
                float3 viewDirection=float3(screen.x/UNITY_MATRIX_P._m00,screen.y/UNITY_MATRIX_P._m11,-1);
                return normalize(mul((float3x3)UNITY_MATRIX_I_V,viewDirection));
            }
            float4 DiskTrace(float3 ro,float3 direction)
            {
                if(_GeneratedGrid.w<.5)return 0;
                if(DiskCaptureDistance(ro)<=.0005)return float4(0,0,0,1);
                float boundary=max(_DiskDomain.z,16),b=dot(ro,direction),c=dot(ro,ro)-boundary*boundary;
                float discriminant=b*b-c;
                if(discriminant<0)return 0;
                float entry=max(0,-b-sqrt(discriminant));
                if(-b+sqrt(discriminant)<=0)return 0;
                float3 p=ro+direction*entry;
                float observerLapse=DiskLapse(ro),opacity=0,travel=0;
                float3 radiance=0;
                bool captured=false;
                int budget=(int)clamp(_DiskDomain.w,64,512);
                [loop]for(int step=0;step<512;step++)
                {
                    if(step>=budget)break;
                    float capture=DiskCaptureDistance(p);
                    if(capture<=.0005){captured=true;break;}
                    float distance=DiskNearestLens(p);
                    float ds=clamp(.08*distance,.015,12);
                    ds=min(ds,max(.001,.65*capture));
                    // Resolve the actual thin emitting layer and its entry, without sampling empty space densely.
                    if(abs(p.x)<_DiskDomain.x+2 && abs(p.z)<_DiskDomain.x+2)
                    {
                        float height=DiskScaleHeight(p.xz);
                        float layer=abs(p.y)-4*height;
                        if(layer<0)
                        {
                            ds=min(ds,max(.008,.42*height/max(abs(direction.y),.15)));
                            float cell=DiskCellWidth();
                            ds=min(ds,max(.06,3*cell/max(length(direction.xz),.15)));
                        }
                        else if(p.y*direction.y<0)ds=min(ds,max(.008,.75*layer/max(abs(direction.y),.05)));
                    }
                    float3 next,nextDirection,midpoint,midpointDirection;
                    DiskAdvance(p,direction,ds,next,nextDirection,midpoint,midpointDirection);
                    if(!all(abs(next)<1e8)||!all(abs(nextDirection)<2))break;
                    if(DiskCaptureDistance(midpoint)<=.0005 || DiskCaptureDistance(next)<=.0005)
                    {captured=true;break;}
                    // Ordered two-point quadrature resolves the thin Gaussian layer without temporal jitter.
                    const float q=.2113248654;
                    DiskIntegrateMedium(lerp(p,next,q),normalize(lerp(direction,nextDirection,q)),
                        .5*ds,observerLapse,radiance,opacity);
                    DiskIntegrateMedium(lerp(p,next,1-q),normalize(lerp(direction,nextDirection,1-q)),
                        .5*ds,observerLapse,radiance,opacity);
                    p=next;direction=nextDirection;travel+=ds;
                    if(opacity>.998 || travel>6*boundary)break;
                    if(dot(p,p)>boundary*boundary && dot(p,direction)>0)break;
                }
                // Numerical exhaustion is not a physical capture classification.
                return float4(radiance*_GeneratedExposure,captured?1:opacity);
            }
            float4 Fragment(Varyings input):SV_Target
            {
                float3 ro=_WorldSpaceCameraPos;
                float4 value=DiskTrace(ro,DiskViewRay(input.screen));
                // Integrate only high-contrast pixel footprints; keep the same geometric capture surface.
                float luminance=dot(value.rgb,float3(.2126,.7152,.0722));
                float2 lightGradient=float2(ddx(luminance),ddy(luminance))/(1+luminance);
                float2 coverageGradient=float2(ddx(value.a),ddy(value.a));
                float2 gradient=dot(lightGradient,lightGradient)>dot(coverageGradient,coverageGradient)
                    ?lightGradient:coverageGradient;
                float edge=abs(gradient.x)+abs(gradient.y);
                float2 pixel=abs(ddx(input.screen))+abs(ddy(input.screen));
                if(edge>.25)
                {
                    float2 offset=.333*pixel*gradient*rsqrt(max(dot(gradient,gradient),1e-8));
                    value=DiskTrace(ro,DiskViewRay(input.screen+offset))
                        +DiskTrace(ro,DiskViewRay(input.screen-offset));
                    value*=.5;
                }
                // Retain scene radiance; ExhibitDisplay supplies the single fixed display curve.
                return value;
            }
            ENDHLSL
        }
    }
}
