using System;

namespace BlackHole.Staging.Simulation
{
    /// <summary>
    /// Compact prescribed gas illustration driven by the retained SXS horizon history.
    /// This does not solve hydrodynamics, conserve gas, or reconstruct gas in GW150914.
    /// Coordinates map SXS (X,Y,Z) to Unity (X,Z,Y), in total-source-mass rg=GM/c^2.
    /// Sample velocities are prescribed coordinate velocities, not local GR measurements.
    /// </summary>
    public sealed class ReducedBinaryFlowModel
    {
        public const string ModelId = "assumed-SXS0305-reduced-binary-flow-v1";
        public const double TemperatureScaleKelvin = 1e7;
        public const double GrayExtinctionPerRg = .16;
        public const double BinaryFadeTg = 120;
        public const double RemnantRiseTg = 160;
        public const double ReservoirAspect = .18;
        public const double DiskAspect = .24;
        public const double StreamWidthRg = 1.4;
        public const double StreamHeightRg = .8;
        public const double StreamBowRg = 2.5;
        private readonly NumericalEventHistory history;
        public double GravitationalRadiusMeters { get; }
        public double GravitationalTimeSeconds { get; }
        public double DurationSeconds => history.DurationSeconds;
        public double CommonHorizonElapsedSeconds => history.CommonHorizonElapsedSeconds;
        public double HalfExtentXZ { get; }
        public double HalfExtentY { get; }
        public double ReservoirInnerRadiusRg { get; }
        public double ReservoirOuterRadiusRg { get; }
        public double InitialSeparationRg { get; }

        public ReducedBinaryFlowModel(NumericalEventHistory history)
        {
            this.history = history ?? throw new ArgumentNullException(nameof(history));
            GravitationalTimeSeconds = history.WaveformSecondsPerNormalizedTime;
            GravitationalRadiusMeters = GravitationalTimeSeconds * history.SpeedOfLight;
            InitialSeparationRg = (history.Evaluate(0).SeparationMeters
                ?? throw new ArgumentException("A retained binary starting state is required.", nameof(history))) / GravitationalRadiusMeters;
            ReservoirInnerRadiusRg = 1.8 * InitialSeparationRg;
            ReservoirOuterRadiusRg = 3 * InitialSeparationRg;
            HalfExtentXZ = Math.Ceiling(ReservoirOuterRadiusRg + 2);
            HalfExtentY = Math.Ceiling(ReservoirAspect * ReservoirOuterRadiusRg + 1);
        }

        public ReducedBinaryFlowState EvaluateState(double elapsedSeconds)
        {
            NumericalEventState numerical = history.Evaluate(elapsedSeconds);
            double time = numerical.ElapsedSeconds / GravitationalTimeSeconds;
            double aligned = numerical.SourceReferenceAlignedSeconds / GravitationalTimeSeconds;
            double binary = 1 - SmoothStep((aligned + BinaryFadeTg) / BinaryFadeTg);
            double remnant = SmoothStep(aligned / RemnantRiseTg);
            double separation = numerical.SeparationMeters.GetValueOrDefault() / GravitationalRadiusMeters;
            ReducedBinaryFlowBody a = default, b = default, c = default;
            ReducedBinaryFlowStream sa = default, sb = default;
            if (numerical.BodyA.HasValue)
            {
                // A symmetric coordinate-time difference, clipped before the common-horizon switch.
                double dt = .25 * GravitationalTimeSeconds;
                double lo = Math.Max(0, numerical.ElapsedSeconds - dt);
                double hi = Math.Min(CommonHorizonElapsedSeconds - 1e-12, numerical.ElapsedSeconds + dt);
                NumericalEventState left = history.Evaluate(lo), right = history.Evaluate(hi);
                a = MakeBody(numerical.BodyA.Value, RequireHorizon(left.BodyA), RequireHorizon(right.BodyA), hi - lo, .3 * separation);
                b = MakeBody(RequireHorizon(numerical.BodyB), RequireHorizon(left.BodyB), RequireHorizon(right.BodyB), hi - lo, .3 * separation);
                sa = MakeStream(a); sb = MakeStream(b);
            }
            else
            {
                double dt = .25 * GravitationalTimeSeconds;
                double lo = Math.Max(CommonHorizonElapsedSeconds, numerical.ElapsedSeconds - dt);
                double hi = Math.Min(DurationSeconds, numerical.ElapsedSeconds + dt);
                c = MakeBody(RequireHorizon(numerical.Remnant), RequireHorizon(history.Evaluate(lo).Remnant),
                    RequireHorizon(history.Evaluate(hi).Remnant), hi - lo, 16);
            }
            double mid = .5 * (ReservoirInnerRadiusRg + ReservoirOuterRadiusRg);
            double phase = time / Math.Sqrt(mid * mid * mid);
            return new ReducedBinaryFlowState(numerical.ElapsedSeconds, time, aligned, separation,
                binary, remnant, ReservoirInnerRadiusRg, ReservoirOuterRadiusRg,
                Math.Cos(2 * phase), Math.Sin(2 * phase), a, b, c, sa, sb);
        }

        public ReducedBinaryFlowSample Sample(ReducedBinaryFlowState state, double xRg, double yRg, double zRg)
        {
            if (state.ReservoirInnerRadiusRg <= 0) throw new ArgumentException("Use a state returned by EvaluateState.", nameof(state));
            ReferenceFlowModel.RequireFinite(xRg, nameof(xRg));
            ReferenceFlowModel.RequireFinite(yRg, nameof(yRg));
            ReferenceFlowModel.RequireFinite(zRg, nameof(zRg));
            if (Math.Abs(xRg) >= HalfExtentXZ || Math.Abs(yRg) >= HalfExtentY || Math.Abs(zRg) >= HalfExtentXZ)
                return default;
            double density = 0, emission = 0, vx = 0, vy = 0, vz = 0;
            AccumulateRing(xRg, yRg, zRg, state.ReservoirInnerRadiusRg, state.ReservoirOuterRadiusRg,
                ReservoirAspect, .55, .35, 1, .004, default,
                state.PatternCos2, state.PatternSin2, .2, ref density, ref emission, ref vx, ref vy, ref vz);
            if (state.BinaryWeight > 0)
            {
                AccumulateMini(state.BodyA, state.BinaryWeight, xRg, yRg, zRg, ref density, ref emission, ref vx, ref vy, ref vz);
                AccumulateMini(state.BodyB, state.BinaryWeight, xRg, yRg, zRg, ref density, ref emission, ref vx, ref vy, ref vz);
                AccumulateStream(state.StreamA, state.BinaryWeight, xRg, yRg, zRg, ref density, ref emission, ref vx, ref vy, ref vz);
                AccumulateStream(state.StreamB, state.BinaryWeight, xRg, yRg, zRg, ref density, ref emission, ref vx, ref vy, ref vz);
            }
            if (state.RemnantWeight > 0)
            {
                ReducedBinaryFlowBody body = state.Remnant;
                AccumulateRing(xRg - body.PositionRg.X, yRg - body.PositionRg.Y, zRg - body.PositionRg.Z,
                    body.InnerRadiusRg, body.OuterRadiusRg, DiskAspect, 2 * state.RemnantWeight, 1.1,
                    body.MassFraction, .008, body.VelocityOverC, 1, 0, 0,
                    ref density, ref emission, ref vx, ref vy, ref vz);
            }
            if (density <= 0) return default;
            // Coordinate equal-area-sphere mask, not a ray-traced horizon or optical shadow.
            double mask = state.IsCommonHorizon
                ? HorizonMask(state.Remnant, xRg, yRg, zRg)
                : HorizonMask(state.BodyA, xRg, yRg, zRg) * HorizonMask(state.BodyB, xRg, yRg, zRg);
            return new ReducedBinaryFlowSample(density * mask, emission * mask, vx / density, vy / density, vz / density);
        }

        private ReducedBinaryFlowBody MakeBody(NumericalHorizonState current, NumericalHorizonState left,
            NumericalHorizonState right, double deltaSeconds, double outer)
        {
            double mass = current.MassSolar / history.TotalMassSolar;
            NumericalVector3 position = Map(current.PositionMeters, 1 / GravitationalRadiusMeters);
            NumericalVector3 l = left.PositionMeters, r = right.PositionMeters;
            double vscale = deltaSeconds > 0 ? 1 / (deltaSeconds * history.SpeedOfLight) : 0;
            var velocity = new NumericalVector3((r.X - l.X) * vscale, (r.Z - l.Z) * vscale, (r.Y - l.Y) * vscale);
            return new ReducedBinaryFlowBody(position, velocity, mass, current.ArealRadiusMeters / GravitationalRadiusMeters,
                6 * mass, outer);
        }

        private ReducedBinaryFlowStream MakeStream(ReducedBinaryFlowBody body)
        {
            double angle = Math.Atan2(body.PositionRg.Z, body.PositionRg.X) + .55;
            var start = new NumericalVector3(1.03 * ReservoirInnerRadiusRg * Math.Cos(angle), 0,
                1.03 * ReservoirInnerRadiusRg * Math.Sin(angle));
            double dx = start.X - body.PositionRg.X, dz = start.Z - body.PositionRg.Z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            double targetRadius = Math.Max(body.HorizonRadiusRg * 1.25, .8 * body.OuterRadiusRg);
            var end = new NumericalVector3(body.PositionRg.X + dx / distance * targetRadius,
                body.PositionRg.Y, body.PositionRg.Z + dz / distance * targetRadius);
            double cx = end.X - start.X, cz = end.Z - start.Z;
            double length = Math.Sqrt(cx * cx + cz * cz);
            return new ReducedBinaryFlowStream(start, end, new NumericalVector3(-cz / length, 0, cx / length),
                cx * cx + cz * cz);
        }

        private static NumericalVector3 Map(NumericalVector3 value, double scale)
            => new NumericalVector3(value.X * scale, value.Z * scale, value.Y * scale);

        private static NumericalHorizonState RequireHorizon(NumericalHorizonState? value)
            => value ?? throw new InvalidOperationException("Required retained horizon is missing.");

        private static void AccumulateMini(ReducedBinaryFlowBody body, double binary, double x, double y, double z,
            ref double rho, ref double emission, ref double vx, ref double vy, ref double vz)
        {
            double strength = 3 * binary * SmoothStep(body.OuterRadiusRg - body.InnerRadiusRg);
            if (strength <= 0) return;
            AccumulateRing(x - body.PositionRg.X, y - body.PositionRg.Y, z - body.PositionRg.Z,
                body.InnerRadiusRg, body.OuterRadiusRg, DiskAspect, strength, 1.1, body.MassFraction,
                .008, body.VelocityOverC, 1, 0, 0, ref rho, ref emission, ref vx, ref vy, ref vz);
        }

        private static void AccumulateRing(double x, double y, double z, double inner, double outer, double aspect,
            double strength, double temperatureScale, double mass, double inwardSpeed, NumericalVector3 translation,
            double cosPhase, double sinPhase, double contrast,
            ref double density, ref double emission, ref double vx, ref double vy, ref double vz)
        {
            double radius2 = x * x + z * z;
            if (outer <= inner || radius2 <= inner * inner || radius2 >= outer * outer) return;
            double radius = Math.Sqrt(radius2), h = y / (aspect * radius);
            if (Math.Abs(h) >= 1) return;
            double u = (radius - inner) / (outer - inner), radial = 4 * u * (1 - u), vertical = 1 - h * h;
            double pattern = 1 + contrast * ((x * x - z * z) / radius2 * cosPhase + 2 * x * z / radius2 * sinPhase);
            double rho = strength * radial * radial * vertical * vertical * pattern;
            double ratio = inner / radius;
            // T/T0 = scale*(rin/R)^(3/4); emission uses T^4 directly, with no pow in the shader.
            double t4 = temperatureScale * temperatureScale; t4 *= t4 * ratio * ratio * ratio;
            double omega = Math.Sqrt(mass / (radius2 * radius));
            density += rho; emission += rho * t4;
            vx += rho * (translation.X - omega * z - inwardSpeed * x / radius);
            vy += rho * translation.Y;
            vz += rho * (translation.Z + omega * x - inwardSpeed * z / radius);
        }

        private static void AccumulateStream(ReducedBinaryFlowStream stream, double binary, double x, double y, double z,
            ref double density, ref double emission, ref double vx, ref double vy, ref double vz)
        {
            double cx = stream.EndRg.X - stream.StartRg.X, cz = stream.EndRg.Z - stream.StartRg.Z;
            double u = ((x - stream.StartRg.X) * cx + (z - stream.StartRg.Z) * cz) / stream.ChordLengthSquaredRg2;
            if (u <= 0 || u >= 1) return;
            double bow = 4 * StreamBowRg * u * (1 - u);
            double dx = x - (stream.StartRg.X + u * cx + stream.BowNormal.X * bow);
            double dz = z - (stream.StartRg.Z + u * cz + stream.BowNormal.Z * bow);
            double dy = y - (stream.StartRg.Y + u * (stream.EndRg.Y - stream.StartRg.Y));
            double ellipse = (dx * dx + dz * dz) / (StreamWidthRg * StreamWidthRg)
                + dy * dy / (StreamHeightRg * StreamHeightRg);
            if (ellipse >= 1) return;
            double along = 4 * u * (1 - u), across = 1 - ellipse;
            double rho = .9 * binary * along * along * across * across;
            double temperature = .4 + .5 * u, t4 = temperature * temperature; t4 *= t4;
            double tx = cx + stream.BowNormal.X * 4 * StreamBowRg * (1 - 2 * u);
            double tz = cz + stream.BowNormal.Z * 4 * StreamBowRg * (1 - 2 * u);
            double ty = stream.EndRg.Y - stream.StartRg.Y;
            double speed = (.12 + .13 * u) / Math.Sqrt(tx * tx + ty * ty + tz * tz);
            density += rho; emission += rho * t4;
            vx += rho * speed * tx; vy += rho * speed * ty; vz += rho * speed * tz;
        }

        private static double HorizonMask(ReducedBinaryFlowBody body, double x, double y, double z)
        {
            double dx = x - body.PositionRg.X, dy = y - body.PositionRg.Y, dz = z - body.PositionRg.Z;
            return SmoothStep((Math.Sqrt(dx * dx + dy * dy + dz * dz) / body.HorizonRadiusRg - 1) / .2);
        }

        public static double SmoothStep(double value)
        { value = Math.Max(0, Math.Min(1, value)); return value * value * (3 - 2 * value); }
    }

    public readonly struct ReducedBinaryFlowBody
    {
        public NumericalVector3 PositionRg { get; }
        public NumericalVector3 VelocityOverC { get; }
        public double MassFraction { get; }
        public double HorizonRadiusRg { get; }
        public double InnerRadiusRg { get; }
        public double OuterRadiusRg { get; }
        internal ReducedBinaryFlowBody(NumericalVector3 p, NumericalVector3 v, double m, double horizon, double inner, double outer)
        { PositionRg = p; VelocityOverC = v; MassFraction = m; HorizonRadiusRg = horizon; InnerRadiusRg = inner; OuterRadiusRg = outer; }
    }

    public readonly struct ReducedBinaryFlowStream
    {
        public NumericalVector3 StartRg { get; }
        public NumericalVector3 EndRg { get; }
        public NumericalVector3 BowNormal { get; }
        public double ChordLengthSquaredRg2 { get; }
        internal ReducedBinaryFlowStream(NumericalVector3 start, NumericalVector3 end, NumericalVector3 normal, double length2)
        { StartRg = start; EndRg = end; BowNormal = normal; ChordLengthSquaredRg2 = length2; }
    }

    public readonly struct ReducedBinaryFlowState
    {
        public double ElapsedSeconds { get; }
        public double TimeTg { get; }
        public double AlignedTimeTg { get; }
        public double SeparationRg { get; }
        public double BinaryWeight { get; }
        public double RemnantWeight { get; }
        public bool IsCommonHorizon => AlignedTimeTg >= 0;
        public double ReservoirInnerRadiusRg { get; }
        public double ReservoirOuterRadiusRg { get; }
        public double PatternCos2 { get; }
        public double PatternSin2 { get; }
        public ReducedBinaryFlowBody BodyA { get; }
        public ReducedBinaryFlowBody BodyB { get; }
        public ReducedBinaryFlowBody Remnant { get; }
        public ReducedBinaryFlowStream StreamA { get; }
        public ReducedBinaryFlowStream StreamB { get; }
        internal ReducedBinaryFlowState(double elapsed, double time, double aligned, double separation, double binary,
            double remnant, double rin, double rout, double cos, double sin, ReducedBinaryFlowBody a,
            ReducedBinaryFlowBody b, ReducedBinaryFlowBody c, ReducedBinaryFlowStream sa, ReducedBinaryFlowStream sb)
        { ElapsedSeconds = elapsed; TimeTg = time; AlignedTimeTg = aligned; SeparationRg = separation;
            BinaryWeight = binary; RemnantWeight = remnant; ReservoirInnerRadiusRg = rin; ReservoirOuterRadiusRg = rout;
            PatternCos2 = cos; PatternSin2 = sin; BodyA = a; BodyB = b; Remnant = c; StreamA = sa; StreamB = sb; }
    }

    public readonly struct ReducedBinaryFlowSample
    {
        public double RelativeDensity { get; }
        // Sum rho*(T/T0)^4, before the .16/rg gray absorption coefficient.
        public double NormalizedBolometricEmission { get; }
        public double TemperatureKelvin => RelativeDensity > 0
            ? ReducedBinaryFlowModel.TemperatureScaleKelvin * Math.Sqrt(Math.Sqrt(NormalizedBolometricEmission / RelativeDensity)) : 0;
        public double VelocityXOverC { get; }
        public double VelocityYOverC { get; }
        public double VelocityZOverC { get; }
        public double ExtinctionPerRg => ReducedBinaryFlowModel.GrayExtinctionPerRg * RelativeDensity;
        internal ReducedBinaryFlowSample(double rho, double emission, double vx, double vy, double vz)
        { RelativeDensity = rho; NormalizedBolometricEmission = emission; VelocityXOverC = vx; VelocityYOverC = vy; VelocityZOverC = vz; }
    }
}
