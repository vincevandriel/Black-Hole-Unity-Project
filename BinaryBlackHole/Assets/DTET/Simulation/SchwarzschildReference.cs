using System;

namespace BlackHole.Staging.Simulation
{
    public enum SchwarzschildRayOutcome { Captured, Escaped, DiskHit, StepLimit, CoplanarDisk }

    public readonly struct SchwarzschildRayResult
    {
        public SchwarzschildRayOutcome Outcome { get; }
        // All lengths are in GM/c^2. These are Schwarzschild pseudo-Cartesian coordinates.
        public NumericalVector3 PositionM { get; }
        // Unit spatial direction in the local static orthonormal frame, pointing away from the camera.
        public NumericalVector3 BackwardLocalDirection { get; }
        public double ImpactParameterM { get; }
        public double SweptAngleRadians { get; }
        public double MaximumNullResidual { get; }
        public int Steps { get; }
        // NaN unless Outcome == DiskHit. The emitter follows a stable circular geodesic.
        public double ObservedToEmittedFrequencyRatio { get; }

        internal SchwarzschildRayResult(SchwarzschildRayOutcome outcome, NumericalVector3 position,
            NumericalVector3 direction, double impact, double angle, double residual, int steps, double shift)
        {
            Outcome = outcome; PositionM = position; BackwardLocalDirection = direction;
            ImpactParameterM = impact; SweptAngleRadians = angle; MaximumNullResidual = residual;
            Steps = steps; ObservedToEmittedFrequencyRatio = shift;
        }
    }

    // Independent direct-integration control for Bruneton's Schwarzschild optical model.
    // See Docs/M3_OPTICAL_REFERENCE.md for equations, units, comparisons and scope.
    // No lookup tables, shader code, textures or sky assets are copied from the upstream renderer.
    public static class SchwarzschildReference
    {
        public const double HorizonRadiusM = 2.0;
        public const double PhotonSphereRadiusM = 3.0;
        public const double InnermostStableCircularOrbitRadiusM = 6.0;
        public const double CaptureRadiusM = 2.000001;
        public static double CriticalImpactParameterM => 3.0 * Math.Sqrt(3.0);

        public static SchwarzschildRayResult Trace(NumericalVector3 observerPositionM,
            NumericalVector3 backwardLocalDirection, double escapeRadiusM = 1000,
            double angularStep = 0.005, int maximumSteps = 8192)
            => TraceCore(observerPositionM, backwardLocalDirection, false, default,
                6, 20, escapeRadiusM, angularStep, maximumSteps);

        public static SchwarzschildRayResult TraceDisk(NumericalVector3 observerPositionM,
            NumericalVector3 backwardLocalDirection, NumericalVector3 diskNormal,
            double innerRadiusM = 6, double outerRadiusM = 20, double escapeRadiusM = 1000,
            double angularStep = 0.005, int maximumSteps = 8192)
            => TraceCore(observerPositionM, backwardLocalDirection, true, diskNormal,
                innerRadiusM, outerRadiusM, escapeRadiusM, angularStep, maximumSteps);

        // g = nu_observer / nu_emitter, with both at rest in Schwarzschild coordinates.
        public static double StaticFrequencyRatio(double emitterRadiusM, double observerRadiusM)
        {
            RequireExterior(emitterRadiusM, nameof(emitterRadiusM));
            RequireExterior(observerRadiusM, nameof(observerRadiusM));
            return Math.Sqrt((1 - 2 / emitterRadiusM) / (1 - 2 / observerRadiusM));
        }

        // photonDirectionDotVelocity is the forward photon's local unit direction dotted with
        // the emitter's prograde unit velocity, both evaluated in the same local static frame.
        public static double CircularEmitterFrequencyRatio(double emitterRadiusM, double observerRadiusM,
            double photonDirectionDotVelocity)
        {
            if (!Finite(emitterRadiusM) || emitterRadiusM < InnermostStableCircularOrbitRadiusM)
                throw new ArgumentOutOfRangeException(nameof(emitterRadiusM), "Stable circular emitter requires r >= 6 GM/c^2.");
            RequireExterior(observerRadiusM, nameof(observerRadiusM));
            if (!Finite(photonDirectionDotVelocity) || Math.Abs(photonDirectionDotVelocity) > 1)
                throw new ArgumentOutOfRangeException(nameof(photonDirectionDotVelocity));
            double beta = 1 / Math.Sqrt(emitterRadiusM - 2);
            double gamma = 1 / Math.Sqrt(1 - beta * beta);
            return StaticFrequencyRatio(emitterRadiusM, observerRadiusM)
                / (gamma * (1 - beta * photonDirectionDotVelocity));
        }

        private static SchwarzschildRayResult TraceCore(NumericalVector3 observer, NumericalVector3 direction,
            bool disk, NumericalVector3 diskNormal, double inner, double outer,
            double escapeRadius, double angularStep, int maximumSteps)
        {
            RequireVector(observer, nameof(observer));
            double cameraRadius = observer.Magnitude;
            RequireExterior(cameraRadius, nameof(observer));
            if (!Finite(escapeRadius) || escapeRadius < cameraRadius)
                throw new ArgumentOutOfRangeException(nameof(escapeRadius), "Escape boundary must contain the observer.");
            if (!Finite(angularStep) || angularStep <= 0 || angularStep > 0.05)
                throw new ArgumentOutOfRangeException(nameof(angularStep));
            if (maximumSteps < 1 || maximumSteps > 1000000)
                throw new ArgumentOutOfRangeException(nameof(maximumSteps));
            direction = Unit(direction, nameof(direction));
            if (disk)
            {
                diskNormal = Unit(diskNormal, nameof(diskNormal));
                if (!Finite(inner) || !Finite(outer) || inner < 6 || outer <= inner || outer >= escapeRadius)
                    throw new ArgumentOutOfRangeException(nameof(inner), "Disk must satisfy 6 <= inner < outer < escape boundary.");
            }

            NumericalVector3 radialBasis = Scale(observer, 1 / cameraRadius);
            double radialCos = Clamp(Dot(direction, radialBasis), -1, 1);
            NumericalVector3 tangent = Add(direction, Scale(radialBasis, -radialCos));
            double tangentLength = tangent.Magnitude;
            double impact = cameraRadius * tangentLength / Math.Sqrt(1 - 2 / cameraRadius);
            if (tangentLength < 1e-12)
            {
                // Radial null geodesics keep their angles. They do not intersect an infinitely thin
                // disk except in the degenerate coplanar case, which is explicitly unavailable.
                if (disk && Math.Abs(Dot(radialBasis, diskNormal)) < 1e-12)
                    return new SchwarzschildRayResult(SchwarzschildRayOutcome.CoplanarDisk, observer,
                        direction, 0, 0, 0, 0, double.NaN);
                double radius = radialCos < 0 ? CaptureRadiusM : escapeRadius;
                return new SchwarzschildRayResult(radialCos < 0 ? SchwarzschildRayOutcome.Captured
                    : SchwarzschildRayOutcome.Escaped, Scale(radialBasis, radius), direction,
                    0, 0, 0, 0, double.NaN);
            }

            NumericalVector3 tangentBasis = Scale(tangent, 1 / tangentLength);
            double nextDiskAngle = double.PositiveInfinity;
            if (disk)
            {
                double a = Dot(radialBasis, diskNormal), b = Dot(tangentBasis, diskNormal);
                if (Math.Abs(a) + Math.Abs(b) < 1e-12)
                    return new SchwarzschildRayResult(SchwarzschildRayOutcome.CoplanarDisk, observer,
                        direction, impact, 0, 0, 0, double.NaN);
                // In the ray plane, disk crossings recur exactly every pi in azimuth.
                nextDiskAngle = Math.Atan2(-a, b);
                while (nextDiskAngle <= 1e-10) nextDiskAngle += Math.PI;
            }

            double u = 1 / cameraRadius, derivative = -radialCos / impact, angle = 0;
            double captureU = 1 / CaptureRadiusM, escapeU = 1 / escapeRadius;
            double maxResidual = NullResidual(u, derivative, impact);
            if (cameraRadius == escapeRadius && derivative < 0)
                return Result(SchwarzschildRayOutcome.Escaped, u, derivative, angle, impact,
                    maxResidual, 0, radialBasis, tangentBasis, diskNormal, cameraRadius);

            for (int step = 1; step <= maximumSteps; step++)
            {
                // Bound fractional radial changes as well as angle, including nearly radial rays.
                double h = Math.Min(angularStep, 0.04 * u / Math.Max(Math.Abs(derivative), 1e-14));
                bool crossesDisk = disk && nextDiskAngle - angle <= h;
                if (crossesDisk) h = nextDiskAngle - angle;
                double previousU = u, previousDerivative = derivative;
                Advance(previousU, previousDerivative, h, out double nextU, out double nextDerivative);

                bool captured = nextU >= captureU;
                bool escaped = nextU <= escapeU && nextDerivative < 0;
                if (captured || escaped)
                {
                    // Find the boundary in phi; do not linearly interpolate across a strong-field step.
                    double targetU = captured ? captureU : escapeU;
                    double lo = 0, hi = h;
                    for (int refinement = 0; refinement < 32; refinement++)
                    {
                        double middle = (lo + hi) * 0.5;
                        Advance(previousU, previousDerivative, middle, out double middleU, out _);
                        if ((captured && middleU < targetU) || (escaped && middleU > targetU)) lo = middle;
                        else hi = middle;
                    }
                    h = (lo + hi) * 0.5;
                    Advance(previousU, previousDerivative, h, out nextU, out nextDerivative);
                    maxResidual = Math.Max(maxResidual, NullResidual(nextU, nextDerivative, impact));
                    return Result(captured ? SchwarzschildRayOutcome.Captured : SchwarzschildRayOutcome.Escaped,
                        nextU, nextDerivative, angle + h, impact, maxResidual, step,
                        radialBasis, tangentBasis, diskNormal, cameraRadius);
                }

                u = nextU; derivative = nextDerivative; angle += h;
                maxResidual = Math.Max(maxResidual, NullResidual(u, derivative, impact));
                if (crossesDisk)
                {
                    double radius = 1 / u;
                    if (radius >= inner && radius <= outer)
                        return Result(SchwarzschildRayOutcome.DiskHit, u, derivative, angle, impact,
                            maxResidual, step, radialBasis, tangentBasis, diskNormal, cameraRadius);
                    nextDiskAngle += Math.PI;
                }
            }
            return Result(SchwarzschildRayOutcome.StepLimit, u, derivative, angle, impact,
                maxResidual, maximumSteps, radialBasis, tangentBasis, diskNormal, cameraRadius);
        }

        private static SchwarzschildRayResult Result(SchwarzschildRayOutcome outcome,
            double u, double derivative, double angle, double impact, double residual, int steps,
            NumericalVector3 radialBasis, NumericalVector3 tangentBasis,
            NumericalVector3 diskNormal, double observerRadius)
        {
            double cos = Math.Cos(angle), sin = Math.Sin(angle), radius = 1 / u;
            NumericalVector3 radial = Add(Scale(radialBasis, cos), Scale(tangentBasis, sin));
            NumericalVector3 tangent = Add(Scale(radialBasis, -sin), Scale(tangentBasis, cos));
            NumericalVector3 localDirection = Unit(Add(Scale(radial, -impact * derivative),
                Scale(tangent, impact * u * Math.Sqrt(Math.Max(0, 1 - 2 * u)))), "computed ray");
            double shift = double.NaN;
            if (outcome == SchwarzschildRayOutcome.DiskHit)
            {
                NumericalVector3 velocityDirection = Unit(Cross(diskNormal, radial), "disk velocity");
                // Integration points backwards, whereas Doppler uses the emitted forward photon.
                double mu = Clamp(-Dot(localDirection, velocityDirection), -1, 1);
                shift = CircularEmitterFrequencyRatio(radius, observerRadius, mu);
            }
            return new SchwarzschildRayResult(outcome, Scale(radial, radius), localDirection,
                impact, angle, residual, steps, shift);
        }

        private static void Advance(double u, double v, double h, out double newU, out double newV)
        {
            double a1 = 3 * u * u - u;
            double u2 = u + 0.5 * h * v, v2 = v + 0.5 * h * a1;
            double a2 = 3 * u2 * u2 - u2;
            double u3 = u + 0.5 * h * v2, v3 = v + 0.5 * h * a2;
            double a3 = 3 * u3 * u3 - u3;
            double u4 = u + h * v3, v4 = v + h * a3;
            double a4 = 3 * u4 * u4 - u4;
            newU = u + h * (v + 2 * v2 + 2 * v3 + v4) / 6;
            newV = v + h * (a1 + 2 * a2 + 2 * a3 + a4) / 6;
        }

        private static double NullResidual(double u, double v, double impact)
            => Math.Abs((v * v + u * u * (1 - 2 * u)) * impact * impact - 1);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static double Clamp(double value, double lo, double hi) => Math.Min(hi, Math.Max(lo, value));
        private static void RequireExterior(double radius, string name)
        {
            if (!Finite(radius) || radius <= CaptureRadiusM)
                throw new ArgumentOutOfRangeException(name, "Static observer/emitter requires r > 2.000001 GM/c^2.");
        }
        private static void RequireVector(NumericalVector3 value, string name)
        {
            if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z) || !Finite(value.Magnitude))
                throw new ArgumentOutOfRangeException(name);
        }
        private static NumericalVector3 Unit(NumericalVector3 value, string name)
        {
            RequireVector(value, name);
            double length = value.Magnitude;
            if (length <= 0) throw new ArgumentOutOfRangeException(name, "Direction must be nonzero.");
            return Scale(value, 1 / length);
        }
        private static double Dot(NumericalVector3 a, NumericalVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static NumericalVector3 Scale(NumericalVector3 a, double b) => new NumericalVector3(a.X * b, a.Y * b, a.Z * b);
        private static NumericalVector3 Add(NumericalVector3 a, NumericalVector3 b) => new NumericalVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        private static NumericalVector3 Cross(NumericalVector3 a, NumericalVector3 b)
            => new NumericalVector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }
}
