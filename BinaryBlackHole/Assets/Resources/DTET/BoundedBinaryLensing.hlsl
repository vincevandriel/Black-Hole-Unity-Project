#ifndef DTET_BOUNDED_BINARY_LENSING_INCLUDED
#define DTET_BOUNDED_BINARY_LENSING_INCLUDED
// Shared with InteractiveOpticsReference.cs. A bounded weak-lens presentation,
// not strong-field image multiplicity, a binary metric, or a geodesic solver.
float2 BoundedLensOffset(float2 theta,float depth,float mass,float radius,float geometry,float gain)
{
    if(depth<=.01||mass<=0||gain<=0||geometry<=0)return 0;
    float soft=max(.75*max(0,radius),.15*mass)/depth;
    float strength=4*mass*geometry*min(gain,2)/(depth*max(1e-12,dot(theta,theta)+soft*soft));
    float cap=.30*min(mass,1),ratio=strength/cap;
    if(ratio>.5)strength=cap*(ratio>=1.5?1:1-.5*(1.5-ratio)*(1.5-ratio));
    float2 offset=-strength*theta;
    return offset*min(1,.16/max(length(offset),1e-12));
}
float2 BoundedSkyApparentOffset(float2 sourceAngle,float depth,float mass,float radius,float gain)
{
    // The inverse image lookup maps apparent angle to source angle. A batched star
    // instead needs a forward shift of its one source centre, away from the lens.
    return -BoundedLensOffset(sourceAngle,depth,mass,radius,1,gain);
}
#endif
