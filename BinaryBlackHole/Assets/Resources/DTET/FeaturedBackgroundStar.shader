Shader "DTET/FeaturedBackgroundStar"
{
    Properties
    {
        [HideInInspector] _WaveHistory("Retarded signed signal", 2D) = "black" {}
        [HideInInspector] _FeaturedSource("World source centre and radius in Rg", Vector) = (0,0,1000000,4650)
        [HideInInspector] _FeaturedColor("Assumed display colour and exposure", Vector) = (.86,.94,1,1.8)
        [HideInInspector] _StellarFixture("CPU/GPU lens reference fixture", Float) = 0
        [HideInInspector] _StellarFixtureInput("Image angle xy", Vector) = (.2,0,0,0)
        [HideInInspector] _StellarFixtureLens("Lens depth, mass, horizon radius, source depth", Vector) = (100,.5,1,1000000)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background+15" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "StellarWaveWarp.hlsl"
            float4 _FeaturedSource, _FeaturedColor;
            float _StellarFixture;
            float4 _StellarFixtureInput, _StellarFixtureLens;
            struct Attributes { float4 position : POSITION; };
            struct Varyings { float4 position : SV_POSITION; float2 angularUnit : TEXCOORD0; };
            Varyings Vertex(Attributes input)
            {
                Varyings output; output.position = float4(input.position.xy, UNITY_RAW_FAR_CLIP_VALUE, 1);
                output.angularUnit = input.position.xy;
                // Unity flips the raster projection for render textures on some graphics
                // APIs. Restore camera-up when deriving rays from explicit clip vertices.
                output.angularUnit.y *= _ProjectionParams.x;
                return output;
            }
            float2 StellarDeflection(float2 theta, float4 lens, float horizonRadius, float sourceDepth)
            {
                if (_Optics.x<=0 || lens.z<=.01 || lens.w<=0 || sourceDepth<=lens.z) return 0;
                float2 relative = theta-lens.xy;
                float geometry = saturate(1-lens.z/sourceDepth);
                float core = max(.25*max(0,horizonRadius), .03*lens.w)/lens.z;
                float strength = 4*lens.w*geometry*clamp(_Optics.x,0,2)/lens.z;
                float2 alpha = strength*relative/max(1e-12,max(dot(relative,relative),core*core));
                // Only the unresolved near-horizon extension is bounded. The resolved lens
                // retains the point-mass inverse mapping, including secondary images and arcs.
                return alpha*min(1,rcp(max(length(alpha),1e-12)));
            }
            float4 Fragment(Varyings input) : SV_Target
            {
                if(_StellarFixture>.5)
                {
                    float2 alpha = StellarDeflection(_StellarFixtureInput.xy,
                        float4(0,0,_StellarFixtureLens.x,_StellarFixtureLens.y),_StellarFixtureLens.z,_StellarFixtureLens.w);
                    return float4(alpha,0,1);
                }
                float3 relativeSource = _FeaturedSource.xyz-_CameraOrigin.xyz;
                float sourceDepth = dot(relativeSource,_CameraForward.xyz);
                if(sourceDepth<=_FeaturedSource.w || _FeaturedSource.w<=0) return 0;
                float2 sourceCentre = float2(dot(relativeSource,_CameraRight.xyz),dot(relativeSource,_CameraUp.xyz))/sourceDepth;
                float sourceRadius = _FeaturedSource.w/sourceDepth;
                float2 theta = input.angularUnit*_Optics.zw;
                theta += StellarWaveDisplacement(theta);
                float2 beta = theta-StellarDeflection(theta,_LensA,_LensRadii.x,sourceDepth)
                    -StellarDeflection(theta,_LensB,_LensRadii.y,sourceDepth)
                    -StellarDeflection(theta,_LensR,_LensRadii.z,sourceDepth);
                float radial = length(beta-sourceCentre)/max(sourceRadius,1e-9);
                float antialias = clamp(.5*fwidth(radial),.002,.75);
                float coverage = 1-smoothstep(1-antialias,1+antialias,radial);
                float limb = .45+.55*sqrt(saturate(1-radial*radial));
                // Compact display glare follows the source through the same lens equation.
                // Nothing emits at the hole or anywhere lacking background source intensity.
                float glare = .04*exp(-.75*radial*radial)*(1-smoothstep(2.5,3,radial));
                return float4(_FeaturedColor.rgb*_FeaturedColor.a*(limb*coverage+glare),0);
            }
            ENDHLSL
        }
    }
}
