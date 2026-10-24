using System;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    // Fixed-axis plot of supplied samples; missing coverage remains a gap.
    public sealed class InteractiveWristPlotGraphic : MaskableGraphic
    {
        private const int Count = 768;
        private readonly float[] samples = new float[Count];
        private readonly float[] envelopes = new float[Count];
        private readonly bool[] dense = new bool[Count - 1];
        private bool frequency;
        private float progress;
        private float cursorValue = float.NaN;
        public double AxisMaximum { get; private set; } = 1;
        public int AvailableSamples { get; private set; }
        public int DenseCycleBins { get; private set; }
        public int DenseBinCount => DenseCycleBins;
        public bool HasCoverage => AvailableSamples > 0;

        public void Rebuild(IInteractiveExhibitPresenter source, bool frequencySeries, double axisMaximum, double durationSeconds)
        {
            frequency = frequencySeries;
            AxisMaximum = axisMaximum > 0 && !double.IsInfinity(axisMaximum) && !double.IsNaN(axisMaximum) ? axisMaximum : 1;
            AvailableSamples = 0;
            DenseCycleBins = 0;
            for (int i = 0; i < Count; i++)
            {
                double? value = source.SampleWristGraph(i / (double)(Count - 1), frequency);
                if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value)) samples[i] = float.NaN;
                else { samples[i] = (float)(value.Value / AxisMaximum); AvailableSamples++; }
                double? envelope = frequency ? null : source.SampleWristEnvelope(i / (double)(Count - 1));
                envelopes[i] = Finite(envelope) ? (float)(Math.Abs(envelope.Value) / AxisMaximum) : float.NaN;
            }
            // Use the largest endpoint/midpoint frequency plus 50% margin to detect undersampling.
            // Plot an envelope when phase or frequency is unresolved.
            double binSeconds = Math.Max(0, durationSeconds) / (Count - 1);
            for (int i = 0; i < Count - 1; i++)
            {
                dense[i] = false;
                if (frequency || float.IsNaN(samples[i]) || float.IsNaN(samples[i + 1])) continue;
                double? f0 = source.SampleWristGraph(i / (double)(Count - 1), true);
                double? fm = source.SampleWristGraph((i + .5) / (Count - 1), true);
                double? f1 = source.SampleWristGraph((i + 1.0) / (Count - 1), true);
                bool ambiguous = !Finite(f0) || !Finite(fm) || !Finite(f1);
                double maximum = Math.Max(Math.Abs(f0.GetValueOrDefault()), Math.Max(Math.Abs(fm.GetValueOrDefault()), Math.Abs(f1.GetValueOrDefault())));
                dense[i] = ambiguous || maximum * 1.5 * binSeconds >= .30;
                if (dense[i]) DenseCycleBins++;
            }
            SetVerticesDirty();
        }
        private static bool Finite(double? value) => value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
        public void SetProgress(float value, double? currentValue)
        {
            value = Mathf.Clamp01(value);
            float normalized = Finite(currentValue) ? (float)(currentValue.Value / AxisMaximum) : float.NaN;
            if (Mathf.Abs(value - progress) < .00001f && normalized.Equals(cursorValue)) return;
            progress = value;
            cursorValue = normalized;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            var grid = new Color(.21f, .41f, .49f, .28f);
            var signal = new Color(.35f, .94f, 1f, .95f);
            var future = new Color(.29f, .62f, .72f, .42f);
            for (int i = 0; i < 5; i++)
            {
                float y = rect.yMin + rect.height * i / 4;
                Stroke(mesh, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), .65f, grid);
            }
            for (int i = 0; i <= 8; i++)
            {
                float x = rect.xMin + rect.width * i / 8;
                Stroke(mesh, new Vector2(x, rect.yMin), new Vector2(x, rect.yMax), .65f, grid);
            }
            for (int i = 0; i < Count - 1; i++)
            {
                float t = i / (float)(Count - 1), next = (i + 1) / (float)(Count - 1);
                float x = rect.xMin + rect.width * t, nx = rect.xMin + rect.width * next;
                if (float.IsNaN(samples[i]) || float.IsNaN(samples[i + 1]))
                {
                    Quad(mesh, new Vector2(x, rect.yMin), new Vector2(nx + .4f, rect.yMax), new Color(.39f, .44f, .49f, .12f));
                    continue;
                }
                if (dense[i])
                {
                    if (float.IsNaN(envelopes[i]) || float.IsNaN(envelopes[i + 1])) continue;
                    // Use the model's amplitude envelope, not extrema of undersampled oscillations.
                    float e0 = Mathf.Clamp01(envelopes[i]), e1 = Mathf.Clamp01(envelopes[i + 1]);
                    float y0 = rect.height * .48f * e0, y1 = rect.height * .48f * e1;
                    var bandColor = next <= progress ? new Color(signal.r, signal.g, signal.b, .28f) : new Color(future.r, future.g, future.b, .14f);
                    Band(mesh, x, nx, rect.center.y, y0, y1, bandColor);
                    Stroke(mesh, new Vector2(x, rect.center.y + y0), new Vector2(nx, rect.center.y + y1), 1, next <= progress ? signal : future);
                    Stroke(mesh, new Vector2(x, rect.center.y - y0), new Vector2(nx, rect.center.y - y1), 1, next <= progress ? signal : future);
                    continue;
                }
                Stroke(mesh, new Vector2(x, Y(rect, samples[i])), new Vector2(nx, Y(rect, samples[i + 1])), 1.55f, next <= progress ? signal : future);
            }
            float cursor = rect.xMin + rect.width * progress;
            Stroke(mesh, new Vector2(cursor, rect.yMin), new Vector2(cursor, rect.yMax), 1.2f, new Color(1, .77f, .42f, .95f));
            if (!float.IsNaN(cursorValue))
            {
                float y = Y(rect, cursorValue);
                Quad(mesh, new Vector2(cursor - 2.5f, y - 2.5f), new Vector2(cursor + 2.5f, y + 2.5f), new Color(1, .88f, .60f));
            }
        }
        private float Y(Rect rect, float normalized)
            => frequency ? Mathf.Lerp(rect.yMin, rect.yMax, Mathf.Clamp01(normalized))
                : rect.center.y + Mathf.Clamp(normalized, -1, 1) * rect.height * .48f;
        private static void Band(VertexHelper mesh, float x, float nx, float center, float left, float right, Color color)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(new Vector2(x, center - left), color, Vector2.zero);
            mesh.AddVert(new Vector2(x, center + left), color, Vector2.zero);
            mesh.AddVert(new Vector2(nx, center + right), color, Vector2.zero);
            mesh.AddVert(new Vector2(nx, center - right), color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
        private static void Quad(VertexHelper mesh, Vector2 min, Vector2 max, Color color)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(new Vector2(min.x, min.y), color, Vector2.zero);
            mesh.AddVert(new Vector2(min.x, max.y), color, Vector2.zero);
            mesh.AddVert(new Vector2(max.x, max.y), color, Vector2.zero);
            mesh.AddVert(new Vector2(max.x, min.y), color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
        private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var direction = (b - a).normalized;
            var normal = new Vector2(-direction.y, direction.x) * thickness * .5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero); mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero); mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
