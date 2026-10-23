#ifndef DTET_GENERATED_FLUID_SAMPLING
#define DTET_GENERATED_FLUID_SAMPLING
sampler2D _GeneratedFrameA, _GeneratedFrameB;
float4 _GeneratedGrid; // half extent, Gaussian height, temporal interpolation, covered
float _GeneratedOpacity, _GeneratedExposure;
float4 SampleGeneratedFluid(float3 p)
{
    if (_GeneratedGrid.w < .5 || _GeneratedGrid.x <= 0 || any(abs(p.xz) > _GeneratedGrid.x)) return 0;
    float2 uv = (p.xz + _GeneratedGrid.x) / (2 * _GeneratedGrid.x);
    // Cell centres map to half texels; Clamp extends the nearest cell to the physical boundary.
    // The cache has one mip level. Explicit LOD avoids undefined screen derivatives
    // inside the view-dependent ray-march loop while retaining bilinear sampling.
    float3 value = lerp(tex2Dlod(_GeneratedFrameA, float4(uv, 0, 0)).rgb,
        tex2Dlod(_GeneratedFrameB, float4(uv, 0, 0)).rgb, _GeneratedGrid.z);
    float height = max(_GeneratedGrid.y, 1e-6);
    float rho = max(value.x, 0) * exp(-.5 * p.y * p.y / (height * height)) / (2.50662827463 * height);
    return float4(max(value.x, 0), rho, value.yz);
}
float3 GeneratedFluidTint(float2 velocity)
{
    // False colour indicates model speed; this is not a temperature or luminosity estimate.
    return lerp(float3(.95,.52,.19), float3(.55,.78,1), .35 * saturate(length(velocity)/.3));
}
#endif
