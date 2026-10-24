using System;

namespace DTET.VisualPrototype
{
    // Dimensionless input multipliers; never change simulation time or tracked XR poses.
    public sealed class ObserverInputSettings
    {
        public const float DefaultLook = 1f;
        public const float DefaultScroll = 3f;
        public const float MinimumLook = 0.25f, MaximumLook = 4f;
        public const float MinimumScroll = 0.25f, MaximumScroll = 10f;
        public float Look { get; private set; } = DefaultLook;
        public float Scroll { get; private set; } = DefaultScroll;

        public bool SetLook(float value)
        {
            if (!Finite(value)) return false;
            Look = Math.Max(MinimumLook, Math.Min(MaximumLook, value));
            return true;
        }

        public bool SetScroll(float value)
        {
            if (!Finite(value)) return false;
            Scroll = Math.Max(MinimumScroll, Math.Min(MaximumScroll, value));
            return true;
        }

        public void Reset() { Look = DefaultLook; Scroll = DefaultScroll; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
