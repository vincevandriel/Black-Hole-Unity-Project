using DTET.VisualPrototype;

internal static class ThinLensTests
{
    public static void Run()
    {
        var lenses = new PointLens[2];
        var images = new ThinLensImage[ThinLensModel.MaximumImages];
        Near(ThinLensModel.EinsteinAngleSquared(0.1, 10, 100), 0.036, "Lens distance geometry");
        Near(ThinLensModel.EinsteinAngleSquared(0.1, 10, 5), 0, "Foreground star is not lensed");
        Check(ThinLensModel.FindImages(0.3, 0.2, lenses, 0, images) == 1, "No lens gives one direct image");
        Near(images[0].X, 0.3, "Direct image coordinate");
        lenses[0] = new PointLens(0, 0, 1);
        Check(ThinLensModel.FindImages(0, 0, lenses, 1, images) == 0, "Alignment is a ring, not two point images");
        for (var i = 1; i <= 100; i++)
        {
            var beta = i * 0.03;
            Check(ThinLensModel.FindImages(beta, 0, lenses, 1, images) == 2, "Single lens splits a background point");
            var expectedPositive = (beta + Math.Sqrt(beta * beta + 4)) / 2;
            Near(images[0].X, expectedPositive, "Analytical primary image");
            Near(images[1].X, -1 / expectedPositive, "Analytical secondary image");
            Near(images[0].X * images[1].X, -1, "Image position product");
            Check(images[0].Determinant > 0 && images[1].Determinant < 0, "Image parity");
            CheckResiduals(beta, 0, lenses, 1, images, 2);
        }
        lenses[0] = new PointLens(-0.8, 0, 0.5); lenses[1] = new PointLens(0.8, 0, 0.5);
        var count = ThinLensModel.FindImages(0, 0, lenses, 2, images);
        Check(count == 5, "Symmetric binary fixture resolves five images");
        CheckResiduals(0, 0, lenses, 2, images, count);
        Check(Contains(images, count, 0, 0) && Contains(images, count, Math.Sqrt(1.64), 0)
            && Contains(images, count, -Math.Sqrt(1.64), 0) && Contains(images, count, 0, 0.6)
            && Contains(images, count, 0, -0.6), "Independent symmetric-binary roots");
        // Lens-plane translations must preserve deflection.
        lenses[0] = new PointLens(1.2, -3, 0.5); lenses[1] = new PointLens(2.8, -3, 0.5);
        count = ThinLensModel.FindImages(2, -3, lenses, 2, images);
        Check(count == 5, "Binary translation invariance");
        CheckResiduals(2, -3, lenses, 2, images, count);
        Check(Contains(images, count, 2, -3), "Translated central image");
        // Finite-difference the lens-map Jacobian.
        lenses[0] = new PointLens(0, 0, 1);
        ThinLensModel.Map(1.4, 0.2, lenses, 1, out _, out _, out var jxx, out var jxy, out var jyy);
        const double h = 1e-5;
        ThinLensModel.Map(1.4 + h, 0.2, lenses, 1, out var xp, out var yp, out _, out _, out _);
        ThinLensModel.Map(1.4 - h, 0.2, lenses, 1, out var xm, out var ym, out _, out _, out _);
        Near((xp - xm) / (2 * h), jxx, "Jacobian xx", 1e-8);
        Near((yp - ym) / (2 * h), jxy, "Jacobian xy", 1e-8);
        ThinLensModel.Map(1.4, 0.2 + h, lenses, 1, out _, out yp, out _, out _, out _);
        ThinLensModel.Map(1.4, 0.2 - h, lenses, 1, out _, out ym, out _, out _, out _);
        Near((yp - ym) / (2 * h), jyy, "Jacobian yy", 1e-8);
        foreach (Action invalid in new Action[] {
            () => new PointLens(0, 0, -1),
            () => new PointLens(double.NaN, 0, 1),
            () => ThinLensModel.EinsteinAngleSquared(1, 0, 10),
            () => ThinLensModel.FindImages(double.NaN, 0, lenses, 1, images),
            () => ThinLensModel.FindImages(0, 0, lenses, 3, images)
        })
        {
            bool rejected = false;
            try { invalid(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid lens input rejected");
        }
        Console.WriteLine("PASS: thin-lens geometry, analytical images/parity, five-image binary fixture, translation, Jacobian and validation");
    }

    private static bool Contains(ThinLensImage[] images, int count, double x, double y) => images.Take(count)
        .Any(image => Math.Abs(image.X - x) < 1e-6 && Math.Abs(image.Y - y) < 1e-6);
    private static void CheckResiduals(double x, double y, PointLens[] lenses, int lensCount, ThinLensImage[] images, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Check(ThinLensModel.Map(images[i].X, images[i].Y, lenses, lensCount, out var bx, out var by, out _, out _, out _), "Finite lens map");
            Near(bx, x, "Lens equation x residual", 1e-7); Near(by, y, "Lens equation y residual", 1e-7);
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Near(double actual, double expected, string message, double tolerance = 1e-10)
    { Check(double.IsFinite(actual) && Math.Abs(actual - expected) < tolerance, message + $": {actual:R} != {expected:R}"); }
}
