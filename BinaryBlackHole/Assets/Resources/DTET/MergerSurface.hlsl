#ifndef DTET_MERGER_SURFACE_INCLUDED
#define DTET_MERGER_SURFACE_INCLUDED

float _MergerActive;
float4 _MergerA, _MergerB, _MergerR;
float4 _MergerShape; // Quintic blend, smooth-union width, hit epsilon.
float4 _MergerBoundsMin, _MergerBoundsMax;

float MergerSphereDistance(float3 p, float4 sphere)
{
    return length(p-sphere.xyz)-sphere.w;
}

float MergerSurfaceDistance(float3 p)
{
    float a=MergerSphereDistance(p,_MergerA), b=MergerSphereDistance(p,_MergerB);
    float width=_MergerShape.y;
    float h=saturate(.5+.5*(b-a)/max(width,1e-8));
    float binary=width>1e-8 ? lerp(b,a,h)-width*h*(1-h) : min(a,b);
    return lerp(binary,MergerSphereDistance(p,_MergerR),saturate(_MergerShape.x));
}

float MergerSphereHit(float3 ro,float3 rd,float4 sphere)
{
    if(sphere.w<=0)return 1e20;
    float3 delta=ro-sphere.xyz;
    float b=dot(delta,rd),c=dot(delta,delta)-sphere.w*sphere.w;
    if(c<=0)return 0;
    float discriminant=b*b-c;
    if(discriminant<0)return 1e20;
    float hit=-b-sqrt(discriminant);
    return hit>=0 ? hit : 1e20;
}

// Normalized world ray; miss=1e20, camera inside the opaque surface=0.
float MergerSurfaceHit(float3 ro,float3 rd)
{
    if(_MergerActive<.5 || !all(abs(ro)<1e12) || !(dot(rd,rd)>.5 && dot(rd,rd)<1.5))return 1e20;
    if(_MergerShape.x<=0)return min(MergerSphereHit(ro,rd,_MergerA),MergerSphereHit(ro,rd,_MergerB));
    if(_MergerShape.x>=1)return MergerSphereHit(ro,rd,_MergerR);
    if(MergerSurfaceDistance(ro)<=0)return 0;
    float3 safe=sign(rd+1e-20)*max(abs(rd),1e-7);
    float3 first=(_MergerBoundsMin.xyz-ro)/safe,last=(_MergerBoundsMax.xyz-ro)/safe;
    float3 nearPoint=min(first,last),farPoint=max(first,last);
    float start=max(0,max(nearPoint.x,max(nearPoint.y,nearPoint.z)));
    float end=min(farPoint.x,min(farPoint.y,farPoint.z));
    if(end<start)return 1e20;
    float t=start,epsilon=max(_MergerShape.z,1e-5);
    [loop]for(int step=0;step<256;step++)
    {
        float distance=MergerSurfaceDistance(ro+rd*t);
        if(distance<=epsilon)return t;
        t+=max(.9*distance,.25*epsilon);
        if(t>end)break;
    }
    return 1e20;
}

#endif
