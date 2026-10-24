#nullable enable
using System;

namespace BlackHole.Staging.Simulation
{
    public enum GeneratedFluidBoundary { Outflow, Periodic }

    // Cell-average surface density and primitive planar velocity; units are Sigma0, rg/tg.
    public readonly struct GeneratedFluidSample
    {
        public double SurfaceDensity { get; }
        public double VelocityXRgPerTg { get; }
        public double VelocityZRgPerTg { get; }
        public GeneratedFluidSample(double density, double vx, double vz)
        { SurfaceDensity = density; VelocityXRgPerTg = vx; VelocityZRgPerTg = vz; }
    }

    // Prescribed Newtonian forcing without gas backreaction.
    public readonly struct GeneratedFluidSource
    {
        public double X { get; }
        public double Z { get; }
        public double MassFraction { get; }
        public double SofteningRg { get; }
        public double SinkRadiusRg { get; }
        public double SinkTimeTg { get; }
        public GeneratedFluidSource(double x, double z, double massFraction, double softeningRg = 2,
            double sinkRadiusRg = 3, double sinkTimeTg = 4)
        {
            RequireFinite(x); RequireFinite(z); RequireFinite(massFraction); RequireFinite(softeningRg);
            RequireFinite(sinkRadiusRg); RequireFinite(sinkTimeTg);
            if (massFraction < 0 || softeningRg <= 0 || sinkRadiusRg < 0 || sinkTimeTg <= 0)
                throw new ArgumentOutOfRangeException(nameof(massFraction));
            X = x; Z = z; MassFraction = massFraction; SofteningRg = softeningRg;
            SinkRadiusRg = sinkRadiusRg; SinkTimeTg = sinkTimeTg;
        }
        internal static void RequireFinite(double value)
        { if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
        internal void At(double x, double z, ref double gx, ref double gz, ref double sink)
        {
            if (MassFraction <= 0) return;
            double dx = x - X, dz = z - Z, r2 = dx * dx + dz * dz;
            double softened = r2 + SofteningRg * SofteningRg;
            double factor = -MassFraction / (softened * Math.Sqrt(softened));
            gx += factor * dx; gz += factor * dz;
            if (SinkRadiusRg > 0 && r2 < SinkRadiusRg * SinkRadiusRg)
            { double w = 1 - r2 / (SinkRadiusRg * SinkRadiusRg); sink += w * w / SinkTimeTg; }
        }
        internal double AccelerationBound => MassFraction <= 0 ? 0 :
            2 * MassFraction / (3 * Math.Sqrt(3) * SofteningRg * SofteningRg);
    }

    public readonly struct GeneratedFluidForcing
    {
        public GeneratedFluidSource A { get; }
        public GeneratedFluidSource B { get; }
        public GeneratedFluidForcing(GeneratedFluidSource a, GeneratedFluidSource b = default) { A = a; B = b; }
        public static GeneratedFluidForcing FromSnapshot(InteractiveBinarySnapshot snapshot)
        {
            GeneratedFluidSource Source(InteractiveBinaryBody body) => new GeneratedFluidSource(
                body.PositionRg.X, body.PositionRg.Z, body.MassFraction);
            return snapshot.Remnant.HasValue ? new GeneratedFluidForcing(Source(snapshot.Remnant.Value)) :
                new GeneratedFluidForcing(snapshot.BodyA.HasValue ? Source(snapshot.BodyA.Value) : default,
                    snapshot.BodyB.HasValue ? Source(snapshot.BodyB.Value) : default);
        }
        internal double AccelerationBound => A.AccelerationBound + B.AccelerationBound;
        public void Evaluate(double x, double z, out double accelerationX, out double accelerationZ, out double sinkRate)
        { accelerationX = accelerationZ = sinkRate = 0; A.At(x, z, ref accelerationX, ref accelerationZ, ref sinkRate);
            B.At(x, z, ref accelerationX, ref accelerationZ, ref sinkRate); }
    }

    public readonly struct GeneratedFluidDiagnostics
    {
        public double InitialMass { get; }
        public double FinalMass { get; }
        public double RemovedSinkMass { get; }
        public double NetOutflowMass { get; }
        public double AddedFloorMass { get; }
        public double MassBudgetResidual => FinalMass - (InitialMass - RemovedSinkMass - NetOutflowMass + AddedFloorMass);
        public long StepCount { get; }
        public double MaxCourantNumber { get; }
        public double MinimumDensity { get; }
        public double MaximumSpeed { get; }
        public long ReconstructionFallbackCount { get; }
        internal GeneratedFluidDiagnostics(double initial, double final, double sink, double outflow, double floor,
            long steps, double courant, double minimum, double speed, long fallbacks)
        { InitialMass = initial; FinalMass = final; RemovedSinkMass = sink; NetOutflowMass = outflow;
            AddedFloorMass = floor; StepCount = steps; MaxCourantNumber = courant; MinimumDensity = minimum; MaximumSpeed = speed;
            ReconstructionFallbackCount = fallbacks; }
    }

    // MUSCL/SSPRK2 isothermal Euler solver; conserved variables are Sigma, Sigma*vx and Sigma*vz.
    // Binary histories use outflow boundaries; periodic boundaries support solver tests.
    public sealed class GeneratedFluidSolver
    {
        public const double CourantLimit = .35;
        private const double TargetCourant = .20;
        private double[] density, momentumX, momentumZ, nextDensity, nextX, nextZ;
        private readonly double[] gravityX, gravityZ, sinkRate;
        private readonly double[] baseDensity, baseX, baseZ, slopeRhoX, slopeVxX, slopeVzX, slopeRhoZ, slopeVxZ, slopeVzZ;
        private readonly GeneratedFluidBoundary boundary;
        private double initialMass, sinkMass, outflowMass, floorMass, maxCourant, minDensity, maxSpeed;
        private double stageOutflow;
        private long steps, fallbacks;
        public int GridSize { get; }
        public double HalfExtentRg { get; }
        public double CellWidthRg { get; }
        public double SoundSpeed { get; }
        public double DensityFloor { get; }
        public long WorkingMemoryBytes => 18L * GridSize * GridSize * sizeof(double);
        public GeneratedFluidDiagnostics Diagnostics => new GeneratedFluidDiagnostics(initialMass, SumMass(),
            sinkMass, outflowMass, floorMass, steps, maxCourant, minDensity, maxSpeed, fallbacks);

        public GeneratedFluidSolver(int gridSize, double halfExtentRg, double soundSpeed, double densityFloor,
            Func<double, double, GeneratedFluidSample> initial, GeneratedFluidBoundary boundary = GeneratedFluidBoundary.Outflow)
        {
            if (gridSize < 8 || gridSize > 256) throw new ArgumentOutOfRangeException(nameof(gridSize));
            GeneratedFluidSource.RequireFinite(halfExtentRg); GeneratedFluidSource.RequireFinite(soundSpeed);
            GeneratedFluidSource.RequireFinite(densityFloor);
            if (halfExtentRg <= 0 || soundSpeed <= 0 || densityFloor <= 0) throw new ArgumentOutOfRangeException(nameof(halfExtentRg));
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (boundary != GeneratedFluidBoundary.Outflow && boundary != GeneratedFluidBoundary.Periodic)
                throw new ArgumentOutOfRangeException(nameof(boundary));
            GridSize = gridSize; HalfExtentRg = halfExtentRg; CellWidthRg = 2 * halfExtentRg / gridSize;
            SoundSpeed = soundSpeed; DensityFloor = densityFloor; this.boundary = boundary;
            int count = gridSize * gridSize;
            density = new double[count]; momentumX = new double[count]; momentumZ = new double[count];
            nextDensity = new double[count]; nextX = new double[count]; nextZ = new double[count];
            gravityX = new double[count]; gravityZ = new double[count]; sinkRate = new double[count];
            baseDensity = new double[count]; baseX = new double[count]; baseZ = new double[count];
            slopeRhoX = new double[count]; slopeVxX = new double[count]; slopeVzX = new double[count];
            slopeRhoZ = new double[count]; slopeVxZ = new double[count]; slopeVzZ = new double[count];
            minDensity = double.PositiveInfinity;
            for (int z = 0; z < gridSize; z++) for (int x = 0; x < gridSize; x++)
            {
                int i = z * gridSize + x;
                GeneratedFluidSample s = initial(CellCenter(x), CellCenter(z));
                GeneratedFluidSource.RequireFinite(s.SurfaceDensity); GeneratedFluidSource.RequireFinite(s.VelocityXRgPerTg);
                GeneratedFluidSource.RequireFinite(s.VelocityZRgPerTg);
                if (s.SurfaceDensity < densityFloor) throw new ArgumentException("Initial density must include its declared floor.");
                density[i] = s.SurfaceDensity; momentumX[i] = density[i] * s.VelocityXRgPerTg; momentumZ[i] = density[i] * s.VelocityZRgPerTg;
                minDensity = Math.Min(minDensity, density[i]);
                maxSpeed = Math.Max(maxSpeed, Math.Sqrt(s.VelocityXRgPerTg * s.VelocityXRgPerTg + s.VelocityZRgPerTg * s.VelocityZRgPerTg));
            }
            initialMass = SumMass();
        }
        public double CellCenter(int coordinate)
        { if (coordinate < 0 || coordinate >= GridSize) throw new ArgumentOutOfRangeException(nameof(coordinate));
            return -HalfExtentRg + (coordinate + .5) * CellWidthRg; }
        public GeneratedFluidSample GetCell(int x, int z)
        {
            if (x < 0 || z < 0 || x >= GridSize || z >= GridSize) throw new ArgumentOutOfRangeException(nameof(x));
            int i = z * GridSize + x;
            return new GeneratedFluidSample(density[i], momentumX[i] / density[i], momentumZ[i] / density[i]);
        }
        public double StableStep(double maximumStepTg, GeneratedFluidForcing forcing = default)
        {
            GeneratedFluidSource.RequireFinite(maximumStepTg);
            if (maximumStepTg <= 0) throw new ArgumentOutOfRangeException(nameof(maximumStepTg));
            double sx = SoundSpeed, sz = SoundSpeed;
            for (int i = 0; i < density.Length; i++)
            { sx = Math.Max(sx, Math.Abs(momentumX[i] / density[i]) + SoundSpeed);
                sz = Math.Max(sz, Math.Abs(momentumZ[i] / density[i]) + SoundSpeed); }
            // Bound both directional wave-speed increases from the full gravity kick.
            // Positive root of dt*(sx+sz+2*gBound*dt) <= CFL*dx, evaluated stably.
            double b = sx + sz, c = TargetCourant * CellWidthRg;
            double bound = 2 * c / (b + Math.Sqrt(b * b + 8 * forcing.AccelerationBound * c));
            return Math.Min(maximumStepTg, bound);
        }
        public void Step(double dt, GeneratedFluidForcing forcing = default)
        {
            double stable = StableStep(dt, forcing);
            if (dt > stable * (1 + 1e-12)) throw new ArgumentOutOfRangeException(nameof(dt), "Step exceeds the multidimensional gravity-aware CFL bound.");
            int n = GridSize;
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                forcing.Evaluate(CellCenter(x), CellCenter(z), out gravityX[i], out gravityZ[i], out sinkRate[i]);
            }
            // Symmetric source splitting with the same midpoint forcing. The source
            // map exactly integrates dv/dt=g and d(Sigma-floor)/dt=-rate*(Sigma-floor).
            ApplySources(.5 * dt);
            Array.Copy(density, baseDensity, density.Length); Array.Copy(momentumX, baseX, density.Length); Array.Copy(momentumZ, baseZ, density.Length);
            // SSPRK2: U1=U0+dt L(U0); U2=.5 U0+.5[U1+dt L(U1)].
            TransportStage(dt);
            TransportStage(dt);
            for (int i = 0; i < density.Length; i++)
            { density[i] = .5 * (baseDensity[i] + density[i]); momentumX[i] = .5 * (baseX[i] + momentumX[i]);
                momentumZ[i] = .5 * (baseZ[i] + momentumZ[i]); }
            ApplySources(.5 * dt); steps++;
        }
        private void ApplySources(double dt)
        {
            double area = CellWidthRg * CellWidthRg;
            for (int i = 0; i < density.Length; i++)
            {
                momentumX[i] += dt * density[i] * gravityX[i]; momentumZ[i] += dt * density[i] * gravityZ[i];
                if (sinkRate[i] > 0)
                {
                    double removed = (density[i] - DensityFloor) * (1 - Math.Exp(-sinkRate[i] * dt));
                    double retained = (density[i] - removed) / density[i]; sinkMass += removed * area;
                    momentumX[i] *= retained; momentumZ[i] *= retained; density[i] -= removed;
                }
                minDensity = Math.Min(minDensity, density[i]);
                maxSpeed = Math.Max(maxSpeed, Math.Sqrt(momentumX[i] * momentumX[i] + momentumZ[i] * momentumZ[i]) / density[i]);
            }
        }
        private void TransportStage(double dt)
        {
            double sx = SoundSpeed, sz = SoundSpeed;
            for (int i = 0; i < density.Length; i++)
            { sx = Math.Max(sx, Math.Abs(momentumX[i] / density[i]) + SoundSpeed);
                sz = Math.Max(sz, Math.Abs(momentumZ[i] / density[i]) + SoundSpeed); }
            double courant = dt * (sx + sz) / CellWidthRg;
            if (courant > CourantLimit * (1 + 1e-12))
                throw new InvalidOperationException("SSPRK stage exceeded its checked CFL bound; history rejected.");
            maxCourant = Math.Max(maxCourant, courant);
            ComputeSlopes();
            bool highOrder = courant <= .24;
            if (!highOrder) fallbacks++;
            ComputeTransport(dt, highOrder);
            if (!PositiveNext())
            {
                // Retry the whole stage with conservative first-order fluxes instead of clipping negative mass.
                if (!highOrder) throw new InvalidOperationException("Nonpositive first-order fluid stage; history rejected.");
                fallbacks++; ComputeTransport(dt, false);
                if (!PositiveNext()) throw new InvalidOperationException("Nonpositive fluid fallback; history rejected.");
            }
            double area = CellWidthRg * CellWidthRg;
            for (int i = 0; i < density.Length; i++)
            {
                double rho = nextDensity[i];
                if (rho < DensityFloor)
                {
                    double factor = DensityFloor / rho;
                    // Both Euler stages have weight 1/2 in the final SSPRK2 update.
                    floorMass += .5 * (DensityFloor - rho) * area;
                    nextX[i] *= factor; nextZ[i] *= factor; nextDensity[i] = DensityFloor;
                }
            }
            outflowMass += .5 * stageOutflow;
            Swap(ref density, ref nextDensity); Swap(ref momentumX, ref nextX); Swap(ref momentumZ, ref nextZ);
        }
        private bool PositiveNext()
        {
            for (int i = 0; i < density.Length; i++)
                if (!NumericalEventHistory.IsFinite(nextDensity[i]) || nextDensity[i] <= 0
                    || !NumericalEventHistory.IsFinite(nextX[i]) || !NumericalEventHistory.IsFinite(nextZ[i])) return false;
            return true;
        }
        private void ComputeTransport(double dt, bool highOrder)
        {
            int n = GridSize; double ratio = dt / CellWidthRg; stageOutflow = 0;
            Array.Copy(density, nextDensity, density.Length); Array.Copy(momentumX, nextX, density.Length); Array.Copy(momentumZ, nextZ, density.Length);
            // Every internal face contributes equal and opposite conserved fluxes.
            for (int z = 0; z < n; z++)
            {
                for (int x = 1; x < n; x++) InteriorFace(z * n + x - 1, z * n + x, true, ratio, highOrder);
                if (boundary == GeneratedFluidBoundary.Periodic) InteriorFace(z * n + n - 1, z * n, true, ratio, highOrder);
                else { BoundaryFace(z * n, true, -1, ratio, dt); BoundaryFace(z * n + n - 1, true, 1, ratio, dt); }
            }
            for (int x = 0; x < n; x++)
            {
                for (int z = 1; z < n; z++) InteriorFace((z - 1) * n + x, z * n + x, false, ratio, highOrder);
                if (boundary == GeneratedFluidBoundary.Periodic) InteriorFace((n - 1) * n + x, x, false, ratio, highOrder);
                else { BoundaryFace(x, false, -1, ratio, dt); BoundaryFace((n - 1) * n + x, false, 1, ratio, dt); }
            }
        }
        private void ComputeSlopes()
        {
            int n = GridSize;
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                int left = z * n + (x > 0 ? x - 1 : boundary == GeneratedFluidBoundary.Periodic ? n - 1 : x);
                int right = z * n + (x < n - 1 ? x + 1 : boundary == GeneratedFluidBoundary.Periodic ? 0 : x);
                int below = (z > 0 ? z - 1 : boundary == GeneratedFluidBoundary.Periodic ? n - 1 : z) * n + x;
                int above = (z < n - 1 ? z + 1 : boundary == GeneratedFluidBoundary.Periodic ? 0 : z) * n + x;
                double rho = density[i], vx = momentumX[i] / rho, vz = momentumZ[i] / rho;
                slopeRhoX[i] = Slope(density[left], rho, density[right]);
                slopeRhoZ[i] = Slope(density[below], rho, density[above]);
                slopeVxX[i] = Slope(momentumX[left] / density[left], vx, momentumX[right] / density[right]);
                slopeVzX[i] = Slope(momentumZ[left] / density[left], vz, momentumZ[right] / density[right]);
                slopeVxZ[i] = Slope(momentumX[below] / density[below], vx, momentumX[above] / density[above]);
                slopeVzZ[i] = Slope(momentumZ[below] / density[below], vz, momentumZ[above] / density[above]);
                double excursion = .5 * (Math.Abs(slopeRhoX[i]) + Math.Abs(slopeRhoZ[i]));
                if (excursion > rho - DensityFloor)
                {
                    double factor = Math.Max(0, (rho - DensityFloor) / excursion);
                    slopeRhoX[i] *= factor; slopeRhoZ[i] *= factor;
                }
            }
        }
        private static double Slope(double left, double center, double right)
        {
            double a = 1.5 * (center - left), b = .5 * (right - left), c = 1.5 * (right - center);
            if (a > 0 && b > 0 && c > 0) return Math.Min(a, Math.Min(b, c));
            if (a < 0 && b < 0 && c < 0) return Math.Max(a, Math.Max(b, c));
            return 0;
        }
        private void InteriorFace(int l, int r, bool xDirection, double ratio, bool highOrder)
        {
            double rhoL = density[l], rhoR = density[r], vxL = momentumX[l] / rhoL, vxR = momentumX[r] / rhoR;
            double vzL = momentumZ[l] / rhoL, vzR = momentumZ[r] / rhoR;
            if (highOrder)
            {
                rhoL += .5 * (xDirection ? slopeRhoX[l] : slopeRhoZ[l]); rhoR -= .5 * (xDirection ? slopeRhoX[r] : slopeRhoZ[r]);
                vxL += .5 * (xDirection ? slopeVxX[l] : slopeVxZ[l]); vxR -= .5 * (xDirection ? slopeVxX[r] : slopeVxZ[r]);
                vzL += .5 * (xDirection ? slopeVzX[l] : slopeVzZ[l]); vzR -= .5 * (xDirection ? slopeVzX[r] : slopeVzZ[r]);
            }
            double vl = xDirection ? vxL : vzL, vr = xDirection ? vxR : vzR;
            double speed = Math.Max(Math.Abs(vl), Math.Abs(vr)) + SoundSpeed, cs2 = SoundSpeed * SoundSpeed;
            double mass = .5 * (rhoL * vl + rhoR * vr - speed * (rhoR - rhoL));
            double fx = .5 * (rhoL * vxL * vl + rhoR * vxR * vr + (xDirection ? cs2 * (rhoL + rhoR) : 0)
                - speed * (rhoR * vxR - rhoL * vxL));
            double fz = .5 * (rhoL * vzL * vl + rhoR * vzR * vr + (xDirection ? 0 : cs2 * (rhoL + rhoR))
                - speed * (rhoR * vzR - rhoL * vzL));
            nextDensity[l] -= ratio * mass; nextDensity[r] += ratio * mass;
            nextX[l] -= ratio * fx; nextX[r] += ratio * fx; nextZ[l] -= ratio * fz; nextZ[r] += ratio * fz;
        }
        private void BoundaryFace(int i, bool xDirection, int outwardSign, double ratio, double dt)
        {
            double vx = momentumX[i] / density[i], vz = momentumZ[i] / density[i];
            // Diode/outflow face: no imposed external mass inflow. Normal pressure remains.
            double normal = xDirection ? vx : vz;
            normal = outwardSign * Math.Max(0, outwardSign * normal);
            if (xDirection) vx = normal; else vz = normal;
            double mass = density[i] * normal, pressure = SoundSpeed * SoundSpeed * density[i];
            nextDensity[i] -= outwardSign * ratio * mass;
            nextX[i] -= outwardSign * ratio * (mass * vx + (xDirection ? pressure : 0));
            nextZ[i] -= outwardSign * ratio * (mass * vz + (xDirection ? 0 : pressure));
            stageOutflow += outwardSign * mass * dt * CellWidthRg;
        }
        public void CopyFrame(float[] sigma, float[] vx, float[] vz, int offset = 0)
        {
            int length = density.Length;
            if (sigma == null || vx == null || vz == null || offset < 0 || offset > sigma.Length - length || offset > vx.Length - length || offset > vz.Length - length)
                throw new ArgumentException("Three non-null destination arrays need one grid of available elements.");
            for (int i = 0; i < length; i++)
            { sigma[offset + i] = (float)density[i]; vx[offset + i] = (float)(momentumX[i] / density[i]); vz[offset + i] = (float)(momentumZ[i] / density[i]); }
        }
        private double SumMass()
        { double sum = 0; for (int i = 0; i < density.Length; i++) sum += density[i]; return sum * CellWidthRg * CellWidthRg; }
        private static void Swap(ref double[] a, ref double[] b) { double[] temp = a; a = b; b = temp; }
    }
}
