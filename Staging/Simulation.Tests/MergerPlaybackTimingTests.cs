using BlackHole.Staging.Simulation;

internal static class MergerPlaybackTimingTests
{
    public static void Run()
    {
        const double merger = 1, tg = .00031769416612286176, end = 1.25, start = .68;
        double Advance(double t, double wall, double rate) => MergerPlaybackTiming.Advance(t, wall, rate, merger, tg, end);
        void Near(double actual, double expected, string label)
        { if (Math.Abs(actual - expected) > 2e-11) throw new Exception(label + ": " + actual + " != " + expected); }
        Near(MergerPlaybackTiming.Factor(merger, merger, tg), .12, "Minimum speed at merger");
        foreach (int fps in new[] { 30, 60, 144 })
        {
            double stepped = start;
            for (int i = 0; i < 30 * fps; i++) stepped = Advance(stepped, 1.0 / fps, .02);
            Near(stepped, Advance(start, 30, .02), "Frame-independent crossing at " + fps + " fps");
            Near(Advance(stepped, 30, -.02), start, "Reversible presentation map at " + fps + " fps");
        }
        Near(Advance(start, 0, .02), start, "Zero display time");
        Near(Advance(start, 30, 0), start, "Zero playback rate");
        Near(Advance(start, 1000, .02), end, "Forward endpoint");
        Near(Advance(start, 1000, -.02), 0, "Reverse endpoint");
        double wallDuration = MergerPlaybackTiming.DisplayDuration(start, end, .02, merger, tg);
        Near(Advance(start, wallDuration, .02), end, "Display interval reaches source endpoint");
        Near(Advance(end, wallDuration, -.02), start, "Display interval reverses to source start");
        double previous = 0;
        for (int i = 0; i < 1000; i++)
        {
            double next = Advance(previous, 1.0 / 30, .02);
            if (next <= previous || next - previous > .02 / 30 + 1e-12) throw new Exception("Monotone bounded source rate");
            previous = next;
        }
        Console.WriteLine("PASS: merger presentation timing, rate bounds, endpoints, reverse and frame independence.");
    }
}
