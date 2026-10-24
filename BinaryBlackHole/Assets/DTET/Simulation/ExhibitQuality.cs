using System;

namespace BlackHole.Staging.Simulation
{
    public enum ExhibitQualityProfile { Auto, Laptop, Balanced, High }

    // Hardware hints for quality selection, not measured performance.
    public readonly struct ExhibitCapabilities
    {
        public int ShaderLevel { get; }
        public int GraphicsMemoryMb { get; }
        public int SystemMemoryMb { get; }
        public bool Supports3DTextures { get; }
        public bool SupportsComputeShaders { get; }
        public ExhibitCapabilities(int shaderLevel, int graphicsMemoryMb, int systemMemoryMb,
            bool supports3DTextures, bool supportsComputeShaders)
        {
            ShaderLevel = Math.Max(0, shaderLevel);
            GraphicsMemoryMb = Math.Max(0, graphicsMemoryMb);
            SystemMemoryMb = Math.Max(0, systemMemoryMb);
            Supports3DTextures = supports3DTextures;
            SupportsComputeShaders = supportsComputeShaders;
        }
    }

    public sealed class ExhibitQualitySettings
    {
        public ExhibitQualityProfile RequestedProfile { get; }
        public ExhibitQualityProfile EffectiveProfile { get; }
        public bool GasEnabled { get; }
        public int RaySteps { get; }
        public double RenderScale { get; }
        public int MaxRenderWidth { get; }
        public int MaxRenderHeight { get; }
        public int StarCount { get; }
        public int TracerCount { get; }
        public string Reason { get; }
        public const string SharedPhysicsContract = "same-reduced-field-and-event-epoch-v1";
        // Estimate owned render targets, meshes and source arrays; excludes other Unity/VRAM usage.
        public long EstimatedManagedAndGpuBytes => 32L * MaxRenderWidth * MaxRenderHeight
            + 512L * StarCount + 512L * TracerCount;

        internal ExhibitQualitySettings(ExhibitQualityProfile requested, ExhibitQualityProfile effective,
            bool gasEnabled, int steps, double scale, int width, int height, int stars, int tracers, string reason)
        {
            RequestedProfile = requested; EffectiveProfile = effective; GasEnabled = gasEnabled;
            RaySteps = gasEnabled ? steps : 0; RenderScale = scale; MaxRenderWidth = width;
            MaxRenderHeight = height; StarCount = stars; TracerCount = tracers; Reason = reason;
        }

        // Even dimensions, aspect preserved to rounding; never exceeds display or cap.
        public void RenderDimensions(int screenWidth, int screenHeight, out int width, out int height)
        {
            if (screenWidth < 2 || screenHeight < 2) throw new ArgumentOutOfRangeException(nameof(screenWidth));
            double scale = Math.Min(RenderScale, Math.Min((double)MaxRenderWidth / screenWidth,
                (double)MaxRenderHeight / screenHeight));
            width = Math.Max(2, 2 * (int)(screenWidth * scale / 2));
            height = Math.Max(2, 2 * (int)(screenHeight * scale / 2));
        }
    }

    public static class ExhibitQuality
    {
        public static ExhibitQualitySettings Resolve(ExhibitQualityProfile requested, ExhibitCapabilities capabilities)
        {
            if (!Enum.IsDefined(typeof(ExhibitQualityProfile), requested))
                throw new ArgumentOutOfRangeException(nameof(requested));
            bool adequate = capabilities.GraphicsMemoryMb >= 2048 && capabilities.SystemMemoryMb >= 8192;
            bool constrained = (capabilities.GraphicsMemoryMb > 0 && capabilities.GraphicsMemoryMb < 1024)
                || (capabilities.SystemMemoryMb > 0 && capabilities.SystemMemoryMb < 4096);
            ExhibitQualityProfile effective = requested == ExhibitQualityProfile.Auto
                ? (adequate ? ExhibitQualityProfile.Balanced : ExhibitQualityProfile.Laptop) : requested;
            string reason = requested == ExhibitQualityProfile.Auto
                ? (adequate ? "Auto selected Balanced from reported memory; benchmark still required."
                    : "Auto selected Laptop because memory is low or unknown; benchmark still required.")
                : "Selected rendering budget; model and event time are unchanged.";
            if (constrained && effective != ExhibitQualityProfile.Laptop)
            {
                effective = ExhibitQualityProfile.Laptop;
                reason = "Reported memory is below the minimum budget; reduced to Laptop.";
            }
            else if (effective == ExhibitQualityProfile.High && !adequate)
            {
                effective = ExhibitQualityProfile.Balanced;
                reason = "High requires at least 2 GiB reported graphics and 8 GiB system memory; using Balanced.";
            }
            bool gas = capabilities.ShaderLevel >= 30;
            if (!gas)
            {
                effective = ExhibitQualityProfile.Laptop;
                reason = "Diagnostic fallback: shader model 3 is unavailable; gas is disabled. Bodies, waveform and camera remain available.";
            }
            switch (effective)
            {
                case ExhibitQualityProfile.High:
                    return new ExhibitQualitySettings(requested, effective, gas, 144, 1, 1120, 630, 4096, 2048, reason);
                case ExhibitQualityProfile.Balanced:
                    return new ExhibitQualitySettings(requested, effective, gas, 96, .75, 800, 450, 2048, 768, reason);
                default:
                    return new ExhibitQualitySettings(requested, effective, gas, 64, .5, 640, 360, 1024, 256, reason);
            }
        }
    }
}
