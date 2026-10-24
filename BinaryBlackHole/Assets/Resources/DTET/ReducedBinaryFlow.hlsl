#ifndef DTET_REDUCED_BINARY_FLOW_INCLUDED
#define DTET_REDUCED_BINARY_FLOW_INCLUDED
// Exact prescribed M4 field counterpart of ReducedBinaryFlowModel.cs.
// All positions are in total-source-mass rg, Unity XZ=SXS XY. No fluid solver.
float4 _FlowA, _FlowB, _FlowRemnant; // position xyz, mass fraction
float4 _FlowARadii, _FlowBRadii, _FlowRemnantRadii; // inner, outer, equal-area proxy, unused
float4 _FlowReservoir; // inner, outer, cos(2 pattern phase), sin(2 pattern phase)
float4 _FlowWeights; // binary weight, remnant weight, common horizon flag, transition gas weight
float4 _FlowBounds; // half extent XZ, half extent Y, unused, unused
float4 _FlowStreamAStart, _FlowStreamAEnd, _FlowStreamABow; // bow xyz unit normal; w chord length squared
float4 _FlowStreamBStart, _FlowStreamBEnd, _FlowStreamBBow;

float FlowSmooth(float value)
{
    value = saturate(value);
    return value * value * (3.0 - 2.0 * value);
}

float2 FlowRing(float3 p, float inner, float outer, float aspect, float strength,
    float temperatureScale, float contrast, float2 patternPhase)
{
    float radius2 = dot(p.xz, p.xz);
    if (outer <= inner || radius2 <= inner * inner || radius2 >= outer * outer) return 0;
    float radius = sqrt(radius2);
    float height = p.y / (aspect * radius);
    if (abs(height) >= 1.0) return 0;
    float u = (radius - inner) / (outer - inner);
    float radial = 4.0 * u * (1.0 - u), vertical = 1.0 - height * height;
    float pattern = 1.0 + contrast * (((p.x * p.x - p.z * p.z) * patternPhase.x
        + 2.0 * p.x * p.z * patternPhase.y) / radius2);
    float density = strength * radial * radial * vertical * vertical * pattern;
    float ratio = inner / radius;
    float t4 = temperatureScale * temperatureScale;
    t4 *= t4 * ratio * ratio * ratio;
    return float2(density, density * t4);
}

float2 FlowStream(float3 p, float4 start, float4 end, float4 bow, float weight)
{
    float2 chord = end.xz - start.xz;
    float u = dot(p.xz - start.xz, chord) / bow.w;
    if (u <= 0.0 || u >= 1.0) return 0;
    float3 center = lerp(start.xyz, end.xyz, u) + bow.xyz * (10.0 * u * (1.0 - u));
    float3 delta = p - center;
    float ellipse = dot(delta.xz, delta.xz) / 1.96 + delta.y * delta.y / .64;
    if (ellipse >= 1.0) return 0;
    float along = 4.0 * u * (1.0 - u), across = 1.0 - ellipse;
    float density = .9 * weight * along * along * across * across;
    float temperature = .4 + .5 * u, t4 = temperature * temperature;
    t4 *= t4;
    return float2(density, density * t4);
}

float FlowHorizonMask(float3 p, float4 body, float4 radii)
{
    return FlowSmooth((length(p - body.xyz) / radii.z - 1.0) / .2);
}

// Returns density and normalized bolometric emission; gray LTE source is y/x.
// Field is observer-independent.
float2 SampleReducedFlow(float3 p)
{
    if (abs(p.x) >= _FlowBounds.x || abs(p.z) >= _FlowBounds.x || abs(p.y) >= _FlowBounds.y) return 0;
    float2 value = FlowRing(p, _FlowReservoir.x, _FlowReservoir.y, .18, .55, .35, .2, _FlowReservoir.zw);
    if (_FlowWeights.x > 0.0)
    {
        value += FlowRing(p - _FlowA.xyz, _FlowARadii.x, _FlowARadii.y, .24,
            3.0 * _FlowWeights.x * FlowSmooth(_FlowARadii.y - _FlowARadii.x), 1.1, 0, float2(1, 0));
        value += FlowRing(p - _FlowB.xyz, _FlowBRadii.x, _FlowBRadii.y, .24,
            3.0 * _FlowWeights.x * FlowSmooth(_FlowBRadii.y - _FlowBRadii.x), 1.1, 0, float2(1, 0));
        value += FlowStream(p, _FlowStreamAStart, _FlowStreamAEnd, _FlowStreamABow, _FlowWeights.x);
        value += FlowStream(p, _FlowStreamBStart, _FlowStreamBEnd, _FlowStreamBBow, _FlowWeights.x);
    }
    if (_FlowWeights.y > 0.0)
        value += FlowRing(p - _FlowRemnant.xyz, _FlowRemnantRadii.x, _FlowRemnantRadii.y, .24,
            2.0 * _FlowWeights.y, 1.1, 0, float2(1, 0));
    if (_FlowWeights.w > 0.0)
        value += FlowRing(p, 4, 16, .24, 1.5 * _FlowWeights.w, 1.1, .2, _FlowReservoir.zw);
    if (value.x <= 0.0) return 0;
    float mask = _FlowWeights.z > .5 ? FlowHorizonMask(p, _FlowRemnant, _FlowRemnantRadii)
        : FlowHorizonMask(p, _FlowA, _FlowARadii) * FlowHorizonMask(p, _FlowB, _FlowBRadii);
    return value * mask;
}

// Flow-aligned emissivity bands aid readability; no resolved turbulence or measured gas.
float3 ReducedFlowDisplayEmission(float3 p, float2 field)
{
    if (field.x <= 1e-10) return 0;
    float source = max(0, field.y / field.x);
    float temperature = sqrt(sqrt(source));
    float3 tint = lerp(float3(1, .06, .004), float3(1, .4, .07),
        saturate((temperature - .25) / .85));
    float3 local = p;
    float inner = _FlowReservoir.x, outer = _FlowReservoir.y, aspect = .18;
    bool disk = false;
    if (_FlowWeights.w > 0 && length(p.xz) > 4 && length(p.xz) < 16)
    { inner = 4; outer = 16; aspect = .24; disk = true; }
    if (_FlowWeights.x > 0)
    {
        float3 a = p - _FlowA.xyz, b = p - _FlowB.xyz;
        float ra = length(a.xz), rb = length(b.xz);
        if (ra > _FlowARadii.x && ra < _FlowARadii.y)
        { local = a; inner = _FlowARadii.x; outer = _FlowARadii.y; aspect = .24; disk = true; }
        else if (rb > _FlowBRadii.x && rb < _FlowBRadii.y)
        { local = b; inner = _FlowBRadii.x; outer = _FlowBRadii.y; aspect = .24; disk = true; }
    }
    if (_FlowWeights.y > 0)
    {
        float3 remnant = p - _FlowRemnant.xyz;
        float radius = length(remnant.xz);
        if (radius > _FlowRemnantRadii.x && radius < _FlowRemnantRadii.y)
        { local = remnant; inner = _FlowRemnantRadii.x; outer = _FlowRemnantRadii.y; aspect = .24; disk = true; }
    }
    float radius = length(local.xz);
    if (radius <= inner || radius >= outer || outer <= inner) return source * tint * 1.25;
    float2 direction = local.xz / max(radius, 1e-6);
    float2 azimuth = float2(direction.x * direction.x - direction.y * direction.y,
        2 * direction.x * direction.y);
    // The same orbit phase drives every viewpoint and rewind: no wall-clock noise.
    float2 phase = float2(dot(azimuth, _FlowReservoir.zw),
        azimuth.y * _FlowReservoir.z - azimuth.x * _FlowReservoir.w);
    float sine, cosine;
    sincos(13 * log(max(radius / max(inner, .1), 1e-5)), sine, cosine);
    float spiral = saturate(.5 + .5 * (phase.x * cosine + phase.y * sine));
    float filament = spiral * spiral; filament *= filament * filament;
    float radial = (radius - inner) / (outer - inner);
    float fine = .5 + .5 * sin(25 * radial + 3 * phase.y);
    fine *= fine; fine *= fine;
    // Concentrate emission near the midplane; preserve density and gray extinction.
    float height = local.y / max(aspect * radius, 1e-6);
    float midPlane = exp(-12 * height * height);
    float structure = .06 + 2.8 * filament + .4 * fine;
    float gain = disk ? 5 : .6;
    return source * tint * structure * midPlane * gain;
}
#endif
