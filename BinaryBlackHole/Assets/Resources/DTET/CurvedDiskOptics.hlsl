#ifndef DTET_CURVED_DISK_OPTICS_INCLUDED
#define DTET_CURVED_DISK_OPTICS_INCLUDED

float4 _DiskLensA, _DiskLensB, _DiskLensR;
float4 _DiskWeights; // Binary, remnant, curvature enabled, Doppler enabled.
float4 _DiskDomain; // Gas half extent, assumed height, optical boundary radius, ray budget.
float4 _HorizonA, _HorizonB, _HorizonR;
float _DopplerStrength;

// Schwarzschild Fermat index in isotropic coordinates: n=(1+a)^3/(1-a), a=m/(2r).
// Summed log indices and the mapping of source coordinates are illustrative for the binary.
float3 DiskLogIndexGradient(float3 p,float4 lens)
{
    if(lens.w<=0)return 0;
    float3 delta=p-lens.xyz;
    float r2=max(dot(delta,delta),1e-10),r=sqrt(r2);
    float a=min(.95,.5*lens.w/r);
    return -a*(4-2*a)*delta/(r2*max(1-a*a,.02));
}
float3 DiskGradient(float3 p)
{
    return _DiskWeights.z*(_DiskWeights.x*(DiskLogIndexGradient(p,_DiskLensA)
        +DiskLogIndexGradient(p,_DiskLensB))+_DiskWeights.y*DiskLogIndexGradient(p,_DiskLensR));
}
float DiskLogLapse(float3 p,float4 lens)
{
    if(lens.w<=0)return 0;
    float a=min(.95,.5*lens.w/max(length(p-lens.xyz),1e-5));
    return log((1-a)/(1+a));
}
float DiskLapse(float3 p)
{
    return exp(_DiskWeights.x*(DiskLogLapse(p,_DiskLensA)+DiskLogLapse(p,_DiskLensB))
        +_DiskWeights.y*DiskLogLapse(p,_DiskLensR));
}
float DiskSphereDistance(float3 p,float4 sphere)
{
    return sphere.w>0?length(p-sphere.xyz)-sphere.w:1e10;
}
float DiskCaptureDistance(float3 p)
{
    if(_MergerActive>.5)return MergerSurfaceDistance(p);
    return min(DiskSphereDistance(p,_HorizonA),min(DiskSphereDistance(p,_HorizonB),DiskSphereDistance(p,_HorizonR)));
}
float DiskLensDistance(float3 p,float4 lens)
{
    return lens.w>0?length(p-lens.xyz):1e10;
}
float DiskNearestLens(float3 p)
{
    float binary=min(DiskLensDistance(p,_DiskLensA),DiskLensDistance(p,_DiskLensB));
    float remnant=DiskLensDistance(p,_DiskLensR);
    return min(_DiskWeights.x>1e-4?binary:1e10,_DiskWeights.y>1e-4?remnant:1e10);
}
void DiskAdvance(float3 p,float3 direction,float ds,out float3 next,out float3 nextDirection,out float3 midpoint,out float3 midpointDirection)
{
    float3 gradient=DiskGradient(p);
    float3 bend=gradient-direction*dot(direction,gradient);
    midpointDirection=normalize(direction+.5*ds*bend);
    midpoint=p+.5*ds*direction;
    gradient=DiskGradient(midpoint);
    bend=gradient-midpointDirection*dot(midpointDirection,gradient);
    nextDirection=normalize(direction+ds*bend);
    next=p+ds*midpointDirection;
    midpoint=.5*(p+next);
}
float3 DiskEmission(float3 p,float4 field,float3 towardObserver,float observerLapse)
{
    if(field.y<=1e-10)return 0;
    float3 velocity=float3(field.z,0,field.w);
    float speed2=dot(velocity,velocity);
    velocity*=min(1,.95*rsqrt(max(speed2,1e-10)));
    speed2=dot(velocity,velocity);
    float doppler=sqrt(max(1-speed2,.001))/max(.05,1-dot(velocity,towardObserver));
    doppler=lerp(1,doppler,saturate(_DopplerStrength));
    float shift=lerp(1,clamp(DiskLapse(p)/max(observerLapse,.02)*doppler,.12,2.5),saturate(_DiskWeights.w));
    float contrast=field.x/(field.x+.3);
    float source=(.1+1.6*contrast*contrast)*(.25+2*saturate(speed2/.2));
    float warmth=saturate((shift-.35)/1.3);
    float3 tint=lerp(float3(1,.12,.012),float3(1,.85,.55),warmth);
    float aperture=1-smoothstep(.9,1,length(p.xz)/max(_DiskDomain.x,1e-5));
    float shift2=shift*shift;
    return tint*source*shift2*shift2*aperture;
}
#endif
