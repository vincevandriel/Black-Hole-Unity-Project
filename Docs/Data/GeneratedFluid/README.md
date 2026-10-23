# Generated-fluid refinement evidence

These are original project calculations, not downloaded gas data. The [standalone audit program](../../../Staging/Fluid.RefinementAudit/README.md) generates both complete histories, with 96 and 192 cells per direction, 129 stored frames, a maximum duration of 4000 tg, and the same assumed physical parameters and SXS input. Its exact code defines the sampled density norm, angular momentum, radius moment and exclusion of planar circles within 6 rg of the active sources.

The audited simulation runtime commit is **`6cc36ad86036fbc0e65b3dd8e22b61f8c68fcb70`**. The source was frozen during the audit. Later shader-only changes do not alter this numerical calculation. The numerical model identifier is `generated-isothermal-newtonian-muscl-ssprk2-v1`.

| File/input | SHA-256 |
| --- | --- |
| `Staging/Fluid.RefinementAudit/Program.cs` | `2cd4b2374e3570104f1bc27f31b6d19e25a354e597e455ea33bc0f0d0bc2a5e3` |
| `BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes` | `67345f1a8ecb2301890e67f3856dbb78170e0740ee2838021e3acdbe8b2657a4` |
| [audit-6cc36ad.stdout.txt](audit-6cc36ad.stdout.txt) | `1bb2f797a35e74a74fc4e2f19bfc4630e1133f342e1106f3478d9e7dd0639e41` |
| [audit-6cc36ad.metrics.json](audit-6cc36ad.metrics.json) | `14ea91da5bd058d6a53b44e8b27ff5fee36a1555edb91890b31bba21a1987792` |

`Program.cs` was copied unchanged from the actually executed temporary runner. The project reference now uses a relative path; no audit formula changed. Small `.gitattributes` files disable newline conversion for the retained program and evidence so the recorded byte hashes survive a fresh checkout. The standalone project was subsequently built without rerunning the expensive calculation.

The stdout file contains all process-output chunks concatenated in order. It was captured through the execution tool, rather than independently redirected to a disk log during the original run. The JSON is parsed from those printed values and retains their printed precision. The external research library preserves the original project record, full manifest and local execution provenance; personal absolute paths are omitted here.

Both runs used the original C# solver under .NET 9 in Release mode. Timing is local generation time, not Unity rendering speed, a laptop guarantee or an XR performance measurement. Floating-point/compiler differences may affect the least significant digits of reruns. The SXS numerical asset and its attribution are documented in [the SXS data contract](../SXS0305/README.md).

The final sampled-density disagreement is approximately **63.14%**, and final integrated mass differs by approximately **20.4%** relative to the doubled grid. Those measurements establish unresolved numerical sensitivity. Read [the complete model and limits](../../GENERATED_FLUID_MODEL.md) before interpreting this coarse Newtonian isothermal field as physical accretion or emission.
