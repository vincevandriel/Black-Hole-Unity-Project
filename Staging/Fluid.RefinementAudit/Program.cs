using BlackHole.Staging.Simulation;
using System.Diagnostics;
using var data = File.OpenRead("BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes");
var scenario = new InteractiveBinaryScenario(NumericalEventHistory.Load(data));
GeneratedFluidHistory? coarse = null;
foreach (int n in new[] { 96, 192 }) {
 var watch = Stopwatch.StartNew();
 var h=GeneratedFluidHistory.Generate(scenario,new GeneratedFluidSettings(n,129,4000),CancellationToken.None);
 Console.WriteLine($"GRID={n} seconds={watch.Elapsed.TotalSeconds:G8} steps={h.Diagnostics.StepCount} extent={h.HalfExtentRg:G9} durationTg={(h.EndSeconds-h.StartSeconds)/h.GravitationalTimeSeconds:G9}");
 var s=new float[n*n];var vx=new float[n*n];var vz=new float[n*n];
 foreach(int f in new[]{0,64,128}) {
 h.CopyFrame(f,s,vx,vz);var snap=scenario.Evaluate(h.FrameTimes[f]);
 double mass=0,farMass=0,rMoment=0,angMom=0,max=0,vFar=0,maxR=0;
 double nearBodies(double x,double z) { double d=1e9; foreach(var b in new[]{snap.BodyA,snap.BodyB,snap.Remnant}) if(b.HasValue) d=Math.Min(d,Math.Sqrt(Math.Pow(x-b.Value.PositionRg.X,2)+Math.Pow(z-b.Value.PositionRg.Z,2))); return d; }
 for(int z=0;z<n;z++)for(int x=0;x<n;x++){int i=z*n+x;double px=-h.HalfExtentRg+(x+.5)*h.CellWidthRg,pz=-h.HalfExtentRg+(z+.5)*h.CellWidthRg,r=Math.Sqrt(px*px+pz*pz),m=s[i]*h.CellWidthRg*h.CellWidthRg;mass+=m;rMoment+=m*r;angMom+=m*(px*vz[i]-pz*vx[i]);if(s[i]>max){max=s[i];maxR=r;}if(nearBodies(px,pz)>6){farMass+=m;if(s[i]>1e-5)vFar=Math.Max(vFar,Math.Sqrt(vx[i]*vx[i]+vz[i]*vz[i]));}}
 var d=h.FrameDiagnostics[f];Console.WriteLine($"frame={f} M={mass:G12} retained={mass/h.Diagnostics.InitialMass:G8} sigmaMax={max:G9} peakR={maxR:G7} meanR={rMoment/mass:G7} Lz={angMom:G10} Moutside6={farMass:G10} outsideFraction={farMass/mass:G8} maxVoutside6={vFar:G7} sink={d.RemovedSinkMass:G10} outflow={d.NetOutflowMass:G10} floor={d.AddedFloorMass:G8} ledger={d.MassBudgetResidual:G5}");
 }
 if(coarse!=null){ foreach(int f in new[]{64,128}) {double error=0,norm=0;for(int z=0;z<96;z++)for(int x=0;x<96;x++){double px=-coarse.HalfExtentRg+(x+.5)*coarse.CellWidthRg,pz=-coarse.HalfExtentRg+(z+.5)*coarse.CellWidthRg;coarse.TrySample(coarse.FrameTimes[f],px,pz,out var a);h.TrySample(h.FrameTimes[f],px,pz,out var b);error+=Math.Abs(a.SurfaceDensity-b.SurfaceDensity);norm+=b.SurfaceDensity;}Console.WriteLine($"frame={f} coarseVsDoubleRelativeSampledL1={error/norm:G9}");}}else coarse=h;
}
