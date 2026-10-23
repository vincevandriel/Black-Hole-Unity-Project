using System;

namespace BlackHole.Staging.Simulation
{
    /// <summary>Scalar transfer controls in declared units; these functions are not a ray tracer.</summary>
    public static class RadiativeTransferReference
    {
        public const double PlanckConstant = 6.62607015e-34;
        public const double BoltzmannConstant = 1.380649e-23;
        public const double SpeedOfLight = 299792458;
        public const double SolarMassGravitationalParameter = 1.3271244e20;
        public const double StefanBoltzmannConstant = 5.670374419e-8;

        // Newtonian, steady, zero-torque thin disk, one-face surface effective temperature.
        // sigma*T_eff^4 = 3 GM Mdot/(8 pi R^3) [1-sqrt(R_in/R)]. This does not determine
        // the midplane gas temperature and is not the relativistic Novikov-Thorne solution.
        public static double NewtonianZeroTorqueTemperature(double radiusRg, double massSolar = 35,
            double massSupplyKgPerSecond = 5e15, double innerRadiusRg = 6)
        {
            PositiveFinite(radiusRg, nameof(radiusRg)); PositiveFinite(massSolar, nameof(massSolar));
            NonnegativeFinite(massSupplyKgPerSecond, nameof(massSupplyKgPerSecond));
            PositiveFinite(innerRadiusRg, nameof(innerRadiusRg));
            if (innerRadiusRg < 6) throw new ArgumentOutOfRangeException(nameof(innerRadiusRg), "This reference retains a Schwarzschild stable inner edge.");
            if (radiusRg <= innerRadiusRg || massSupplyKgPerSecond == 0) return 0;
            double gm = SolarMassGravitationalParameter * massSolar;
            double physicalRadius = radiusRg * gm / (SpeedOfLight * SpeedOfLight);
            double flux = 3 * gm * massSupplyKgPerSecond / (8 * Math.PI * physicalRadius * physicalRadius * physicalRadius)
                * (1 - Math.Sqrt(innerRadiusRg / radiusRg));
            return Math.Pow(flux / StefanBoltzmannConstant, .25);
        }

        // B_nu [W m^-2 sr^-1 Hz^-1], frequency [Hz], temperature [K].
        public static double PlanckRadiance(double frequencyHz, double temperatureKelvin)
        {
            PositiveFinite(frequencyHz, nameof(frequencyHz)); NonnegativeFinite(temperatureKelvin, nameof(temperatureKelvin));
            if (temperatureKelvin == 0) return 0;
            double exponent = PlanckConstant * frequencyHz / (BoltzmannConstant * temperatureKelvin);
            if (exponent > 700) return 0;
            double denominator = exponent < 1e-5
                ? exponent * (1 + exponent * (.5 + exponent / 6)) : Math.Exp(exponent) - 1;
            return 2 * PlanckConstant * frequencyHz * frequencyHz * frequencyHz / (SpeedOfLight * SpeedOfLight * denominator);
        }

        // Exact dI/ds = alpha(S-I), with alpha >= 0, constant S and alpha along this segment.
        // Incoming and source function share radiance units; extinction and length use reciprocal units.
        public static double ConstantSlab(double incoming, double sourceFunction, double extinctionPerLength, double pathLength)
        {
            NonnegativeFinite(incoming, nameof(incoming)); NonnegativeFinite(sourceFunction, nameof(sourceFunction));
            NonnegativeFinite(extinctionPerLength, nameof(extinctionPerLength)); NonnegativeFinite(pathLength, nameof(pathLength));
            double fraction = EmissionFraction(extinctionPerLength * pathLength);
            return incoming * (1 - fraction) + sourceFunction * fraction;
        }

        public static double EmissionFraction(double opticalDepth)
        {
            if (double.IsNaN(opticalDepth) || opticalDepth < 0) throw new ArgumentOutOfRangeException(nameof(opticalDepth));
            return opticalDepth < 1e-5
                ? opticalDepth * (1 - opticalDepth * (.5 - opticalDepth / 6)) : 1 - Math.Exp(-opticalDepth);
        }

        // Static emitters/observers outside a Schwarzschild horizon; infinity is supported.
        public static double SchwarzschildStaticFrequencyRatio(double emitterRadiusRg, double observerRadiusRg = double.PositiveInfinity)
        {
            ExteriorRadius(emitterRadiusRg, nameof(emitterRadiusRg)); ExteriorRadius(observerRadiusRg, nameof(observerRadiusRg));
            return Math.Sqrt((1 - 2 / emitterRadiusRg) / (1 - 2 / observerRadiusRg));
        }

        // Direction is the photon propagation direction from emitter toward observer in the local
        // static frame; betaDotDirection is v.n/c. This is a flat/local SR control, not global optics.
        public static double SpecialRelativisticDoppler(double betaSquared, double betaDotDirection)
        {
            NonnegativeFinite(betaSquared, nameof(betaSquared)); ReferenceFlowModel.RequireFinite(betaDotDirection, nameof(betaDotDirection));
            if (betaSquared >= 1 || betaDotDirection * betaDotDirection > betaSquared + 1e-14)
                throw new ArgumentOutOfRangeException(nameof(betaSquared));
            return Math.Sqrt(1 - betaSquared) / (1 - betaDotDirection);
        }

        // I_nu/nu^3 invariant: g^3 B_(nu/g)(T) = B_nu(g*T). Apply the shift exactly once.
        // No extra g^3 multiplier belongs after evaluating the shifted blackbody temperature.
        public static double ShiftedPlanckRadiance(double observedFrequencyHz, double emitterTemperatureKelvin, double frequencyRatio)
        {
            PositiveFinite(frequencyRatio, nameof(frequencyRatio));
            NonnegativeFinite(emitterTemperatureKelvin, nameof(emitterTemperatureKelvin));
            return PlanckRadiance(observedFrequencyHz, frequencyRatio * emitterTemperatureKelvin);
        }

        private static void ExteriorRadius(double value, string name)
        { if (double.IsNaN(value) || value <= 2) throw new ArgumentOutOfRangeException(name); }
        private static void NonnegativeFinite(double value, string name)
        { ReferenceFlowModel.RequireFinite(value, name); if (value < 0) throw new ArgumentOutOfRangeException(name); }
        private static void PositiveFinite(double value, string name)
        { ReferenceFlowModel.RequireFinite(value, name); if (value <= 0) throw new ArgumentOutOfRangeException(name); }
    }
}
