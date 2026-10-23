using System;
using System.IO;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    public sealed partial class VisualPrototypeBootstrap
    {
        private bool numericalMode;
        private NumericalEventSession numericalSession;
        private Text numericalNotes;
        private bool numericalMarkersCreated;
        public bool NumericalMode => numericalMode;
        public NumericalEventSession NumericalSession => numericalSession;
        public const double NumericalMetersPerDisplayUnit = 250000;

        private void BuildNumericalNotes(Transform parent)
        {
            numericalNotes = TextAt(parent, "", 14, Muted, new Vector2(42, -470), new Vector2(800, 155), TextAnchor.UpperLeft, Anchor.TopLeft);
            numericalNotes.gameObject.SetActive(false);
        }

        public void SetNumericalMode()
        {
            if (numericalSession == null)
            {
                var asset = Resources.Load<TextAsset>("Physics/SXS0305");
                if (asset == null) throw new InvalidOperationException("The SXS0305 numerical replay asset is missing.");
                try
                {
                    using (var stream = new MemoryStream(asset.bytes, false))
                        numericalSession = new NumericalEventSession(NumericalEventHistory.Load(stream));
                }
                finally { Resources.UnloadAsset(asset); }
            }
            session.Pause();
            physicalMode = false;
            numericalMode = true;
            playing = false;
            numericalSession.Reset();
            numericalSession.SetPlaybackRate(.05);
            observerPhysics.InvalidateEpoch();
            reverseButton.interactable = true;
            separationSlider.interactable = false;
            applyStartButton.GetComponentInChildren<Text>(true).text = "TO MERGER";
            separationSlider.gameObject.SetActive(false);
            separationCaption.text = "FIXED NUMERICAL HISTORY";
            playbackSlider.minValue = .01f;
            playbackSlider.SetValueWithoutNotify(.05f);
            stellarField.SetApproximationAvailable(false);
            if (!numericalMarkersCreated)
            {
                MakeNumericalMarker(bodyA, Cyan);
                MakeNumericalMarker(bodyB, Violet);
                MakeNumericalMarker(remnantMarker.transform, Gold);
                numericalMarkersCreated = true;
            }
            foreach (var body in new[] { bodyA, bodyB, remnantMarker.transform })
            {
                SetArtisticGas(body, false);
                foreach (Transform child in body)
                    if (child.name.StartsWith("Numerical", StringComparison.Ordinal)) child.gameObject.SetActive(true);
            }
            modeBanner.text = "SXS:BBH:0305 / NUMERICAL REPLAY";
            motionNote.text = "Coordinate inspection / 1 scene unit = 250 km / free exterior views.";
            gasNote.text = "Area-based horizon proxies; spheres are not measured horizon shapes.";
            storyExplanation.text = "Numerical relativity supplies the inspiral, common apparent horizon and remnant. Pause, reverse or seek to inspect the recorded coordinate history.";
            graphTitle.text = "Re[H22] / PEAK |H22|";
            graphAxis.text = "Scaled H22";
            eventContextNote.text = "GW150914-inspired replay / fixed SXS configuration; measured parameters differ.";
            graphLegend.text = "Source seconds relative to the comparison epochs";
            playbackCaption.text = "PLAYBACK RATE / SOURCE SECONDS";
            layerBody.text = "Vacuum SXS0305, Lev6. Horizon coordinates and distant radiation have different times and frames. AH and wave-peak epochs are aligned for comparison, not a measured delay.";
            scenarioText.text = "Fixed SXS0305 / A, B, C return to SI lessons";
            separationText.text = $"Native t=540 / start {numericalSession.State.SeparationMeters.Value / 1000:0.0} km";
            numericalNotes.gameObject.SetActive(true);
            numericalNotes.text = "GW150914-inspired SXS configuration: 35.46 + 29.04 solar masses.\n"
                + "Aligned spins: +0.330 and -0.440. Total mass scale: 64.5 solar masses.\n"
                + "Comparison: first common apparent horizon ↔ peak |h22|.\n"
                + "This is a display alignment, not light travel time or a common gauge.\n"
                + "H22 = r h22/M is a normalized radiation mode, not detector strain.\n"
                + "GW150914 was measured by LIGO; Einstein Telescope has no readings for it.";
            partnerHud.InvalidateNarration();
            RefreshNumericalState();
        }

        private void LeaveNumericalMode()
        {
            numericalSession?.Pause();
            numericalMode = false;
            stellarField?.SetApproximationAvailable(true);
            if (numericalNotes != null) numericalNotes.gameObject.SetActive(false);
            if (separationSlider != null) { separationSlider.interactable = true; separationSlider.gameObject.SetActive(true); }
            if (separationCaption != null) separationCaption.text = "INITIAL SEPARATION / GM/c²";
            if (applyStartButton != null) applyStartButton.GetComponentInChildren<Text>(true).text = "APPLY START";
            if (playbackSlider != null) playbackSlider.minValue = .1f;
            learningSignal?.SetEvent(null);
            if (bodyA != null) bodyA.localScale = bodyB.localScale = Vector3.one * 1.08f;
            if (remnantMarker != null)
            {
                remnantMarker.transform.localScale = Vector3.one * 1.25f;
                remnantMarker.transform.position = ExhibitCenter;
            }
        }

        // SXS right-handed (x,y,z) maps to Unity (x,z,y), a declared reflection into y-up display coordinates.
        public static Vector3 NumericalDisplayPosition(NumericalVector3 metres) => ExhibitCenter +
            new Vector3((float)(metres.X / NumericalMetersPerDisplayUnit),
                (float)(metres.Z / NumericalMetersPerDisplayUnit), (float)(metres.Y / NumericalMetersPerDisplayUnit));

        public void SeekNumericalMerger()
        {
            if (!numericalMode) return;
            numericalSession.Seek(numericalSession.Model.CommonHorizonElapsedSeconds);
            observerPhysics.InvalidateEpoch();
            RefreshNumericalState();
        }

        private void RefreshNumericalState()
        {
            if (!numericalMode) return;
            var state = numericalSession.State;
            visualProgress = (float)(state.ElapsedSeconds / numericalSession.Model.DurationSeconds);
            visualSpeed = (float)numericalSession.PlaybackRate;
            playing = numericalSession.IsPlaying;
            ApplyNumericalBody(bodyA, state.BodyA);
            ApplyNumericalBody(bodyB, state.BodyB);
            ApplyNumericalBody(remnantMarker.transform, state.Remnant);
            orbit.enabled = false;
            trailA.enabled = trailB.enabled = !state.HasCommonHorizon;
            if (!state.HasCommonHorizon)
            {
                DrawNumericalTrail(trailA, false);
                DrawNumericalTrail(trailB, true);
            }
            Shader.SetGlobalFloat("_StoryDisplayTime", (float)state.SourceCoordinateTimeSeconds);
            seekSlider.SetValueWithoutNotify(visualProgress);
            seekText.text = $"Coordinate t={state.SourceCoordinateTimeSeconds:0.0000} s / Δt={state.SourceReferenceAlignedSeconds:+0.0000;-0.0000;0} s";
            playbackText.text = $"{visualSpeed:0.00}x playback / fixed numerical history";
            endpointText.text = state.HasCommonHorizon
                ? "COMMON APPARENT HORIZON / area proxy from the numerical data; not an event-horizon surface"
                : "TWO APPARENT HORIZONS / coordinate tracks and area proxies; not an optical shadow";
            transportText.text = (playing ? "Playing" : "Paused") + (state.HasCommonHorizon ? " / common horizon and remnant" : " / numerical inspiral");
            learningSignal.SetEvent(numericalSession.Model);
            learningSignal.SetProgress(visualProgress);
            graphLegend.text = state.Radiation.HasValue
                ? $"u={state.Radiation.Value.SourceRetardedTimeSeconds:0.0000} s / peak-aligned comparison"
                : "Outside waveform coverage / horizon replay continues";
        }

        private static void ApplyNumericalBody(Transform body, NumericalHorizonState? state)
        {
            body.gameObject.SetActive(state.HasValue);
            if (!state.HasValue) return;
            body.position = NumericalDisplayPosition(state.Value.PositionMeters);
            body.localScale = Vector3.one * (float)(state.Value.ArealRadiusMeters / (NumericalMetersPerDisplayUnit * .39));
        }

        private void DrawNumericalTrail(LineRenderer line, bool second)
        {
            var end = numericalSession.PlaybackTimeSeconds;
            for (int i = 0; i < line.positionCount; i++)
            {
                var t = Math.Max(0, end - .015 * (1 - i / (double)(line.positionCount - 1)));
                var state = numericalSession.Model.Evaluate(t);
                var body = second ? state.BodyB : state.BodyA;
                line.SetPosition(i, NumericalDisplayPosition(body.Value.PositionMeters));
            }
        }

        private static void MakeNumericalMarker(Transform body, Color color)
        {
            for (int plane = 0; plane < 3; plane++)
            {
                var line = VisualAssetFactory.CreateGlowLine(body, "Numerical areal proxy wire " + plane, color, .008f, 96, true);
                for (int i = 0; i < line.positionCount; i++)
                {
                    var angle = 2 * Mathf.PI * i / line.positionCount;
                    var a = .39f * Mathf.Cos(angle);var b = .39f * Mathf.Sin(angle);
                    line.SetPosition(i, plane == 0 ? new Vector3(a, 0, b) : plane == 1 ? new Vector3(a, b, 0) : new Vector3(0, a, b));
                }
            }
        }
    }
}
