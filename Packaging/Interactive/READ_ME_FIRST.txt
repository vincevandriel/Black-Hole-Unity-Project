DT-ET Interactive Observatory - Windows x64 verification/development candidate

Extract the complete folder to a writable location, then run START_OBSERVATORY.cmd.
Keep the executable, data and engine folders together. Unity Editor is not required.
This is the exact development-player runtime supplied to the packaging script. The
validation harness is retained for optional device evidence; debug symbols, editor
caches and build backups are excluded. It is not a production/XR certification.

Choose Auto, Laptop, Balanced or High before entering. The scene starts paused.
Space plays/pauses; comma reverses; R resets; M selects the merger event. V opens or
lowers the wrist interface. WASD/QE move, right-drag looks, wheel approaches, Shift
moves faster, and 1-5 select viewpoints. Esc pauses. Use the wrist controls to seek,
adjust time, pin/enlarge the interface, or configure an educational binary. Consult
Docs/INTERACTIVE_OBSERVATORY.md for the current controls, settings and science labels.

Representative replay retains SXS:BBH:0305 numerical positions and a normalized
radiation mode. Adjustable masses use a separate bounded quadrupole inspiral with
an explicitly conceptual merger and a physical-waveform cutoff. Bright gas is a
prescribed educational field by default. SOLVE GAS generates an optional 2D
isothermal fluid history with pressure and softened Newtonian gravity. No private
gas files are needed. Wait for generation, then use the existing time/view controls.
The covered final interval is shown in the gas panel; earlier gas is unavailable.
The gas environment, thickness and brightness are assumed, not measured GW150914
gas or a GRMHD calculation. See Docs/GENERATED_FLUID_MODEL.md.
Optical warping and magnified wave distortion are illustrative. Density samples are
weighted spatial samples, not fluid particles. The star catalogue is synthetic.
The observations of GW150914 came from LIGO; no Einstein Telescope event readings
are claimed. Read Docs/FINAL_DESKTOP_SCOPE.md for remaining scientific/data gates.

Other entry points:
  START_EVENT_LESSONS.cmd       Retained lessons; N then Space starts numerical replay.
  START_M3_REFERENCE_LAB.cmd    Separate single-hole field/light reference control.
  START_M4_REDUCED_EXHIBIT.cmd   Earlier reduced binary exhibit.

Optional device evidence:
  RUN_DEVICE_CHECK.cmd          Full interactive integration/capture suite.
  RUN_PERFORMANCE_CHECK.cmd     Separate High-requested performance/memory process.
These write unique DeviceCheck_* or PerformanceCheck_* folders alongside the player.
Allow several minutes. Close other demanding applications first and preserve all
output when reporting results. The test player is launched hidden and exits itself.
High can be downgraded by capability admission. Timings include completed offscreen
rendering and a GPU completion/readback fence; they do not certify native-window
presentation FPS, laptop suitability, human usability or physical XR performance.
Laptop is a rendering budget, not a promise for every laptop. Compatibility admission
turns off volume gas and optical warping while retaining the binary and interface.

Caller-declared tested runtime source revision: @@TESTED_REVISION@@
Source: @@SOURCE_URL@@/tree/@@TESTED_REVISION@@
Supplied full integration receipt: @@PASS_COUNT@@ passing checks.
Evidence/interactive-exhibit-integration.json records that run's hardware and scope.
runtime-manifest.json identifies the exact runtime files by SHA-256. The release
operator's build/revision record establishes binary-to-source provenance; a passing
receipt and build marker alone cannot prove that association. The package summary
and complete package-file manifest are distributed next to the ZIP.

Third-party/data attribution: NOTICES.txt and the referenced license notices.
