Shader "DTET/StellarBackdrop"
{
    Properties
    {
        [HideInInspector] _Optics("Optics", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background+5" "RenderType"="Transparent" }
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
            struct Attributes { float4 position : POSITION; };
            struct Varyings { float4 position : SV_POSITION; float2 angularUnit : TEXCOORD0; };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.position = float4(input.position.xy, UNITY_RAW_FAR_CLIP_VALUE, 1);
                output.angularUnit = input.position.xy * float2(1, _ProjectionParams.x);
                return output;
            }
            float4 Hash4(float4 p)
            {
                p = frac(p * .1031);
                p *= p + 33.33;
                return frac(p * (p + p));
            }
            float Noise(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float n = dot(cell, float3(1, 57, 113));
                float4 a = Hash4(n + float4(0, 1, 57, 58));
                float4 b = Hash4(n + float4(113, 114, 170, 171));
                float2 low = lerp(a.xz, a.yw, f.x), high = lerp(b.xz, b.yw, f.x);
                return lerp(lerp(low.x, low.y, f.y), lerp(high.x, high.y, f.y), f.z);
            }
            float3 FaintStars(float3 direction, float frequency, float seed, float density)
            {
                float3 p = direction * frequency + seed;
                float3 cell = floor(p);
                float4 random = Hash4(dot(cell, float3(1, 57, 113)) + float4(0, 19, 47, 101));
                float3 local = frac(p) - .5 - (random.xyz - .5) * .35;
                float radius = lerp(.10, .20, random.z);
                float width = max(.003, .35 * length(fwidth(p)));
                float coverage = 1 - smoothstep(radius - width, radius + width, length(local));
                coverage *= radius * radius / (radius * radius + width * width);
                float intensity = coverage * step(1 - density, random.w) * lerp(.012, .065, random.x * random.x);
                return intensity * lerp(float3(.73, .85, 1), float3(1, .84, .64), random.y);
            }
            float2 BackgroundDeflection(float2 theta, float4 lens, float radius)
            {
                if (_Optics.x <= 0 || lens.z <= .01 || lens.w <= 0) return 0;
                return BoundedLensOffset(theta - lens.xy, lens.z, lens.w, radius, 1, _Optics.x);
            }
            float3 SourceLight(float3 direction)
            {
                // Fixed directions for faint stars and unresolved synthetic galactic light.
                float latitude = dot(direction, normalize(float3(.3, .8, .5)));
                float band = exp2(-36 * latitude * latitude);
                float broad = Noise(direction * 7 + float3(12, 31, 8));
                float cloud = Noise(direction * 30 + float3(4, 17, 53));
                float detail = Noise(direction * 92 + float3(71, 13, 5));
                float dust = smoothstep(.24, .65, Noise(direction * 18 + float3(9, 65, 24)));
                float light = band * (.002 + .013 * broad + .014 * cloud * detail) * (.35 + .65 * dust);
                float3 color = light * lerp(float3(.68, .78, 1), float3(1, .86, .70), broad);
                color += FaintStars(direction, 150, 37, .30);
                color += FaintStars(direction, 310, 83, .17) * .65;
                return color;
            }
            float4 Fragment(Varyings input) : SV_Target
            {
                float2 theta = input.angularUnit * _Optics.zw;
                theta += StellarWaveDisplacement(theta);
                float2 beta = theta + BackgroundDeflection(theta, _LensA, _LensRadii.x)
                    + BackgroundDeflection(theta, _LensB, _LensRadii.y)
                    + BackgroundDeflection(theta, _LensR, _LensRadii.z);
                float3 direction = normalize(_CameraForward.xyz + beta.x * _CameraRight.xyz + beta.y * _CameraUp.xyz);
                // Warp source lookup only; do not add luminous rings.
                return float4(SourceLight(direction), 0);
            }
            ENDHLSL
        }
    }
}
