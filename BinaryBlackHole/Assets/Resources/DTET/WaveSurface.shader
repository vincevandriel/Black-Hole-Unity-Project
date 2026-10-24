Shader "DTET/WaveSurface"
{
    Properties
    {
        _WaveHistory("Shared retarded waveform and coverage",2D)="black"{}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            sampler2D _WaveHistory;
            float4 _WaveHistory_TexelSize, _WaveParameters;
            float _GridSegments;
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionHCS:SV_POSITION; float2 plane:TEXCOORD0; };

            float4 History(float radius)
            {
                float u=(saturate(radius)*(_WaveHistory_TexelSize.z-1)+.5)*_WaveHistory_TexelSize.x;
                return tex2Dlod(_WaveHistory,float4(u,.5,0,0));
            }
            float SignedStrain(float2 p,float4 history)
            {
                float r2=dot(p,p);
                // phi is measured from +X toward +Z in the orbital plane.
                float2 quadrupole=float2(p.x*p.x-p.y*p.y,2*p.x*p.y)/max(r2,1e-10);
                return dot(clamp(history.rg*_WaveParameters.y,-4,4),quadrupole);
            }
            float Envelope(float radius)
            {
                return smoothstep(.025,.065,radius)*(1-smoothstep(.85,1,radius));
            }
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float2 plane=input.positionOS.xz;
                float radius=length(plane);
                float4 history=History(radius);
                float available=radius<=1 && history.b>=.999 ? 1 : 0;
                input.positionOS.y=clamp(SignedStrain(plane,history),-1,1)*Envelope(radius)*available;
                output.positionHCS=TransformObjectToHClip(input.positionOS.xyz);
                output.plane=plane;
                return output;
            }
            float4 Fragment(Varyings input):SV_Target
            {
                float radius=length(input.plane);
                clip(1-radius);
                float4 history=History(radius);
                clip(history.b-.999);
                float signal=SignedStrain(input.plane,history);
                float amplitude=saturate(abs(signal));
                // Display lines stay sparse while the immutable surface uses finer triangles.
                float2 grid=(input.plane*.5+.5)*_GridSegments;
                float2 width=max(fwidth(grid),1e-4);
                float2 distance=abs(frac(grid+.5)-.5)/width;
                float gridLine=(1-smoothstep(.65,1.5,min(distance.x,distance.y)))
                    *(1-smoothstep(.45,1.2,max(width.x,width.y)));
                float alpha=Envelope(radius)*(.025+.075*amplitude+gridLine*(.17+.20*amplitude));
                float3 color=lerp(float3(1,.48,.08),float3(.05,.88,1),smoothstep(-.025,.025,signal));
                return float4(color*(1.35+amplitude)*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
