#ifndef DTET_CURVED_DISK_OPTICS_INCLUDED
#define DTET_CURVED_DISK_OPTICS_INCLUDED

float4 _DiskLensA, _DiskLensB, _DiskLensR;
float4 _DiskWeights; // Binary, remnant, curvature enabled, Doppler enabled.
float4 _DiskDomain; // Gas half extent, assumed height, optical boundary radius, ray budget.
float4 _HorizonA, _HorizonB, _HorizonR;
float _DopplerStrength;
float _MaterialDetailStrength;
float4 _DiskSampling; // Cell width, grid size, render width and height.
float4 _DiskVertical; // Assumed sound speed and softened-potential lengths for A, B and remnant.
float4 _GeneratedFrameA_TexelSize;

float DiskCellWidth()
{
    return _DiskSampling.x>0?_DiskSampling.x:
        2*_GeneratedGrid.x*max(_GeneratedFrameA_TexelSize.x,1.0/256);
}
float DiskVerticalFrequencySquared(float2 xz,float4 lens,float epsilon,float horizon)
{
    if(lens.w<=0)return 0;
    float softening=epsilon>0?epsilon:(horizon>0?.5*horizon:lens.w);
    float2 delta=xz-lens.xz;
    float r2=dot(delta,delta)+max(softening*softening,1e-8);
    return lens.w/(r2*sqrt(r2));
}
float DiskScaleHeight(float2 xz)
{
    float omega2=_DiskWeights.x*(DiskVerticalFrequencySquared(xz,_DiskLensA,_DiskVertical.y,_HorizonA.w)
        +DiskVerticalFrequencySquared(xz,_DiskLensB,_DiskVertical.z,_HorizonB.w))
        +_DiskWeights.y*DiskVerticalFrequencySquared(xz,_DiskLensR,_DiskVertical.w,_HorizonR.w);
    if(omega2<=0)return max(_GeneratedGrid.y,1e-6);
    float soundSpeed=_DiskVertical.x>0?_DiskVertical.x:.04;
    // Assumed local isothermal vertical equilibrium, normalized to the recorded surface-density column.
    return clamp(soundSpeed*rsqrt(omega2),max(.025,.4*DiskCellWidth()),max(.3,.12*_DiskDomain.x));
}
float4 DiskSampleMedium(float3 p,out float material)
{
    material=0;
    if(_GeneratedGrid.w<.5 || _GeneratedGrid.x<=0 || any(abs(p.xz)>_GeneratedGrid.x))return 0;
    float2 uv=(p.xz+_GeneratedGrid.x)/(2*_GeneratedGrid.x);
    float4 value=lerp(tex2Dlod(_GeneratedFrameA,float4(uv,0,0)),
        tex2Dlod(_GeneratedFrameB,float4(uv,0,0)),_GeneratedGrid.z);
    float height=DiskScaleHeight(p.xz),sigma=max(value.x,0);
    float rho=sigma*exp(-.5*p.y*p.y/(height*height))/(2.50662827463*height);
    float label=clamp(value.w,-1,1);
    // Fixed emissivity labels follow transported material; these are not extra density or simulated turbulence.
    material=.5*(.65*sin(13*label)+.35*sin(29*label))*saturate(_MaterialDetailStrength);
    return float4(sigma,rho,value.yz);
}

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
float3 DiskEmission(float3 p,float4 field,float3 towardObserver,float observerLapse,float material)
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
    float speedHeat=sqrt(saturate(speed2/.3));
    // An assumed emission closure; the isothermal flow has no measured temperature or energy equation.
    // Passive material concentration changes emissivity, never the transported gas density.
    float thermalVariation=exp2(clamp(material,-.5,.5));
    float heat=(.35+.55*speedHeat+.55*sqrt(sqrt(contrast)))*thermalVariation;
    float observedHeat=heat*shift;
    // Chromaticity only, not an absolute Planck spectrum; the frequency-shift fourth power is applied once below.
    float3 tint=lerp(float3(1,.075,.005),float3(1,.42,.07),saturate((observedHeat-.25)/.4));
    tint=lerp(tint,float3(1,.84,.52),saturate((observedHeat-.65)/.45));
    tint=lerp(tint,float3(.78,.89,1),saturate((observedHeat-1.1)/.5));
    float source=(.08+1.4*contrast*contrast)*(.25+2*saturate(speed2/.2));
    float thermal2=thermalVariation*thermalVariation;
    source*=thermal2*thermal2;
    float aperture=1-smoothstep(.9,1,length(p.xz)/max(_DiskDomain.x,1e-5));
    float shift2=shift*shift;
    return tint*source*shift2*shift2*aperture;
}

void DiskIntegrateMedium(float3 p,float3 direction,float ds,float observerLapse,
    inout float3 radiance,inout float opacity)
{
    float material;
    float4 field=DiskSampleMedium(p,material);
    float tau=max(0,_GeneratedOpacity*field.y*ds);
    float absorb=tau<1e-3?tau*(1-tau*(.5-tau/6)):1-exp(-tau);
    radiance+=(1-opacity)*absorb*DiskEmission(p,field,-direction,observerLapse,material);
    opacity+=(1-opacity)*absorb;
}
#endif
