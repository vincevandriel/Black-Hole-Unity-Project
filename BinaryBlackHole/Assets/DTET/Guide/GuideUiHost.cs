using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DTET.Guide
{
    /// <summary>
    /// Builds every guide pop-up at start and offers one simple, display-only API:
    ///
    ///   host.Show(GuideMessageId.LookAround);   host.Hide(GuideMessageId.LookAround);
    ///   host.ShowCongratulation();              host.ShowOutOfRange("2", "10");
    ///   host.Voice.SetSpeaking(true);           host.Timer.Begin(30f);
    ///   host.Transcript.AddLine("Guide", "..."); host.Transcript.Toggle();
    ///   host.AddElementLabel(blackHoleTransform, "Black hole", new Vector3(0, 0.6f, 0));
    ///
    /// It never decides WHEN something appears - that logic lives elsewhere.
    /// Put this component on an empty GameObject. Untick 'Preview On Play' once the real logic is connected.
    /// </summary>
    public sealed class GuideUiHost : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private float fadeSeconds = 0.25f;

        [Header("Preview (to check the look without any logic)")]
        [Tooltip("On Play, shows every pop-up one after another so you can see how each one looks. Untick when wiring the real logic.")]
        [SerializeField] private bool previewOnPlay = true;
        [SerializeField] private float previewSecondsPerMessage = 2.5f;
        [SerializeField] private bool previewLoop = false;

        private readonly Dictionary<GuideMessageId, GuidePopup> popups = new Dictionary<GuideMessageId, GuidePopup>();
        private readonly List<GuideElementLabel> labels = new List<GuideElementLabel>();
        private Transform root;
        private int congratulationIndex;

        public VoiceIndicatorView Voice { get; private set; }
        public QuestionTimerView Timer { get; private set; }
        public TranscriptView Transcript { get; private set; }

        private void Awake()
        {
            root = new GameObject("Guide UI").transform;
            root.SetParent(transform, false);

            foreach (GuideMessageSpec spec in GuideUiSpecs.All())
            {
                popups[spec.Id] = GuidePopup.Create(spec, root, fadeSeconds);
            }

            Voice = VoiceIndicatorView.Create(root, fadeSeconds);
            Timer = QuestionTimerView.Create(root, fadeSeconds);
            Transcript = TranscriptView.Create(root, fadeSeconds);
        }

        private void Start()
        {
            if (previewOnPlay)
            {
                StartCoroutine(Preview());
            }
        }

        // ---------- pop-ups ----------

        public GuidePopup Popup(GuideMessageId id)
        {
            return popups[id];
        }

        public void Show(GuideMessageId id)
        {
            popups[id].Show();
        }

        public void Show(GuideMessageId id, string overrideText)
        {
            popups[id].Show(overrideText);
        }

        public void ShowFor(GuideMessageId id, float seconds)
        {
            popups[id].ShowFor(seconds);
        }

        public void Hide(GuideMessageId id)
        {
            popups[id].Hide();
        }

        public void HideAll()
        {
            foreach (KeyValuePair<GuideMessageId, GuidePopup> pair in popups)
            {
                pair.Value.Hide();
            }
        }

        /// <summary>Shows the next of "Well done!", "Great work!", "Excellent!", "Nice thinking!", "Great job!" for 3 s.</summary>
        public void ShowCongratulation()
        {
            string[] texts = GuideUiSpecs.CongratulationTexts;
            congratulationIndex = (congratulationIndex + 1) % texts.Length;
            popups[GuideMessageId.Congratulation].Show(texts[congratulationIndex]);
        }

        /// <summary>"That value is outside the model's range. Try between X and Y"</summary>
        public void ShowOutOfRange(string min, string max)
        {
            popups[GuideMessageId.OutOfRange].Show("That value is outside the model's range. Try between " + min + " and " + max);
        }

        // ---------- element labels ----------

        /// <summary>Name tag that follows 'target' (offset is in world axes, e.g. 0.6 m above it).</summary>
        public GuideElementLabel AddElementLabel(Transform target, string name, Vector3 offset)
        {
            GuideElementLabel label = GuideElementLabel.Create(root, name, target, offset, fadeSeconds);
            labels.Add(label);
            return label;
        }

        /// <summary>Name tag at a fixed world position.</summary>
        public GuideElementLabel AddElementLabel(Vector3 worldPosition, string name)
        {
            GuideElementLabel label = GuideElementLabel.Create(root, name, null, worldPosition, fadeSeconds);
            labels.Add(label);
            return label;
        }

        public void SetElementLabelsVisible(bool visible)
        {
            foreach (GuideElementLabel label in labels)
            {
                if (visible)
                {
                    label.Show();
                }
                else
                {
                    label.Hide();
                }
            }
        }

        // ---------- preview ----------

        private IEnumerator Preview()
        {
            do
            {
                GuideMessageSpec[] specs = GuideUiSpecs.All();
                foreach (GuideMessageSpec spec in specs)
                {
                    if (spec.Id == GuideMessageId.Congratulation)
                    {
                        ShowCongratulation();
                    }
                    else if (spec.Id == GuideMessageId.OutOfRange)
                    {
                        ShowOutOfRange("X", "Y");
                    }
                    else
                    {
                        popups[spec.Id].Show();
                    }

                    // Long texts stay up a bit longer.
                    float seconds = spec.Body.Length > 60 ? previewSecondsPerMessage * 1.6f : previewSecondsPerMessage;
                    yield return new WaitForSecondsRealtime(seconds);
                    popups[spec.Id].Hide();
                    yield return new WaitForSecondsRealtime(0.35f);
                }

                // voice bars
                Voice.SetSpeaking(true);
                yield return new WaitForSecondsRealtime(3f);
                Voice.SetSpeaking(false);
                yield return new WaitForSecondsRealtime(0.4f);

                // timer
                Timer.Begin(8f);
                yield return new WaitForSecondsRealtime(9.5f);
                Timer.Stop();

                // transcript
                Transcript.Clear();
                Transcript.AddLine("Guide", "Move the black holes closer. What do you expect?");
                Transcript.AddLine("You", "The orbit gets faster.");
                Transcript.AddLine("Guide", "Closer means a faster orbit and a higher frequency.");
                Transcript.Show();
                yield return new WaitForSecondsRealtime(5f);
                Transcript.Hide();
                yield return new WaitForSecondsRealtime(0.4f);

                // element labels (demo positions only)
                GuideElementLabel a = AddElementLabel(new Vector3(-3.25f, 1.6f, 0f), "Black holes");
                GuideElementLabel b = AddElementLabel(new Vector3(-7.3f, 2.2f, 0f), "Data panel");
                GuideElementLabel c = AddElementLabel(new Vector3(1.3f, 3.0f, 0f), "Chirp graph");
                SetElementLabelsVisible(true);
                yield return new WaitForSecondsRealtime(4f);
                SetElementLabelsVisible(false);
                yield return new WaitForSecondsRealtime(0.5f);
                RemoveLabel(a);
                RemoveLabel(b);
                RemoveLabel(c);
            }
            while (previewLoop);
        }

        private void RemoveLabel(GuideElementLabel label)
        {
            labels.Remove(label);
            Destroy(label.gameObject);
        }
    }
}
