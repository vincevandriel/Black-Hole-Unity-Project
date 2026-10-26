using UnityEngine;

namespace DTET.Guide
{
    public enum GuideKind { Info, Task, Success, Warning, Plain, Tag }

    public enum GuideAnchorMode
    {
        /// <summary>Fixed position in the scene (Offset = world position).</summary>
        World,
        /// <summary>Floats in front of the user's head (Offset = right, up, distance in metres).</summary>
        HeadLocked,
        /// <summary>Follows a Transform (Offset = world-space offset from the target).</summary>
        FollowTarget
    }

    /// <summary>Everything that decides how one pop-up looks and where it sits.</summary>
    public sealed class GuideMessageSpec
    {
        public GuideMessageId Id;
        public GuideKind Kind;
        public string Title;            // small heading, may be null
        public string Body;
        public float Width, Height;     // canvas pixels
        public float Scale;             // metres per pixel
        public GuideAnchorMode Anchor;
        public Vector3 Offset;          // meaning depends on Anchor
        public float HoldSeconds;       // > 0: hides itself after this long; 0: stays until Hide()
    }

    /// <summary>
    /// All texts, sizes and positions in one place. To move or reword a pop-up, change it here
    /// (or move its GameObject in the Scene view while playing, then copy the position back).
    /// </summary>
    public static class GuideUiSpecs
    {
        // Scene positions (exhibit centre is (-3.25, 0, 0); chirp card (1.3, 2.1, 0); learning board (3.4, 2, 3)).
        public static readonly Vector3 HintSlot = new Vector3(-3.25f, 2.5f, -2.2f);
        public static readonly Vector3 ChirpHintSlot = new Vector3(1.3f, 3.7f, -0.3f);

        // Head-locked offsets: x = right, y = up, z = distance in front of the eyes.
        private static readonly Vector3 HeadMain = new Vector3(0f, -0.05f, 1.6f);

        private const float WorldScale = 0.005f;
        private const float HeadScale = 0.002f;
        private const float TagScale = 0.004f;

        public static readonly string[] CongratulationTexts =
        {
            "Well done!", "Great work!", "Excellent!", "Nice thinking!", "Great job!"
        };

        public const string LearningGoalsBody =
            "•  See what happens to the wave when the black holes move closer\n" +
            "•  Find out why they orbit faster and faster\n" +
            "•  Follow them from orbit to merger and see what the wave does\n" +
            "•  Compare two systems and find out why they differ\n" +
            "•  Find out why one perfect sphere makes no waves, but two orbiting bodies do";

        public static GuideMessageSpec[] All()
        {
            return new[]
            {
                // ---------------- always available (head-locked, short) ----------------
                Head(GuideMessageId.TurnToOrbit, GuideKind.Info, null, "Please turn a little toward the orbit", 0f, new Vector3(0f, -0.2f, 1.6f)),
                Head(GuideMessageId.AnswerRight, GuideKind.Success, null, "Your answer is right", 3f, HeadMain),
                Head(GuideMessageId.AnswerWrong, GuideKind.Warning, null, "Not quite. Let's look again", 3f, HeadMain),
                Head(GuideMessageId.MergerComplete, GuideKind.Info, null, "The conceptual merger is complete", 3f, HeadMain),
                Head(GuideMessageId.OutOfRange, GuideKind.Warning, "OUT OF RANGE", "That value is outside the model's range. Try between X and Y", 4f, HeadMain, 600f, 150f),
                Head(GuideMessageId.Congratulation, GuideKind.Success, null, CongratulationTexts[0], 3f, HeadMain),

                // zone tags
                Tag(GuideMessageId.LearningBoardTag, "Learning board", new Vector3(3.4f, 3.2f, 3f)),
                Tag(GuideMessageId.MainAreaTag, "Main area", new Vector3(-3.25f, 3.2f, -2.8f)),

                // ---------------- Stage 1 ----------------
                World(GuideMessageId.LookAround, GuideKind.Info, "STAGE 1", "Have a look around", HintSlot),
                World(GuideMessageId.LearningGoals, GuideKind.Info, "WHAT YOU WILL LEARN", LearningGoalsBody, HintSlot, 700f, 330f),
                World(GuideMessageId.WalkToBlackHoles, GuideKind.Task, "TASK", "Walk toward the black holes", HintSlot),
                World(GuideMessageId.Arrived, GuideKind.Success, null, "You have arrived", HintSlot, hold: 3f),

                // ---------------- Stage 2 ----------------
                World(GuideMessageId.OrbitIntro, GuideKind.Info, "STAGE 2", "The two black holes orbit each other", HintSlot),
                World(GuideMessageId.WalkAround, GuideKind.Task, "TASK", "Walk around the system", HintSlot),
                World(GuideMessageId.AskGuide, GuideKind.Info, "VOICE", "Ask the guide by voice", HintSlot),
                World(GuideMessageId.Rescaled, GuideKind.Warning, "NOTE", "Sizes and distances are rescaled", HintSlot),
                World(GuideMessageId.SymmetryTask, GuideKind.Task, "TASK", "Does the pair look the same from every side?", HintSlot),
                World(GuideMessageId.TryAnotherViewpoint, GuideKind.Info, null, "Try another viewpoint", HintSlot),

                // ---------------- Stage 3 ----------------
                World(GuideMessageId.MoveCloser, GuideKind.Task, "STAGE 3", "Move the black holes closer. What do you expect?", HintSlot),
                World(GuideMessageId.InspiralStarted, GuideKind.Info, null, "The inspiral has started", HintSlot),
                World(GuideMessageId.SeparationShrinking, GuideKind.Info, null, "The separation is shrinking", HintSlot),
                World(GuideMessageId.Paused, GuideKind.Plain, null, "Paused", HintSlot, 300f, 90f),
                World(GuideMessageId.CloserFaster, GuideKind.Info, null, "Closer means a faster orbit and a higher frequency", HintSlot),

                // ---------------- Stage 4 ----------------
                World(GuideMessageId.WatchSlowMotion, GuideKind.Info, "STAGE 4", "Watch the merger in slow motion", HintSlot),
                World(GuideMessageId.PostMerger, GuideKind.Warning, "CONCEPTUAL", "Conceptual post-merger state. The simple formula no longer applies this close.", HintSlot, 560f, 170f),

                // ---------------- Stage 5 ----------------
                World(GuideMessageId.ChirpNotPicture, GuideKind.Warning, "STAGE 5", "Chirp: a detector-like signal, not a picture", ChirpHintSlot),
                World(GuideMessageId.FrequencyMatches, GuideKind.Info, null, "The rising frequency matches the faster orbit", ChirpHintSlot),
                World(GuideMessageId.PointTask, GuideKind.Task, "TASK", "Point to the inspiral, then the merger, then the post-merger", ChirpHintSlot, 560f, 150f),
                Tag(GuideMessageId.ChirpInspiralTag, "Inspiral: frequency rising", new Vector3(0.5f, 3.05f, 0f)),
                Tag(GuideMessageId.ChirpMergerTag, "Merger: the peak", new Vector3(1.3f, 3.05f, 0f)),
                Tag(GuideMessageId.ChirpPostTag, "Post-merger: the signal fades", new Vector3(2.1f, 3.05f, 0f)),

                // ---------------- Stage 6 ----------------
                World(GuideMessageId.SystemsIntro, GuideKind.Info, "STAGE 6", "System 1 and System 2", HintSlot),
                World(GuideMessageId.WhatIfCloser, GuideKind.Task, "TASK", "What if it had started closer?", HintSlot),
                World(GuideMessageId.OnlyStartDiffers, GuideKind.Info, null, "Only the starting separation differs", HintSlot),
                World(GuideMessageId.WhichMergesFirst, GuideKind.Task, "TASK", "Which system merges first, and why?", HintSlot),
                Head(GuideMessageId.FinalCongratulations, GuideKind.Success, "EXPERIENCE COMPLETE", "Congratulations! You finished the experience", 0f, HeadMain, 600f, 170f)
            };
        }

        // ---------- small factories ----------

        private static GuideMessageSpec World(GuideMessageId id, GuideKind kind, string title, string body, Vector3 position,
            float width = 480f, float height = 0f, float hold = 0f)
        {
            return new GuideMessageSpec
            {
                Id = id, Kind = kind, Title = title, Body = body,
                Width = width, Height = height > 0f ? height : (title != null ? 130f : 100f),
                Scale = WorldScale, Anchor = GuideAnchorMode.World, Offset = position, HoldSeconds = hold
            };
        }

        private static GuideMessageSpec Head(GuideMessageId id, GuideKind kind, string title, string body, float hold,
            Vector3 offset, float width = 520f, float height = 0f)
        {
            return new GuideMessageSpec
            {
                Id = id, Kind = kind, Title = title, Body = body,
                Width = width, Height = height > 0f ? height : (title != null ? 130f : 100f),
                Scale = HeadScale, Anchor = GuideAnchorMode.HeadLocked, Offset = offset, HoldSeconds = hold
            };
        }

        private static GuideMessageSpec Tag(GuideMessageId id, string body, Vector3 position)
        {
            return new GuideMessageSpec
            {
                Id = id, Kind = GuideKind.Tag, Title = null, Body = body,
                Width = 200f, Height = 70f, Scale = TagScale,
                Anchor = GuideAnchorMode.World, Offset = position
            };
        }
    }
}
