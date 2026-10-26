namespace DTET.Guide
{
    /// <summary>
    /// One id per pop-up in the VR guide script. Whoever wires the logic calls
    /// GuideUiHost.Show(GuideMessageId.X) / Hide(...) - this kit only draws them.
    /// </summary>
    public enum GuideMessageId
    {
        // ---- Always available ----
        TurnToOrbit,            // "Please turn a little toward the orbit"
        AnswerRight,            // "Your answer is right" (3 s)
        AnswerWrong,            // "Not quite. Let's look again" (3 s)
        MergerComplete,         // "The conceptual merger is complete" (3 s)
        OutOfRange,             // "That value is outside the model's range. Try between X and Y" (use GuideUiHost.ShowOutOfRange)
        LearningBoardTag,       // "Learning board"
        MainAreaTag,            // "Main area"

        // ---- Stage 1 ----
        LookAround,             // "Have a look around"
        LearningGoals,          // the five learning goals
        WalkToBlackHoles,       // "Walk toward the black holes"
        Arrived,                // "You have arrived"

        // ---- Stage 2 ----
        OrbitIntro,             // "The two black holes orbit each other"
        WalkAround,             // "Walk around the system"
        AskGuide,               // "Ask the guide by voice"
        Rescaled,               // "Sizes and distances are rescaled"
        SymmetryTask,           // "Does the pair look the same from every side?"
        TryAnotherViewpoint,    // "Try another viewpoint"

        // ---- Stage 3 ----
        MoveCloser,             // "Move the black holes closer. What do you expect?"
        InspiralStarted,        // "The inspiral has started"
        SeparationShrinking,    // "The separation is shrinking"
        Paused,                 // "Paused"
        CloserFaster,           // "Closer means a faster orbit and a higher frequency"

        // ---- Stage 4 ----
        WatchSlowMotion,        // "Watch the merger in slow motion"
        PostMerger,             // "Conceptual post-merger state. ..."

        // ---- Stage 5 ----
        ChirpNotPicture,        // "Chirp: a detector-like signal, not a picture"
        FrequencyMatches,       // "The rising frequency matches the faster orbit"
        PointTask,              // "Point to the inspiral, then the merger, then the post-merger"
        ChirpInspiralTag,       // "Inspiral: frequency rising"
        ChirpMergerTag,         // "Merger: the peak"
        ChirpPostTag,           // "Post-merger: the signal fades"

        // ---- Stage 6 ----
        SystemsIntro,           // "System 1 and System 2"
        WhatIfCloser,           // "What if it had started closer?"
        OnlyStartDiffers,       // "Only the starting separation differs"
        WhichMergesFirst,       // "Which system merges first, and why?"
        FinalCongratulations,   // "Congratulations! You finished the experience"

        // Rotating "Well done!" style message: use GuideUiHost.ShowCongratulation().
        Congratulation
    }
}
