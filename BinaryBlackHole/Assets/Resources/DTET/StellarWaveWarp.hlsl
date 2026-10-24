#ifndef DTET_STELLAR_WAVE_WARP
#define DTET_STELLAR_WAVE_WARP
sampler2D _WaveHistory;
float4 _WaveHistory_TexelSize;
float4 _CameraOrigin,_CameraRight,_CameraUp,_CameraForward;
float4 _LensA,_LensB,_LensR,_LensRadii;
float4 _Optics,_WaveParameters,_WaveBasisX,_WaveBasisY,_Polarization;

// Inverse angular lookup affects only background light; gas and proxies draw later.
float2 StellarWaveDisplacement(float2 theta)
{
    if(_Optics.y<=0||_WaveParameters.x<=0)return 0;
    float3 rd=normalize(_CameraForward.xyz+theta.x*_CameraRight.xyz+theta.y*_CameraUp.xyz);
    float3 ro=_CameraOrigin.xyz;
    float3 probe=ro+rd*max(0,-dot(ro,rd));
    float coordinate=length(probe)*_WaveParameters.w/_WaveParameters.x;
    if(coordinate>1)return 0;
    float u=(coordinate*(_WaveHistory_TexelSize.z-1)+.5)*_WaveHistory_TexelSize.x;
    // Explicit LOD shares history sampling between star vertex and fragment shaders.
    float4 history=tex2Dlod(_WaveHistory,float4(u,.5,0,0));
    if(history.b<.999)return 0;
    float2 signal=clamp(history.rg*_WaveParameters.y,-4,4);
    float2 local=float2(dot(probe,_WaveBasisX.xyz),dot(probe,_WaveBasisY.xyz))/max(1,_WaveBasisX.w);
    float scale=.055*clamp(_Optics.y,0,2)/(.18+length(local));
    float2 delta=scale*float2(signal.x*local.x+signal.y*local.y,signal.y*local.x-signal.x*local.y);
    delta*=min(1,.035/max(length(delta),1e-12));
    return (delta.x*_Polarization.xy+delta.y*_Polarization.zw)*(1-smoothstep(.85,1,coordinate));
}
#endif
