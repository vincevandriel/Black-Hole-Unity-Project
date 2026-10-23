using BlackHole.Staging.Simulation;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    /// <summary>Shared model frequency curve in physical mode; qualitative artwork in storyboard mode. Neither is detector strain.</summary>
    public sealed class HudSignalGraphic : MaskableGraphic
    {
        private float progress;
        private CircularInspiral model;
        private IReadOnlyList<FrequencyGraphPoint> samples;
        private NumericalEventHistory eventModel;
        private float[] eventSamples;
        public NumericalEventHistory EventModel => eventModel;
        public double MaximumFrequencyHz { get; private set; }
        public CircularInspiral Model => model;

        public void SetModel(CircularInspiral value)
        {
            if (ReferenceEquals(model, value) && eventModel == null) return;
            eventModel = null;
            eventSamples = null;
            model = value;
            samples = value == null ? null : FrequencyGraph.Sample(value, value.CutoffTimeSeconds, 192);
            MaximumFrequencyHz = samples == null ? 0 : samples[samples.Count - 1].GravitationalWaveFrequencyHertz;
            SetVerticesDirty();
        }

        public void SetEvent(NumericalEventHistory value)
        {
            if (ReferenceEquals(eventModel, value)) return;
            eventModel = value;
            model = null;
            samples = null;
            eventSamples = null;
            MaximumFrequencyHz = 0;
            if (value != null)
            {
                // Graph presentation only; authoritative native samples remain full precision in the model.
                const int count = 1536;
                eventSamples = new float[count];
                for (int i = 0; i < count; i++)
                {
                    var radiation = value.Evaluate(value.DurationSeconds * i / (count - 1.0)).Radiation;
                    eventSamples[i] = radiation.HasValue ? (float)(radiation.Value.RealH22 / value.WaveformPeakAmplitude) : float.NaN;
                }
            }
            SetVerticesDirty();
        }
        public void SetProgress(float value)
        {
            if (Mathf.Approximately(progress, value)) return;
            progress = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            var muted = new Color(color.r, color.g, color.b, 0.19f);
            float zeroY = model == null ? rect.center.y : rect.yMin;
            Stroke(mesh, new Vector2(rect.xMin, zeroY), new Vector2(rect.xMax, zeroY), 1, muted);
            if (eventSamples != null)
            {
                for (int i = 0; i < eventSamples.Length - 1; i++)
                {
                    if (float.IsNaN(eventSamples[i]) || float.IsNaN(eventSamples[i + 1])) continue;
                    float x = i / (float)(eventSamples.Length - 1), nx = (i + 1) / (float)(eventSamples.Length - 1);
                    Stroke(mesh, new Vector2(rect.xMin + x * rect.width, rect.center.y + eventSamples[i] * rect.height * .44f),
                        new Vector2(rect.xMin + nx * rect.width, rect.center.y + eventSamples[i + 1] * rect.height * .44f),
                        1.5f, nx <= progress ? color : muted);
                }
                float anchor = rect.xMin + (float)(eventModel.CommonHorizonElapsedSeconds / eventModel.DurationSeconds) * rect.width;
                Stroke(mesh, new Vector2(anchor, rect.yMin), new Vector2(anchor, rect.yMax), 1, muted);
                float cursor = Mathf.Lerp(rect.xMin, rect.xMax, progress);
                Stroke(mesh, new Vector2(cursor, rect.yMin), new Vector2(cursor, rect.yMax), 1.5f, new Color(.95f, .70f, .36f));
                return;
            }
            if (this.samples != null)
            {
                for (int i = 0; i < this.samples.Count - 1; i++)
                {
                    float x = i / (float)(this.samples.Count - 1), nextX = (i + 1) / (float)(this.samples.Count - 1);
                    var a = new Vector2(rect.xMin + x * rect.width, rect.yMin + (float)(this.samples[i].GravitationalWaveFrequencyHertz / MaximumFrequencyHz) * rect.height * 0.94f);
                    var b = new Vector2(rect.xMin + nextX * rect.width, rect.yMin + (float)(this.samples[i + 1].GravitationalWaveFrequencyHertz / MaximumFrequencyHz) * rect.height * 0.94f);
                    Stroke(mesh, a, b, 1.8f, nextX <= progress ? color : muted);
                }
                var cursor = Mathf.Lerp(rect.xMin, rect.xMax, progress);
                Stroke(mesh, new Vector2(cursor, rect.yMin), new Vector2(cursor, rect.yMax), 1.5f, new Color(0.95f, 0.70f, 0.36f));
                return;
            }
            const int samples = 400;
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)samples;
                var next = (i + 1) / (float)samples;
                var a = new Vector2(rect.xMin + t * rect.width, rect.center.y + Wave(t) * rect.height * 0.44f);
                var b = new Vector2(rect.xMin + next * rect.width, rect.center.y + Wave(next) * rect.height * 0.44f);
                Stroke(mesh, a, b, 1.8f, next <= progress ? color : muted);
            }
            var cursorX = Mathf.Lerp(rect.xMin, rect.xMax, progress);
            Stroke(mesh, new Vector2(cursorX, rect.yMin), new Vector2(cursorX, rect.yMax), 1.5f, new Color(0.95f, 0.70f, 0.36f));
        }

        private static float Wave(float t) => Mathf.Sin(2f * Mathf.PI * (3f * t + 11f * t * t * t)) * Mathf.Lerp(0.18f, 1f, t * t);

        internal static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var direction = (b - a).normalized;
            var normal = new Vector2(-direction.y, direction.x) * thickness * 0.5f;
            var index = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
