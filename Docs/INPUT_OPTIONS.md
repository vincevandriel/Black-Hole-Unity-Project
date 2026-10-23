# Visitor input options

**M1 update:** [the shared SI lesson](PHYSICS_M1.md) is now the default mode. Historical storyboard-only descriptions below apply to the separately retained artistic mode. Current physical readouts, graph and playback use one session; merger and clocks remain outside this milestone.

Open `Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity` in Unity 6000.3.25f1 and press Play, or run `Builds/VisualQA/BinaryBlackHole_VisualQA.exe`.

Press **O** to raise the wrist and open **Input options**. Alternatively press V and click **OPTIONS / O** on the wrist. Two independent sliders adjust:

- **Mouse look:** 0.25–4.00× the original sensitivity; default 1.00× (0.18 degrees per pixel on right-drag). Applies to emulated first-person head look and optional orbit look, not middle-drag panning.
- **Scroll / closer–farther:** 0.25–10.00× the original speed; default **3.00×**. One 120-unit wheel notch moves the visitor **2.4 exhibit units**, up from 0.8. Optional orbit distance uses the same multiplier in its existing exponential distance control; its exponent is tripled, not its bounded distance change at every distance.

**BACK** or O returns to the normal wrist controls. **RESET DEFAULTS** restores look 1.00× and scroll 3.00×. The sliders take effect immediately. Settings use the application's local Unity PlayerPrefs keys `DTET.Observer.LookSensitivity.v1` and `DTET.Observer.ScrollSensitivity.v1`. Changes are flushed on closing options, losing application focus or normal application exit, and restored on the next startup. No account, cloud or network service is used. A forced process kill while dragging may lose the most recent changes. Resetting or seeking the storyboard does not reset these preferences.

The wheel still does **not** change the lens strengths, body separation, storyboard time or camera field of view. Scrolling over either wrist page or another raycastable world-space panel does not move the camera. Existing orbit/visitor distance limits remain enforced. Nonfinite sensitivity values are ignored and finite out-of-range values are clamped. Extreme wheel-event deltas are capped at 20 notches per update.

These controls adjust keyboard/mouse pose emulation only. They must not scale a tracked headset's physical head rotation or alter tracked hand poses. Headset comfort and hardware integration remain unverified.

## Verification

`ObserverInputSettings.cs` is included in Unity and linked into the existing portable source tests. Tests cover the tripled default, independent values, lower/upper bounds, nonfinite inputs and reset.

The development player adds `Options` as a screenshot variant and `DTET_INPUT_OPTIONS_PASS`: actual pointer opening, pointer acquisition of the look slider, independent scroll callback, menu wheel suppression, reset/slider synchronization, O navigation, exact 3× free-flight movement, optional orbit scaling and look scaling. Capture runs disable preference persistence and reset only in-memory values so QA cannot overwrite a visitor's saved preferences. See [QA receipts](DESKTOP_VISUAL_QA.md) for executed checks and limitations.

Verified 2026-10-05: source tests and Unity build passed. Options and Wrist players returned 0 with all eight checks; both fresh 1600×900 screenshots were inspected. A real saved-preferences restart, other resolutions, human usability and headset input remain unverified.
