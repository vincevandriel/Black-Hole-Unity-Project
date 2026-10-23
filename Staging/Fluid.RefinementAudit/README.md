# Generated-fluid refinement audit

This standalone .NET 9 program reproduces the full 96/192-grid comparison in [the fluid model documentation](../../Docs/GENERATED_FLUID_MODEL.md). `Program.cs` is byte-for-byte the already-executed audit code. Its input, source commit, hashes and retained results are recorded in [the audit provenance](../../Docs/Data/GeneratedFluid/README.md).

Run from the **repository root**, because the program loads the retained SXS asset by its repository-relative path:

```powershell
dotnet build Staging/Fluid.RefinementAudit/Fluid.RefinementAudit.csproj -c Release
dotnet run --project Staging/Fluid.RefinementAudit/Fluid.RefinementAudit.csproj -c Release --no-build
```

The second command runs both full histories sequentially. They took approximately 33 and 257 seconds on the audited development desktop; other devices may take longer. Unity and a GPU are not needed. Building the project alone does not execute the audit.

The recorded comparison used runtime commit `6cc36ad86036fbc0e65b3dd8e22b61f8c68fcb70`. For an exact source comparison, use that solver revision; later numerical changes should produce a new labeled audit. The 96/192 field disagreement remains substantial, so this is evidence of resolution sensitivity, not a converged physical gas reconstruction.
