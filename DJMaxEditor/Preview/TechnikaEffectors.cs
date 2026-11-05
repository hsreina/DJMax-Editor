namespace DJMaxEditor.Preview
{
    /// <summary>
    /// The arcade's note-series effector: whether notes fade in as the sweep approaches or
    /// fade out before it reaches them. Fade In hides notes far ahead of the line, Fade Out
    /// hides them right where the line reads, and the 2 variants do it over roughly half the
    /// distance. The manuals name them FI / FI2 / FO / FO2; they are position-based, not
    /// speed-based, so the renderer derives opacity from each note's distance in scans, the
    /// same number the approach glow uses.
    /// </summary>
    internal enum TechnikaNoteFader
    {
        Off,
        FadeIn,
        FadeIn2,
        FadeOut,
        FadeOut2
    }

    /// <summary>
    /// The arcade's timeline-series effector. Blink flashes the sweep on for about half
    /// (Blink) or a quarter (Blink2) of the time; Blind removes it altogether, leaving
    /// memory play. The duty is taken off the musical clock rather than a wall clock, the
    /// same contract the note shine loop keeps, so a stopped transport holds a still,
    /// deterministic frame.
    /// </summary>
    internal enum TechnikaLineEffector
    {
        On,
        Blink,
        Blink2,
        Blind
    }
}
