using System;
using System.Collections.Generic;

namespace BlackHole.Staging.Simulation
{
    public sealed class FrequencyGraphPoint
    {
        public double PhysicalTimeSeconds { get; }
        public double SeparationMeters { get; }
        public double OrbitalFrequencyHertz { get; }
        public double GravitationalWaveFrequencyHertz { get; }

        internal FrequencyGraphPoint(InspiralState state)
        {
            PhysicalTimeSeconds = state.PhysicalTimeSeconds;
            SeparationMeters = state.SeparationMeters;
            OrbitalFrequencyHertz = state.OrbitalFrequencyHertz!.Value;
            GravitationalWaveFrequencyHertz = state.GravitationalWaveFrequencyHertz!.Value;
        }
    }

    public static class FrequencyGraph
    {
        // Deterministic redraw. A seek or scenario switch simply resamples the same model.
        // The graph stops before cutoff because the approximation ends there.
        public static IReadOnlyList<FrequencyGraphPoint> Sample(CircularInspiral model,
            double playbackTimeSeconds, int sampleCount)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (sampleCount < 2 || sampleCount > 4096)
                throw new ArgumentOutOfRangeException(nameof(sampleCount), "Choose 2 to 4096 graph samples.");
            if (playbackTimeSeconds < 0 || double.IsNaN(playbackTimeSeconds)
                || double.IsInfinity(playbackTimeSeconds))
                throw new ArgumentOutOfRangeException(nameof(playbackTimeSeconds));

            double lastPhysicalTime = Math.Min(playbackTimeSeconds, model.CutoffTimeSeconds);
            // Keep the plotted endpoint within the inspiral regime.
            if (lastPhysicalTime >= model.CutoffTimeSeconds)
                lastPhysicalTime = model.CutoffTimeSeconds * (1.0 - 1e-12);
            var result = new List<FrequencyGraphPoint>(sampleCount);
            for (int i = 0; i < sampleCount; i++)
            {
                double time = lastPhysicalTime * i / (sampleCount - 1);
                result.Add(new FrequencyGraphPoint(model.Evaluate(time)));
            }
            return result.AsReadOnly();
        }
    }
}
