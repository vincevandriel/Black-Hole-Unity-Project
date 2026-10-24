Shader "DTET/ExhibitSky"
{
    Properties
    {
        _Exposure ("Relative display exposure", Float) = 12
        [HideInInspector] _RenderSize ("Render target dimensions", Vector) = (1600,900,0.000625,0.001111111)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background+10" "RenderType"="Transparent" }
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
            #include "BoundedBinaryLensing.hlsl"
            #include "StellarWaveWarp.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Exposure;
                float4 _RenderSize;
                float4 _SkyLensA,_SkyLensB,_SkyLensR,_SkyLensRadii;
                float _SkyLensGain;
            CBUFFER_END
            struct Attributes { float4 direction:POSITION; float2 corner:TEXCOORD0; float4 color:COLOR; };
            struct Varyings
            {
                float4 position:SV_POSITION;
                float2 localPixels:TEXCOORD0;
                float4 color:COLOR;
                float sigma:TEXCOORD1;
                float visible:TEXCOORD2;
            };
            float2 SkyDeflection(float2 theta,float4 body,float radius)
            {
                if(body.w<=0||_SkyLensGain<=0)return 0;
                float3 positionVS=mul((float3x3)UNITY_MATRIX_V,body.xyz-_WorldSpaceCameraPos);
                float depth=-positionVS.z;
                if(depth<=.01)return 0;
                return BoundedSkyApparentOffset(theta-positionVS.xy/depth,depth,body.w,radius,_SkyLensGain);
            }
            Varyings Vertex(Attributes i)
            {
                Varyings o;
                // Distant directions rotate with the camera; no translation parallax.
                float3 directionVS = mul((float3x3)UNITY_MATRIX_V, i.direction.xyz);
                o.visible = -directionVS.z - 0.00001;
                if(o.visible>0)
                {
                    float depth=-directionVS.z;
                    float2 theta=directionVS.xy/depth;
                    float2 delta=SkyDeflection(theta,_SkyLensA,_SkyLensRadii.x)
                        +SkyDeflection(theta,_SkyLensB,_SkyLensRadii.y)+SkyDeflection(theta,_SkyLensR,_SkyLensRadii.z);
                    delta*=min(1,.22/max(length(delta),1e-12));
                    // Move centers before building quads; preserve each PSF's width and flux.
                    // Gas renders later and stays independent.
                    directionVS.xy+=delta*depth;
                    // Forward wave placement negates inverse lookup; move background centers only.
                    directionVS.xy-=StellarWaveDisplacement(directionVS.xy/depth)*depth;
                }
                float4 center = mul(UNITY_MATRIX_P, float4(directionVS, 0));
                float heightScale = _RenderSize.y / 900.0;
                o.sigma = max(.65, min(3, .52 + 1.8 * sqrt(max(i.color.a, 0))) * heightScale);
                o.localPixels = i.corner * (3 * o.sigma + .5);
                // All four corners have equal W, so the fragment coordinate is linear in pixels.
                o.position = float4(center.xy / max(.00001, center.w) + 2 * o.localPixels * _RenderSize.zw, 0, 1);
                o.color = float4(i.color.rgb, _Exposure * i.color.a * heightScale * heightScale);
                return o;
            }
            float ErfApprox(float x)
            {
                float a = abs(x), t = rcp(1 + .3275911 * a);
                float p = (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - .284496736) * t + .254829592) * t;
                return sign(x) * (1 - p * exp(-a * a));
            }
            float PixelInterval(float p, float sigma)
            {
                float bound = 3 * sigma, scale = rcp(1.4142135623730951 * sigma);
                return ErfApprox(clamp(p + .5, -bound, bound) * scale)
                    - ErfApprox(clamp(p - .5, -bound, bound) * scale);
            }
            float4 Fragment(Varyings i):SV_Target
            {
                clip(i.visible);
                // Pixel-area integration preserves flux across subpixel shifts and render resolutions.
                float mass = .25 * PixelInterval(i.localPixels.x, i.sigma)
                    * PixelInterval(i.localPixels.y, i.sigma) / .9946076967722628;
                return float4(i.color.rgb * (i.color.a * max(0, mass)), 0);
            }
            ENDHLSL
        }
    }
}
