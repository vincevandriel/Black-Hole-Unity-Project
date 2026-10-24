# Guided tour content and replaceable narration

The tour text is `Assets/Resources/DTET/GuidedTour.json`. Its `entries` array contains objects with `id`, `title`, `body`, `narration` and `clip`. The five checkpoint IDs are `inspiral`, `gas_tides`, `final_approach`, `coalescence` and `remnant`. The initial scene's target topics are `system`, `black_hole_a`, `black_hole_b`, `accretion` and `waves`. Keep these IDs stable: the runtime associates them with checkpoints and selectable objects.

`body` is the readable card; `narration` is its independently editable spoken script. All information needed for the explanation is available in text. The voice is optional. Short card labels identify numerical replay, assumed gas, the illustrative capture surface and exaggerated wave display without interrupting each explanation with a long warning.

Five topics also provide `titleExperiment`, `bodyExperiment`, `narrationExperiment` and `clipExperiment`. These replace the replay explanation when adjustable masses are selected. Their files end in `_experiment.wav`. Replace both recordings when revising such a topic; do not describe the adjustable merger as a numerical relativity result. The component A/B text is independent of which mass is larger.

## Replace text or a clip

1. Edit the relevant entry in `GuidedTour.json`. Preserve valid JSON and the entry's ID. `title` names the card; `body` supports `\n` line breaks. Keep checkpoint bodies around 40–70 words.
2. Replace `Assets/Resources/DTET/Narration/<id>.wav` with a recording of the revised `narration`. Retain its `.meta` file so Unity preserves the asset identity. Plain mono PCM WAV is sufficient; the reference clips use signed 16-bit samples at 22050 Hz, approximately 20–30 seconds per topic.
3. Preserve `clip: "DTET/Narration/<id>"`, or change it to the new **Resources-relative path without an extension**. Do not include `Assets/Resources/` in that value. A JSON script edit alone does not regenerate recorded audio.
4. Update `narrationMetadata` to identify the replacement voice/recording method and date. Import/rebuild the desktop player, then check its optional narration and readable card together. A voice provider or human recording can be substituted; the shipped player needs no speech service or installed Windows voice.

The supplied clips are generic **machine-generated reference narration**, synthesized offline with Windows `System.Speech`/SAPI, Microsoft David Desktop, English (United States), rate -1 on its -10 to 10 scale, volume 100. They imitate no named person. PCM audio is packaged with the project for Unity playback on supported desktop platforms. Production does not synthesize speech or send text to a server. The generator/verification helpers remain outside the release source tree; scripts are not required to replace a WAV.

## Scientific content

The cards describe the current [binary observatory](INTERACTIVE_OBSERVATORY.md), [finite gas calculation](MINIDISK_DYNAMICS.md) and [appearance assumptions](PLASMA_APPEARANCE.md). The binary is the representative SXS0305 replay inspired by GW150914; bright gas is an assumed environment. Passive labels change the displayed emission rather than solving extra particle forces. The joining capture boundary is illustrative, and the visible background ripples amplify an optical wave illustration. They must not be presented as a measured bright disk, literal visible gravitational waves or a validated binary spacetime image.

The existing research library preserves the underlying event/SXS and gravitational-wave sources. These short explanations summarize that established scope; they do not add a new physical model. Text/PCM format checks establish content availability, not intelligibility for every listener, human usability, audio-device support or headset comfort.
