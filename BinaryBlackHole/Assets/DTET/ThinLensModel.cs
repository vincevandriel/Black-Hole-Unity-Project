using System;

namespace DTET.VisualPrototype
{
    public readonly struct PointLens
    {
        public readonly double X, Y, EinsteinAngleSquared;
        public PointLens(double x, double y, double einsteinAngleSquared)
        {
            if (!Finite(x) || !Finite(y) || !Finite(einsteinAngleSquared) || einsteinAngleSquared <= 0)
                throw new ArgumentException("Point lens parameters must be finite, with positive strength.");
            X = x; Y = y; EinsteinAngleSquared = einsteinAngleSquared;
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public readonly struct ThinLensImage
    {
        public readonly double X, Y, Jxx, Jxy, Jyy;
        public double Determinant => Jxx * Jyy - Jxy * Jxy;
        public ThinLensImage(double x, double y, double jxx, double jxy, double jyy)
        { X = x; Y = y; Jxx = jxx; Jxy = jxy; Jyy = jyy; }
    }

    // Common-plane point-mass lens in paraxial coordinates, not a spacetime ray tracer.
    // The bounded Newton search may miss caustic images.
    public static class ThinLensModel
    {
        public const int MaximumImages = 5;
        private const double Tolerance = 1e-8;
        private static readonly double[] DirectionsX = { 1, 0.7071067811865475, 0, -0.7071067811865475, -1, -0.7071067811865475, 0, 0.7071067811865475 };
        private static readonly double[] DirectionsY = { 0, 0.7071067811865475, 1, 0.7071067811865475, 0, -0.7071067811865475, -1, -0.7071067811865475 };

        // r_g=GM/c² in the same distance units as D_l and D_s. Foreground sources are not lensed.
        public static double EinsteinAngleSquared(double gravitationalRadius, double lensDistance, double sourceDistance)
        {
            if (!PointLens.Finite(gravitationalRadius) || !PointLens.Finite(lensDistance) || !PointLens.Finite(sourceDistance)
                || gravitationalRadius < 0 || lensDistance <= 0 || sourceDistance <= 0) throw new ArgumentException("Invalid lens geometry.");
            return sourceDistance <= lensDistance ? 0 : 4 * gravitationalRadius * (1 - lensDistance / sourceDistance) / lensDistance;
        }

        public static bool Map(double x, double y, PointLens[] lenses, int count, out double sourceX,
            out double sourceY, out double jxx, out double jxy, out double jyy)
        {
            sourceX = x; sourceY = y; jxx = jyy = 1; jxy = 0;
            for (var i = 0; i < count; i++)
            {
                var dx = x - lenses[i].X; var dy = y - lenses[i].Y;
                var radiusSquared = dx * dx + dy * dy;
                if (radiusSquared < 1e-20) return false;
                var strength = lenses[i].EinsteinAngleSquared / radiusSquared;
                sourceX -= strength * dx; sourceY -= strength * dy;
                jxx -= strength * (1 - 2 * dx * dx / radiusSquared);
                jyy -= strength * (1 - 2 * dy * dy / radiusSquared);
                jxy += strength * 2 * dx * dy / radiusSquared;
            }
            return PointLens.Finite(sourceX) && PointLens.Finite(sourceY) && PointLens.Finite(jxx)
                && PointLens.Finite(jxy) && PointLens.Finite(jyy);
        }

        public static int FindImages(double sourceX, double sourceY, PointLens[] lenses, int count, ThinLensImage[] output)
        {
            if (!PointLens.Finite(sourceX) || !PointLens.Finite(sourceY) || lenses == null || count < 0 || count > 2
                || count > lenses.Length || output == null || output.Length < MaximumImages) throw new ArgumentException("Invalid lens query.");
            if (count == 0) { output[0] = new ThinLensImage(sourceX, sourceY, 1, 0, 1); return 1; }
            if (count == 1)
            {
                var dx = sourceX - lenses[0].X; var dy = sourceY - lenses[0].Y;
                var offset = Math.Sqrt(dx * dx + dy * dy);
                // Exact point-source alignment is a ring, not two discrete roots. Renderer handles finite-source annulus.
                if (offset < 1e-7) return 0;
                var root = Math.Sqrt(offset * offset + 4 * lenses[0].EinsteinAngleSquared);
                var positive = (offset + root) * 0.5;
                var negative = -lenses[0].EinsteinAngleSquared / positive; // stable for distant sources
                Write(lenses[0].X + dx / offset * positive, lenses[0].Y + dy / offset * positive, lenses, count, output, 0);
                Write(lenses[0].X + dx / offset * negative, lenses[0].Y + dy / offset * negative, lenses, count, output, 1);
                return 2;
            }
            var found = 0;
            AddRoot(sourceX, sourceY, sourceX, sourceY, lenses, count, output, ref found);
            for (var lensIndex = 0; lensIndex < count; lensIndex++)
            {
                var lens = lenses[lensIndex];
                var dx = sourceX - lens.X; var dy = sourceY - lens.Y;
                var offset = Math.Sqrt(dx * dx + dy * dy);
                var scale = Math.Sqrt(lens.EinsteinAngleSquared);
                var nx = offset > 1e-12 ? dx / offset : 1; var ny = offset > 1e-12 ? dy / offset : 0;
                var positive = (offset + Math.Sqrt(offset * offset + 4 * lens.EinsteinAngleSquared)) * 0.5;
                var negative = -lens.EinsteinAngleSquared / positive;
                AddRoot(lens.X + nx * positive, lens.Y + ny * positive, sourceX, sourceY, lenses, count, output, ref found);
                AddRoot(lens.X + nx * negative, lens.Y + ny * negative, sourceX, sourceY, lenses, count, output, ref found);
                if (offset > 4 * scale) continue;
                for (var ring = 0; ring < 3; ring++)
                    for (var direction = 0; direction < DirectionsX.Length; direction++)
                    {
                        var radius = scale * (ring == 0 ? 0.35 : ring == 1 ? 1 : 2);
                        AddRoot(lens.X + DirectionsX[direction] * radius, lens.Y + DirectionsY[direction] * radius,
                            sourceX, sourceY, lenses, count, output, ref found);
                    }
            }
            return found;
        }

        private static void AddRoot(double x, double y, double sx, double sy, PointLens[] lenses, int count,
            ThinLensImage[] output, ref int found)
        {
            if (found == MaximumImages) return;
            for (var iteration = 0; iteration < 32; iteration++)
            {
                if (!Map(x, y, lenses, count, out var bx, out var by, out var jxx, out var jxy, out var jyy)) return;
                var ex = bx - sx; var ey = by - sy; var error = ex * ex + ey * ey;
                if (error < Tolerance * Tolerance)
                {
                    for (var i = 0; i < found; i++)
                        if ((x - output[i].X) * (x - output[i].X) + (y - output[i].Y) * (y - output[i].Y) < 1e-10) return;
                    output[found++] = new ThinLensImage(x, y, jxx, jxy, jyy);
                    return;
                }
                var determinant = jxx * jyy - jxy * jxy;
                if (Math.Abs(determinant) < 1e-14) return;
                var stepX = (jyy * ex - jxy * ey) / determinant;
                var stepY = (jxx * ey - jxy * ex) / determinant;
                var accepted = false;
                for (var damping = 0; damping < 10; damping++)
                {
                    var nextX = x - stepX; var nextY = y - stepY;
                    if (Map(nextX, nextY, lenses, count, out var nbx, out var nby, out _, out _, out _)
                        && (nbx - sx) * (nbx - sx) + (nby - sy) * (nby - sy) < error)
                    { x = nextX; y = nextY; accepted = true; break; }
                    stepX *= 0.5; stepY *= 0.5;
                }
                if (!accepted) return;
            }
        }

        private static void Write(double x, double y, PointLens[] lenses, int count, ThinLensImage[] output, int index)
        {
            Map(x, y, lenses, count, out _, out _, out var jxx, out var jxy, out var jyy);
            output[index] = new ThinLensImage(x, y, jxx, jxy, jyy);
        }
    }
}
