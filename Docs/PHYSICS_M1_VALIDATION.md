# M1 validation receipt — 23 October 2026

Tested implementation commit: `6269b349e9e13754701780bcd44b16b1db5f6279`. Follow-up receipt changes are documentation only. Unity **6000.3.25f1**, Windows x64 development build, NVIDIA GeForce RTX 3060 Ti. Original project/package versions were preserved.

| Check | Result |
|---|---|
| Original portable baseline | Six existing groups passed before implementation. |
| Final shared-source portable checks | Seven groups passed, including the new physical-binding/reverse/unit/frame-rate group. |
| Fresh source clone | 166 tracked files; no generated cache/build input required in Git. |
| Fresh-clone Unity import and Windows build | Succeeded; build method validated all five custom shaders and logged `DTET_QA_BUILD_SUCCEEDED`. |
| Fresh-clone built-player physical integration | **43/43 checks passed**, with machine-readable receipt and four actual camera-render captures. |
| Fresh-clone retained artistic UI/input regression | **Nine groups passed**, including simulated pointer/keyboard, wrist/world interaction, navigation, clock/lens boundaries and deterministic storyboard replay. |
| New asset GUIDs | No duplicates found. |
| Source whitespace/integrity | Staged diff check passed; generated settings changes excluded from publication. |

The physical integration covers initial/seek/play/reverse body transforms against the shared SI snapshot, render separation within one metre, unequal-mass barycentre and radius ratio, custom starting separation preserving masses, source-time advance, pause with camera movement, deterministic seeking, graph model identity, displayed Hz, hidden artistic gas, null frequencies at cutoff, reverse from the final endpoint, reset and return between modes. Captures show the frequency board, source-time/Hz/km readouts, above view and conceptual cutoff.

The camera capture utility renders the real URP camera to a texture so the automated test can run in a hidden window. It does not fabricate readouts or draw an editorial replacement scene. It is compiled only for Editor/development builds. [Unity render-request API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rendering.RenderPipeline.SubmitRenderRequest.html).

Cached working-copy batch builds emitted a URP `TraceRenderingLayerMask.urtshader` resource-host message. The fresh-clone build did **not** reproduce that message and completed successfully. This receipt does not certify a clean interactive Editor Console. Raw logs/captures remain outside Unity Assets and source publication.

The automated regression's frame timings came from a hidden test window and are not a display-framerate benchmark, laptop performance result or XR certification. No human study, expert physics approval, tracked headset test, numerical merger, gas simulation, detector-strain calibration or full binary optical validation is claimed. M1 uses the original bounded circular model and enlarged markers; merger/remnant presentation remains conceptual.

Reproduce with the commands in [PHYSICS_M1.md](PHYSICS_M1.md). The requested scientific gas/merger experience requires the later dataset and observer-rendering milestones.
