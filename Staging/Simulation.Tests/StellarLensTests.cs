using BlackHole.Staging.Simulation;

internal static class StellarLensTests
{
    private static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
    private static void Near(double actual, double expected, double tolerance, string message)
    { Check(double.IsFinite(actual) && Math.Abs(actual-expected)<=tolerance, $"{message}: {actual:R} / {expected:R}"); }

    public static void Run()
    {
        StellarLensReference.SourceDimensions(100000, out double distance, out double radius);
        Near(distance*100000, 149597870700, .0001, "Illustrative star remains at one physical AU");
        Near(radius*100000, 695700000, .0001, "Illustrative star has one nominal solar radius");
        StellarLensReference.SourceDimensions(200000, out double distance2, out double radius2);
        Near(distance2, distance/2, 0, "Changing scenario mass rescales source coordinates, not its physical distance");
        Near(radius2/distance2, radius/distance, 1e-16, "Physical source angular size survives scene-unit rescaling");
        double diameterDegrees = 2*Math.Atan(radius/distance)*180/Math.PI;
        Check(diameterDegrees>.5 && diameterDegrees<.6, "Unbent source is a modest roughly half-degree disk");

        double einstein2 = StellarLensReference.EinsteinRadiusSquared(100, .5, double.PositiveInfinity);
        Near(einstein2, .02, 0, "Independent 4GM Dls/(c squared Dl Ds) Einstein-radius coefficient");
        Near(StellarLensReference.EinsteinRadiusSquared(100, 1, double.PositiveInfinity),2*einstein2,0,"Einstein radius squared scales linearly with lens mass");
        Near(StellarLensReference.EinsteinRadiusSquared(100,.5,200),einstein2/2,0,"Finite source geometry changes bending");
        foreach (double sourceDepth in new[] { -1.0, 0, 99, 100 })
            Near(StellarLensReference.EinsteinRadiusSquared(100,.5,sourceDepth),0,0,"Foreground source has no background lensing");

        // Compare the shader-reference map against independently solved analytic image locations.
        foreach (double sourceAngle in new[] { .005, .03, .1, .4 })
        {
            double root = Math.Sqrt(sourceAngle*sourceAngle+4*einstein2);
            foreach (double image in new[] { .5*(sourceAngle+root), .5*(sourceAngle-root) })
            {
                StellarLensReference.Deflection(image,0,100,.5,1,double.PositiveInfinity,1,out double alpha,out double y);
                Near(image-alpha,sourceAngle,1e-14,"Both analytic point-mass images map back to the same source");
                Near(y,0,0,"Axis-aligned source retains reflection symmetry");
            }
        }
        double ring = Math.Sqrt(einstein2);
        StellarLensReference.Deflection(0,ring,100,.5,1,double.PositiveInfinity,1,out double dx,out double dy);
        Near(ring-dy,0,1e-14,"Aligned source maps onto its Einstein ring");
        Near(dx,0,0,"Einstein-ring transverse coordinate");
        StellarLensReference.Deflection(.2,.1,100,.5,1,1e8,0,out dx,out dy);
        Near(dx,0,0,"Effects-off horizontal identity"); Near(dy,0,0,"Effects-off vertical identity");
        for(int i=-100;i<=100;i++)
        {
            StellarLensReference.Deflection(i*1e-5,1e-8,1,.8,1.6,1e6,2,out dx,out dy);
            Check(double.IsFinite(dx)&&double.IsFinite(dy)&&Math.Sqrt(dx*dx+dy*dy)<=1.00000000001,"Near-core extension remains finite and bounded");
        }
        Near(StellarLensReference.SurfaceBrightness(0),1,0,"Finite source central surface brightness");
        Near(StellarLensReference.SurfaceBrightness(1),.45,0,"Assumed limb darkening remains finite");
        Near(StellarLensReference.SurfaceBrightness(1.01),0,0,"No stellar emission away from the source");
        Console.WriteLine("PASS: hypothetical stellar geometry, two images and Einstein ring, finite-source depth, mass scaling and bounded core");
    }
}
