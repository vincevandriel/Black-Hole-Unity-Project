using DTET.VisualPrototype;

internal static class AdmObserverTests
{
    public static void Run()
    {
        var rest = new MetricVector3(0, 0, 0);
        var flat = new AdmMetricSample(1, rest, 1, 0, 0, 1, 0, 1);
        Near(flat.ProperTimeRate(rest), 1, "Minkowski resting clock");
        var v = new MetricVector3(0.6, 0, 0);
        Near(flat.ProperTimeRate(v), 0.8, "Special-relativistic clock dilation");
        var incoming = new PhotonFourMomentum(1, new MetricVector3(-1, 0, 0));
        var outgoing = new PhotonFourMomentum(1, new MetricVector3(1, 0, 0));
        Near(flat.MeasurePhotonEnergy(incoming, v), 2, "Relativistic approaching Doppler blueshift");
        Near(flat.MeasurePhotonEnergy(outgoing, v), 0.5, "Relativistic receding Doppler redshift");
        var lapseSample = new AdmMetricSample(0.5, rest, 1, 0, 0, 1, 0, 1);
        Near(lapseSample.ProperTimeRate(rest), 0.5, "Non-unit lapse resting clock");
        // Unit conserved energy in this static sample: k^t=1/alpha², |k^x|=1/alpha.
        var sameConservedEnergy = new PhotonFourMomentum(4, new MetricVector3(-2, 0, 0));
        Near(lapseSample.MeasurePhotonEnergy(sameConservedEnergy, rest), 2, "Static gravitational blueshift measurement");
        Near(AdmMetricSample.FrequencyRatio(1, 2), 2, "Endpoint energy ratio convention");
        // Schwarzschild fixture at r=10 M, theta=pi/2; separate from the exhibit metric.
        var schwarzschild = new AdmMetricSample(Math.Sqrt(0.8), rest, 1 / 0.8, 0, 0, 100, 0, 100);
        Near(schwarzschild.ProperTimeRate(rest), Math.Sqrt(0.8), "Static Schwarzschild clock rate at r=10 M");
        Near(schwarzschild.MeasurePhotonEnergy(new PhotonFourMomentum(1 / 0.8, new MetricVector3(-1, 0, 0)), rest),
            1 / Math.Sqrt(0.8), "Incoming distant photon blueshift in Schwarzschild fixture");
        var shifted = new AdmMetricSample(1, new MetricVector3(0.2, 0, 0), 1, 0, 0, 1, 0, 1);
        Near(shifted.ProperTimeRate(new MetricVector3(-0.2, 0, 0)), 1, "Eulerian observer includes shift");
        Near(shifted.MeasurePhotonEnergy(new PhotonFourMomentum(1, new MetricVector3(0.8, 0, 0)),
            new MetricVector3(-0.2, 0, 0)), 1, "Shifted null photon measurement");
        var nonDiagonal = new AdmMetricSample(1, rest, 2, 0.3, 0.2, 1.5, 0.1, 1.2);
        Near(nonDiagonal.ProperTimeRate(new MetricVector3(0.1, 0.2, 0.3)), Math.Sqrt(1 - 0.224), "Full spatial-metric contraction");
        foreach (Action invalid in new Action[] {
            () => new AdmMetricSample(0, rest, 1, 0, 0, 1, 0, 1),
            () => new AdmMetricSample(1, rest, 1, 2, 0, 1, 0, 1),
            () => new AdmMetricSample(1, rest, 1, 0, 0, 1, 0, -1),
            () => new AdmMetricSample(1, rest, double.NaN, 0, 0, 1, 0, 1),
            () => flat.ProperTimeRate(new MetricVector3(1, 0, 0)),
            () => flat.ProperTimeRate(new MetricVector3(1.01, 0, 0)),
            () => flat.MeasurePhotonEnergy(new PhotonFourMomentum(1, rest), rest),
            () => flat.MeasurePhotonEnergy(outgoing, rest, 0.1),
            () => new PhotonFourMomentum(-1, rest),
            () => AdmMetricSample.FrequencyRatio(0, 1)
        })
        {
            bool rejected = false;
            try { invalid(); } catch (ArgumentException) { rejected = true; }
            if (!rejected) throw new Exception("Invalid ADM observer input accepted");
        }
        Console.WriteLine("PASS: ADM proper-time and photon-energy invariants, Doppler signs, lapse/shift and validation (analytic fixtures only)");
    }

    private static void Near(double actual, double expected, string description)
    { if (!double.IsFinite(actual) || Math.Abs(actual - expected) > 1e-12) throw new Exception(description + $": {actual:R} != {expected:R}"); }
}
