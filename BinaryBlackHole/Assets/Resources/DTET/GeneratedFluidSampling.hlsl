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
    // Explicit LOD keeps single-mip bilinear sampling valid inside ray-march loops.
    float3 value = lerp(tex2Dlod(_GeneratedFrameA, float4(uv, 0, 0)).rgb,
        tex2Dlod(_GeneratedFrameB, float4(uv, 0, 0)).rgb, _GeneratedGrid.z);
    float height = max(_GeneratedGrid.y, 1e-6);
    float rho = max(value.x, 0) * exp(-.5 * p.y * p.y / (height * height)) / (2.50662827463 * height);
    return float4(max(value.x, 0), rho, value.yz);
}
float3 GeneratedFluidTint(float2 velocity)
{
    // False colour shows model speed, not temperature or luminosity.
    return lerp(float3(1,.19,.025), float3(1,.73,.3), saturate(length(velocity)/.35));
}
float3 GeneratedFluidDisplayEmission(float3 p, float4 field)
{
    // Display transfer preserves stored flow and fixture values; no added filaments.
    float contrast = field.x / (field.x + .45);
    float speed = saturate(length(field.zw) / .35);
    float source = (.08 + 1.9 * contrast * contrast) * (.3 + 1.7 * speed * speed);
    float height = p.y / max(_GeneratedGrid.y, 1e-6);
    float midPlane = exp(-1.5 * height * height);
    // Fade the square display edge; sampling and mass budgets retain the full grid.
    float aperture = 1 - smoothstep(.82, .98, length(p.xz) / max(_GeneratedGrid.x, 1e-6));
    return GeneratedFluidTint(field.zw) * source * midPlane * aperture;
}
#endif
