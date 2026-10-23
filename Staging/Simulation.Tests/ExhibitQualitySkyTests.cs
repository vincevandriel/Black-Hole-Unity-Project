using BlackHole.Staging.Simulation;

internal static class ExhibitQualitySkyTests
{
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); }
    private static void Near(double value, double expected, double tolerance, string label)
    { Check(double.IsFinite(value) && Math.Abs(value - expected) <= tolerance, $"{label}: {value:R} / {expected:R}"); }
    private static void Reject(Action action, string label)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Expected rejected input: " + label);
    }

    public static void Run()
    {
        var modern = new ExhibitCapabilities(50, 8192, 32768, true, true);
        var legacy = new ExhibitCapabilities(30, 2048, 8192, false, false);
        var unknown = new ExhibitCapabilities(30, 0, 0, false, false);
        var small = new ExhibitCapabilities(30, 512, 2048, false, false);
        var unsupported = new ExhibitCapabilities(25, 8192, 32768, true, true);
        Check(ExhibitQuality.Resolve(ExhibitQualityProfile.Auto, modern).EffectiveProfile == ExhibitQualityProfile.Balanced,
            "Auto does not silently allocate High");
        Check(ExhibitQuality.Resolve(ExhibitQualityProfile.Auto, unknown).EffectiveProfile == ExhibitQualityProfile.Laptop,
            "Unknown capability selects conservative Laptop");
        Check(ExhibitQuality.Resolve(ExhibitQualityProfile.High, small).EffectiveProfile == ExhibitQualityProfile.Laptop,
            "Known low memory reduces High");
        Check(ExhibitQuality.Resolve(ExhibitQualityProfile.High, unknown).EffectiveProfile == ExhibitQualityProfile.Balanced,
            "Unknown memory cannot select High");
        var fallback = ExhibitQuality.Resolve(ExhibitQualityProfile.High, unsupported);
        Check(!fallback.GasEnabled && fallback.RaySteps == 0 && fallback.Reason.Contains("gas is disabled"),
            "Minimum shader fallback declares missing gas and zero ray work");
        Check(ExhibitQuality.Resolve(ExhibitQualityProfile.Balanced, legacy).GasEnabled,
            "Analytic gas requires neither compute nor 3D textures");

        int lastSteps = 0; long lastBytes = 0;
        foreach (var profile in new[] { ExhibitQualityProfile.Laptop, ExhibitQualityProfile.Balanced, ExhibitQualityProfile.High })
        {
            var settings = ExhibitQuality.Resolve(profile, modern);
            Check(settings.RequestedProfile == profile && settings.EffectiveProfile == profile && settings.GasEnabled,
                "Supported explicit profile retained");
            Check(settings.RaySteps > lastSteps && settings.EstimatedManagedAndGpuBytes > lastBytes,
                "Rendering budgets increase explicitly");
            Check(settings.EstimatedManagedAndGpuBytes <= 26L * 1024 * 1024, "Bounded render/mesh allowance below 26 MiB");
            Check(settings.StarCount <= 4096 && settings.TracerCount <= 2048, "Bounded marker and source counts");
            foreach (var size in new[] { (3840, 2160), (1920, 1080), (1024, 768), (320, 200), (900, 1600) })
            {
                settings.RenderDimensions(size.Item1, size.Item2, out int w, out int h);
                Check(w >= 2 && h >= 2 && w % 2 == 0 && h % 2 == 0, "Valid even render dimensions");
                Check(w <= settings.MaxRenderWidth && h <= settings.MaxRenderHeight
                    && w <= size.Item1 && h <= size.Item2, "Dimensions obey both display and profile cap");
                Near((double)w / h, (double)size.Item1 / size.Item2, .03, "Aspect preserved within pixel rounding");
            }
            lastSteps = settings.RaySteps; lastBytes = settings.EstimatedManagedAndGpuBytes;
        }
        Reject(() => ExhibitQuality.Resolve((ExhibitQualityProfile)99, modern), "Invalid quality enum");
        Reject(() => fallback.RenderDimensions(0, 1080, out _, out _), "Invalid screen size");

        var low = SyntheticSkyCatalog.Create(1024);
        var high = SyntheticSkyCatalog.Create(4096);
        var replay = SyntheticSkyCatalog.Create(1024);
        Check(SyntheticSkyCatalog.Fingerprint(low) == SyntheticSkyCatalog.Fingerprint(replay), "Seeded sky replay is deterministic");
        Check(SyntheticSkyCatalog.Fingerprint(low) != SyntheticSkyCatalog.Fingerprint(SyntheticSkyCatalog.Create(1024, 2)),
            "Different seed changes directions");
        double meanX = 0, meanY = 0, meanZ = 0, meanY2 = 0, brightFlux = 0;
        for (int i = 0; i < high.Length; i++)
        {
            var star = high[i];
            if (i < low.Length) Check(star.Equals(low[i]), "Quality preserves every retained source value");
            Near(star.X * star.X + star.Y * star.Y + star.Z * star.Z, 1, 1e-14, "Unit celestial direction");
            Near(.2126 * star.Red + .7152 * star.Green + .0722 * star.Blue, 1, 1e-14, "Colour does not change luminance flux");
            Near(star.RelativeFlux, Math.Pow(10, -.4 * star.ApparentMagnitude), 1e-15, "Magnitude maps logarithmically to flux");
            if (i > 0) Check(star.RelativeFlux < high[i - 1].RelativeFlux, "Only successively fainter sources are added");
            Check(star.TemperatureKelvin >= 2800 && star.TemperatureKelvin <= 12000, "Declared synthetic temperature range");
            SyntheticSkyCatalog.DirectionAtObserver(star, 1e9, -83, 44, out double dx, out double dy, out double dz);
            Near(dx, star.X, 0, "Translation does not move fixed sky x");
            Near(dy, star.Y, 0, "Translation does not move fixed sky y");
            Near(dz, star.Z, 0, "Translation does not move fixed sky z");
            meanX += star.X; meanY += star.Y; meanZ += star.Z; meanY2 += star.Y * star.Y;
            if (i < 32) brightFlux += star.RelativeFlux;
        }
        Near(meanX / high.Length, 0, .035, "Direction population has no large x bias");
        Near(meanY / high.Length, 0, .035, "Direction population has no large y bias");
        Near(meanZ / high.Length, 0, .035, "Direction population has no large z bias");
        Near(meanY2 / high.Length, 1.0 / 3, .025, "Uniform solid-angle sphere second moment");
        Near(low.Take(32).Sum(s => s.RelativeFlux), brightFlux, 0, "Bright prefix integrated flux stable");

        // Independent discrete pixel sums exercise subpixel phase and resolution, not just a
        // mirrored one-sample formula. Each full PSF must collect the source's prescribed flux.
        foreach (int height in new[] { 270, 360, 450, 630, 900, 1800 })
        foreach (double flux in new[] { .0001, .01, 1.0, 4.0 })
        foreach (double phase in new[] { 0.0, .19, .49 })
        {
            double sigma = SyntheticSkyCatalog.PsfSigmaPixels(flux, height);
            int extent = (int)Math.Ceiling(3 * sigma + 2);
            double sum = 0;
            for (int y = -extent; y <= extent; y++)
            for (int x = -extent; x <= extent; x++)
                sum += SyntheticSkyCatalog.PsfPixelMass(x - phase, y + .37, sigma);
            Near(sum, 1, 5e-7, "Discrete PSF pixel area integrates to unit flux");
            double pixelSum = sum * SyntheticSkyCatalog.IntegratedDisplayFlux(flux, height);
            Near(pixelSum * Math.Pow(900.0 / height, 2) / SyntheticSkyCatalog.DefaultExposure,
                flux, flux * 5e-7, "Angular integrated flux survives render resolution changes");
        }
        Check(SyntheticSkyCatalog.PsfPixelMass(100, 100, 1) == 0, "PSF is bounded and has no infinite glow tail");
        Reject(() => SyntheticSkyCatalog.Create(0), "Empty sky");
        Reject(() => SyntheticSkyCatalog.Create(8193), "Unbounded sky");
        Reject(() => SyntheticSkyCatalog.PsfSigmaPixels(double.NaN, 900), "Nonfinite flux");
        Reject(() => SyntheticSkyCatalog.PsfPixelMass(0, 0, 0), "Zero sigma");
    }
}
