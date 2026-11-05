using System;
using System.Collections.Generic;
using System.Linq;
using DJMaxEditor.DJMax;
using DJMaxEditor.Files.FormatDetection;

namespace DJMaxEditor.Preview
{
    public enum GameplayPreviewProfile
    {
        Generic,
        Technika
    }

    public enum GameplayPreviewNoteKind
    {
        Basic,
        Drag,
        ChainHead,
        ChainNode,
        RepeatHead,
        RepeatHeadHold,
        Repeat,
        RepeatHold,
        Hold,
        Generic
    }

    public enum GameplayPreviewNoteState
    {
        Inactive,
        Prepare,
        Active,
        Resolved
    }

    /// <summary>
    /// Facts about <see cref="GameplayPreviewNoteKind"/> that every preview surface has to
    /// agree on. Lives beside the enum rather than inside a renderer so the playfield and
    /// the timeline cannot drift.
    /// </summary>
    public static class GameplayPreviewNoteKinds
    {
        /// <summary>
        /// Whether a kind is a hold gesture, and so draws a trail behind its head. A
        /// TECHNIKA note's stored duration is the length of its keysound, not the length of
        /// a hold, so gating a trail on a duration merely being non-zero would put a stub
        /// behind every note; whether a note is held is a property of its kind.
        /// </summary>
        public static bool HasHoldTrail(GameplayPreviewNoteKind kind)
        {
            return kind == GameplayPreviewNoteKind.Hold ||
                kind == GameplayPreviewNoteKind.Drag ||
                kind == GameplayPreviewNoteKind.RepeatHold ||
                kind == GameplayPreviewNoteKind.RepeatHeadHold;
        }
    }

    public sealed class GameplayPreviewProfileSuggestion
    {
        public GameplayPreviewProfileSuggestion(
            GameplayPreviewProfile profile,
            bool requiresConfirmation,
            string explanation)
        {
            Profile = profile;
            RequiresConfirmation = requiresConfirmation;
            Explanation = explanation ?? string.Empty;
        }

        public GameplayPreviewProfile Profile { get; private set; }

        public bool RequiresConfirmation { get; private set; }

        public string Explanation { get; private set; }
    }

    public static class GameplayPreviewProfileResolver
    {
        public static GameplayPreviewProfileSuggestion Suggest(PlayerData model)
        {
            ChartFormat? format = model == null ? null : model.SourceFormat;
            if (format == ChartFormat.PtffDecrypted ||
                format == ChartFormat.PtffEncryptedTechnika)
            {
                return new GameplayPreviewProfileSuggestion(
                    GameplayPreviewProfile.Technika,
                    true,
                    "PTFF can contain TECHNIKA or Trilogy data. Confirm the TECHNIKA profile.");
            }

            if (format == ChartFormat.TrailerRespectV)
            {
                // .bytes Technika Q trailer data speaks the same note vocabulary as PTFF,
                // so it renders directly on the TECHNIKA playfield - no confirmation.
                return new GameplayPreviewProfileSuggestion(
                    GameplayPreviewProfile.Technika,
                    false,
                    "Technika Q trailer data uses the TECHNIKA projection.");
            }

            if (format == ChartFormat.TechmaniaTrack)
            {
                // track.tech declares TECHNIKA-style metadata (beats per scan, playable
                // lanes), so it renders directly on the TECHNIKA playfield - no
                // confirmation.
                return new GameplayPreviewProfileSuggestion(
                    GameplayPreviewProfile.Technika,
                    false,
                    "TECHMANIA track.tech uses the TECHNIKA projection.");
            }

            return new GameplayPreviewProfileSuggestion(
                GameplayPreviewProfile.Generic,
                false,
                "This format uses the generic lane preview.");
        }
    }

    public sealed class ProjectedGameplayNote
    {
        internal ProjectedGameplayNote(EventData source)
        {
            Source = source;
        }

        public EventData Source { get; internal set; }

        public int Lane { get; internal set; }

        public int Pulse { get; internal set; }

        public int DurationPulse { get; internal set; }

        public GameplayPreviewNoteKind Kind { get; internal set; }

        public int ScanIndex { get; internal set; }

        public double RelativeScan { get; internal set; }

        public double X { get; internal set; }

        public double Y { get; internal set; }

        public bool IsTopHalf { get; internal set; }

        public bool EndOfScan { get; internal set; }

        public bool IsImplicitChainNode { get; internal set; }

        public GameplayPreviewNoteState State { get; internal set; }

        public bool ApproachVisible { get; internal set; }

        public double ApproachProgress { get; internal set; }

        /// <summary>
        /// How far the sweep is past the note, in scans: negative while the note is still
        /// ahead of the line, positive once the line has crossed it. The hit burst and the
        /// hold glow read this as their clock, so it is part of the frame rather than
        /// recomputed per renderer.
        /// </summary>
        public double ApproachScanDistance { get; internal set; }

        /// <summary>
        /// Pulse through which this note stays Active. For a RepeatHead that anchors a
        /// series, this is the last member's end so the head does not vanish mid-run.
        /// </summary>
        public int ResolveThroughPulse { get; internal set; }

        internal ProjectedGameplayNote Copy()
        {
            return (ProjectedGameplayNote)MemberwiseClone();
        }
    }

    public sealed class GameplayPreviewFrame
    {
        internal GameplayPreviewFrame(
            int currentTick,
            double currentScan,
            IReadOnlyList<ProjectedGameplayNote> notes)
        {
            CurrentTick = currentTick;
            CurrentScan = currentScan;
            CurrentIntScan = (int)Math.Floor(currentScan);
            CurrentPhase = currentScan - CurrentIntScan;
            Notes = notes;
        }

        public int CurrentTick { get; private set; }

        public double CurrentScan { get; private set; }

        public int CurrentIntScan { get; private set; }

        public double CurrentPhase { get; private set; }

        public IReadOnlyList<ProjectedGameplayNote> Notes { get; private set; }
    }

    public sealed class GameplayPreviewProjection
    {
        private readonly ushort _ticksPerMeasure;
        private readonly int _beatsPerScan;

        internal GameplayPreviewProjection(
            GameplayPreviewProfile profile,
            string statusLabel,
            int laneCount,
            ushort ticksPerMeasure,
            int beatsPerScan,
            float tempo,
            IList<ProjectedGameplayNote> notes,
            IList<string> diagnostics)
            : this(
                profile,
                statusLabel,
                laneCount,
                ticksPerMeasure,
                beatsPerScan,
                tempo,
                TechnikaScrollDirection.Clockwise,
                notes,
                diagnostics)
        {
        }

        internal GameplayPreviewProjection(
            GameplayPreviewProfile profile,
            string statusLabel,
            int laneCount,
            ushort ticksPerMeasure,
            int beatsPerScan,
            float tempo,
            TechnikaScrollDirection scrollDirection,
            IList<ProjectedGameplayNote> notes,
            IList<string> diagnostics)
        {
            Profile = profile;
            StatusLabel = statusLabel;
            LaneCount = laneCount;
            _ticksPerMeasure = ticksPerMeasure;
            _beatsPerScan = beatsPerScan;
            BeatsPerScan = Math.Max(1, beatsPerScan);
            ScanSeconds = tempo > 0.0f
                ? (BeatsPerScan * 60.0) / tempo
                : 0.0;
            ScrollDirection = scrollDirection;
            Notes = new List<ProjectedGameplayNote>(notes).AsReadOnly();
            Diagnostics = new List<string>(diagnostics).AsReadOnly();
        }

        public GameplayPreviewProfile Profile { get; private set; }

        public string StatusLabel { get; private set; }

        public int LaneCount { get; private set; }

        /// <summary>Beats in one scan - four on standard TECHNIKA charts.</summary>
        public int BeatsPerScan { get; private set; }

        /// <summary>Seconds one scan lasts at the chart's tempo, or 0 when the tempo is unknown.</summary>
        public double ScanSeconds { get; private set; }

        /// <summary>
        /// The TECHNIKA scroll-direction effector this projection was placed under;
        /// <see cref="TechnikaScrollDirection.Clockwise"/> for the arcade default and for
        /// every non-TECHNIKA profile. Renderers read it to sweep the scanline, lay hold
        /// bodies and orient the approach glow the same way the notes were placed.
        /// </summary>
        public TechnikaScrollDirection ScrollDirection { get; private set; }

        public IReadOnlyList<ProjectedGameplayNote> Notes { get; private set; }

        public IReadOnlyList<string> Diagnostics { get; private set; }

        public GameplayPreviewFrame CreateFrame(int currentTick)
        {
            return CreateFrame(currentTick, false, 1.0);
        }

        /// <summary>
        /// Creates the small playback window used by the renderer. Unlike the
        /// diagnostic/full frame, it does not clone notes that cannot be drawn.
        /// </summary>
        public GameplayPreviewFrame CreateRenderableFrame(int currentTick)
        {
            return CreateRenderableFrame(currentTick, 1.0);
        }

        /// <summary>
        /// Creates the playback window with the chart's scroll speed applied to the
        /// SWEEP, not the notes: every note keeps its authored scan position, and
        /// the scanline crosses the field at this many times the musical rate - 2x
        /// reaches notes twice as fast, 1/2x lags behind, 1 plays the plain clock.
        /// States, the hit flash and the approach glow all measure from the sweep,
        /// so they follow it without moving a single note.
        /// </summary>
        public GameplayPreviewFrame CreateRenderableFrame(int currentTick, double scrollSpeed)
        {
            return CreateFrame(currentTick, true, scrollSpeed);
        }

        private GameplayPreviewFrame CreateFrame(
            int currentTick,
            bool renderableOnly,
            double scrollSpeed)
        {
            int ticks = Math.Max(1, (int)_ticksPerMeasure);
            double currentScan = Profile == GameplayPreviewProfile.Technika
                ? (4.0 * currentTick) / (ticks * Math.Max(1, _beatsPerScan))
                : currentTick / (double)ticks;
            if (renderableOnly && Profile == GameplayPreviewProfile.Technika)
            {
                // Scroll speed drives the sweep: the scan position advances at this
                // many times the musical rate. Notes never move - their scan index,
                // relative position and X are the authored ones.
                currentScan *= scrollSpeed;
            }
            int currentIntScan = (int)Math.Floor(currentScan);
            double currentPhase = currentScan - currentIntScan;
            var notes = new List<ProjectedGameplayNote>(Notes.Count);

            foreach (ProjectedGameplayNote topology in Notes)
            {
                if (Profile == GameplayPreviewProfile.Technika)
                {
                    double pulsesPerScan = 240.0 * Math.Max(1, _beatsPerScan);
                    double headFloatScan = topology.Pulse / pulsesPerScan;
                    double tailFloatScan = GameplayPreviewNoteKinds.HasHoldTrail(topology.Kind)
                        ? (topology.Pulse + topology.DurationPulse) / pulsesPerScan
                        : headFloatScan;

                    if (renderableOnly && !IsInRenderableWindow(
                            currentIntScan, headFloatScan, tailFloatScan))
                    {
                        continue;
                    }
                }
                else if (renderableOnly)
                {
                    // Generic rendering maps two measures around the playhead into
                    // the viewport, so notes outside that range cannot contribute
                    // pixels.
                    if (Math.Abs(topology.Source.Tick - currentTick) > ticks * 2)
                    {
                        continue;
                    }
                }

                ProjectedGameplayNote note = topology.Copy();
                if (Profile == GameplayPreviewProfile.Technika)
                {
                    double pulsesPerScan = 240.0 * Math.Max(1, _beatsPerScan);
                    // Only a hold answers for its tail: every note carries a keysound-length
                    // duration, so reading the raw tail for a tap would keep it lit behind
                    // the sweep until its sample "ends". A tap is Resolved the instant the
                    // sweep clears its head — except a RepeatHead, whose ResolveThroughPulse
                    // reaches the last tick of its series so the head stays until the run ends.
                    double noteFloatScan = topology.Pulse / pulsesPerScan;
                    double endFloatScan = GameplayPreviewNoteKinds.HasHoldTrail(topology.Kind)
                        ? (topology.Pulse + topology.DurationPulse) / pulsesPerScan
                        : noteFloatScan;
                    if (topology.ResolveThroughPulse > topology.Pulse)
                    {
                        endFloatScan = Math.Max(
                            endFloatScan,
                            topology.ResolveThroughPulse / pulsesPerScan);
                    }
                    double distance = currentScan - noteFloatScan;

                    if (currentScan > endFloatScan)
                    {
                        // Resolved the moment the sweep is past it, not at the end of the
                        // scan it sits in - the end-of-scan test left every note the line
                        // had already crossed sitting on the field at full brightness until
                        // the handover.
                        note.State = GameplayPreviewNoteState.Resolved;
                    }
                    else if (topology.ScanIndex < currentIntScan)
                    {
                        // Head behind, tail still ahead: a hold spanning into this scan or
                        // the next. It is still being played, so Active - the renderer draws
                        // the visible scans' worth of its body and skips the head it passed.
                        note.State = GameplayPreviewNoteState.Active;
                    }
                    else if (topology.ScanIndex == currentIntScan)
                    {
                        note.State = GameplayPreviewNoteState.Active;
                    }
                    else if (topology.ScanIndex == currentIntScan + 1)
                    {
                        note.State = currentPhase >= 0.875
                            ? GameplayPreviewNoteState.Active
                            : GameplayPreviewNoteState.Prepare;
                    }
                    else
                    {
                        note.State = GameplayPreviewNoteState.Inactive;
                    }

                    // The approach glow measures from the (possibly sped-up) sweep,
                    // so it fires as the faster line closes on the note.
                    note.ApproachScanDistance = distance;
                    note.ApproachVisible = distance >= -0.5 && distance <= 0;
                    note.ApproachProgress = note.ApproachVisible
                        ? Math.Max(0, Math.Min(1, (distance + 0.5) / 0.5))
                        : 0;
                }
                else
                {
                    int distance = note.Source.Tick - currentTick;
                    note.State = Math.Abs(distance) <= Math.Max(1, ticks / 16)
                        ? GameplayPreviewNoteState.Active
                        : distance < 0
                            ? GameplayPreviewNoteState.Resolved
                            : GameplayPreviewNoteState.Prepare;
                    note.X = Math.Max(0.05, Math.Min(0.95,
                        0.5 + (distance / (double)(ticks * 2))));
                }
                notes.Add(note);
            }

            return new GameplayPreviewFrame(currentTick, currentScan, notes.AsReadOnly());
        }

        private bool IsInRenderableWindow(
            int currentIntScan,
            double headFloatScan,
            double tailFloatScan)
        {
            // The Technika renderer draws this scan plus the one already waiting on
            // the other half - two float scans on stage. A hold belongs to the window
            // while any part of its span intersects them: its head may be scans behind
            // while its tail is still ahead, and testing the head alone is what clipped
            // a long hold at the scan past it. The far edge sits at the start of the
            // scan AFTER the waiting one (float scan current+2); an edge at current+1
            // admitted only notes exactly on the handover boundary. Taps answer for
            // their head alone (see CreateFrame for why the raw duration is not a tail).
            // currentIntScan is where the (possibly sped-up) sweep currently is, so a
            // faster sweep stages notes further along the chart, a slower one earlier.
            return tailFloatScan >= currentIntScan &&
                headFloatScan < currentIntScan + 2;
        }
    }

    public static class GameplayPreviewProjector
    {
        private const int PulsesPerMeasure = 960;
        private const int PulsesPerBeat = 240;
        private const int DefaultBeatsPerScan = 4;

        /// <summary>
        /// Beats per scan for the projection. Real TECHNIKA .pt charts are always four; a
        /// TECHMANIA .tech declares its own (2, 4, 8, 12 ...) in pattern metadata, and the
        /// scan boundary is what places every note, so a fixed four would pack a 2-bps chart
        /// into half-length scans. Anything that declares none or an illegal value keeps
        /// the four-beat default.
        /// </summary>
        private static int BeatsPerScanFor(PlayerData model)
        {
            int bps = model != null && model.TechMetadata != null
                ? model.TechMetadata.Bps
                : DefaultBeatsPerScan;
            return bps > 0 ? bps : DefaultBeatsPerScan;
        }

        /// <summary>
        /// Left edge of the TECHNIKA note field, as a fraction of the arcade's 1280 px
        /// width - where an upper-half scan begins and a lower-half one ends. Measured
        /// from the arcade client's own draw calls, not chosen: the sweep is continuous
        /// in x through a handover, and both halves' bright edges coincide there at
        /// x = 156.5 and x = 1116.5, so both halves share one screen rectangle 7 px left
        /// of centre rather than being mirror images of each other.
        /// </summary>
        public const double TechnikaFieldLeft = 156.5 / 1280.0;

        /// <summary>
        /// Right edge of the TECHNIKA note field. See <see cref="TechnikaFieldLeft"/>.
        /// The span is exactly 960 px - 0.75 of the width.
        /// </summary>
        public const double TechnikaFieldRight = 1116.5 / 1280.0;

        /// <summary>
        /// Which way the sweep travels over the named half under the clockwise scroll
        /// default: left to right over the upper half, right to left over the lower.
        /// Note placement, the scanline, hold trails and the approach glow all read this
        /// so the four cannot drift apart.
        /// </summary>
        public static bool TechnikaSweepRightward(bool isTopHalf)
        {
            return TechnikaSweepRightward(isTopHalf, TechnikaScrollDirection.Clockwise);
        }

        /// <summary>
        /// The four arcade readings of the scroll-direction effector: the clockwise
        /// default, its counter-clockwise inverse, and both half-fields traveling to the
        /// same edge. One rule for the projector's note placement and every renderer-side
        /// sweep (scanline, hold body, approach glow), so they cannot disagree about
        /// which way a scan runs.
        /// </summary>
        public static bool TechnikaSweepRightward(
            bool isTopHalf,
            TechnikaScrollDirection direction)
        {
            switch (direction)
            {
                case TechnikaScrollDirection.CounterClockwise:
                    return !isTopHalf;
                case TechnikaScrollDirection.AllLeft:
                    return false;
                case TechnikaScrollDirection.AllRight:
                    return true;
                default:
                    return isTopHalf;
            }
        }

        public static GameplayPreviewProjection Project(
            PlayerData model,
            GameplayPreviewProfile profile)
        {
            return Project(model, profile, TechnikaScrollDirection.Clockwise);
        }

        public static GameplayPreviewProjection Project(
            PlayerData model,
            GameplayPreviewProfile profile,
            TechnikaScrollDirection scrollDirection)
        {
            if (model == null) throw new ArgumentNullException("model");
            return profile == GameplayPreviewProfile.Technika
                ? ProjectTechnika(model, scrollDirection)
                : ProjectGeneric(model);
        }

        private static GameplayPreviewProjection ProjectTechnika(
            PlayerData model,
            TechnikaScrollDirection scrollDirection)
        {
            int ticksPerMeasure = Math.Max(1, (int)model.TickPerMinute);
            var diagnostics = new List<string>();
            var notes = new List<ProjectedGameplayNote>();

            // A .tech declares how many lanes are playable. Notes on later lanes are the
            // format's invisible/autoplay keysound lanes: they still trigger audio in game
            // but are never drawn, and letting one widen the field would misdraw every note.
            int playableLanes = model.TechMetadata != null &&
                                model.TechMetadata.PlayableLanes >= 2
                ? Math.Min(4, model.TechMetadata.PlayableLanes)
                : 0;

            foreach (TrackData track in model.Tracks)
            {
                if (track.Idx > 3) continue;
                foreach (EventData source in track.Events)
                {
                    if (playableLanes > 0 && (int)track.Idx >= playableLanes)
                    {
                        continue;
                    }
                    GameplayPreviewNoteKind? kind = Classify(source);
                    if (!kind.HasValue)
                    {
                        if (source.EventType == EventType.Note && source.Attribute != 100)
                        {
                            diagnostics.Add(
                                "Unsupported attribute " + source.Attribute +
                                " on lane " + track.Idx + " at tick " + source.Tick + ".");
                        }
                        continue;
                    }

                    notes.Add(new ProjectedGameplayNote(source)
                    {
                        Lane = (int)track.Idx,
                        Pulse = TickToPulse(source.Tick, ticksPerMeasure),
                        DurationPulse = TickToPulse(source.Duration, ticksPerMeasure),
                        Kind = kind.Value
                    });
                }
            }

            notes = notes
                .OrderBy(note => note.Pulse)
                .ThenBy(note => note.Lane)
                .ToList();

            ApplyChainFixups(notes, diagnostics);
            ApplyRepeatFixups(notes, diagnostics);
            ApplyEndOfScanMarkers(model, notes, ticksPerMeasure);

            // A .tech's declared playable lanes win; anything else (legacy .pt charts)
            // keeps deriving the count from the lanes the notes actually use.
            int laneCount = playableLanes > 0
                ? playableLanes
                : DeriveLaneCount(notes);
            int beatsPerScan = BeatsPerScanFor(model);
            foreach (ProjectedGameplayNote note in notes)
            {
                PlaceTechnikaNote(note, laneCount, beatsPerScan, scrollDirection);
            }

            return new GameplayPreviewProjection(
                GameplayPreviewProfile.Technika,
                "TECHNIKA PROFILE",
                laneCount,
                model.TickPerMinute,
                beatsPerScan,
                model.Tempo,
                scrollDirection,
                notes,
                diagnostics);
        }

        private static GameplayPreviewProjection ProjectGeneric(PlayerData model)
        {
            var diagnostics = new List<string>();
            var notes = new List<ProjectedGameplayNote>();
            List<TrackData> noteTracks = model.Tracks
                .Where(track => track.Events.Any(source => source.EventType == EventType.Note))
                .ToList();
            int laneCount = Math.Max(1, noteTracks.Count);

            for (int lane = 0; lane < noteTracks.Count; lane++)
            {
                foreach (EventData source in noteTracks[lane].Events)
                {
                    if (source.EventType != EventType.Note) continue;
                    notes.Add(new ProjectedGameplayNote(source)
                    {
                        Lane = lane,
                        Kind = GameplayPreviewNoteKind.Generic,
                        Pulse = source.Tick,
                        DurationPulse = source.Duration,
                        ScanIndex = 0,
                        RelativeScan = 0.5,
                        X = 0.5,
                        Y = (lane + 0.5) / laneCount,
                        IsTopHalf = false
                    });
                }
            }

            return new GameplayPreviewProjection(
                GameplayPreviewProfile.Generic,
                "GENERIC LANE PREVIEW  |  APPROXIMATION",
                laneCount,
                model.TickPerMinute,
                DefaultBeatsPerScan,
                model.Tempo,
                notes,
                diagnostics);
        }

        private static GameplayPreviewNoteKind? Classify(EventData source)
        {
            if (source == null || source.EventType != EventType.Note ||
                source.Attribute == 100)
            {
                return null;
            }

            switch (source.Attribute)
            {
                case 0:
                    return source.Duration > 6
                        ? GameplayPreviewNoteKind.Drag
                        : GameplayPreviewNoteKind.Basic;
                case 5:
                    return GameplayPreviewNoteKind.ChainHead;
                case 6:
                    return GameplayPreviewNoteKind.ChainNode;
                case 10:
                    return source.Duration > 6
                        ? GameplayPreviewNoteKind.RepeatHeadHold
                        : GameplayPreviewNoteKind.RepeatHead;
                case 11:
                    return source.Duration > 6
                        ? GameplayPreviewNoteKind.RepeatHold
                        : GameplayPreviewNoteKind.Repeat;
                case 12:
                    return GameplayPreviewNoteKind.Hold;
                default:
                    return null;
            }
        }

        private static int TickToPulse(int tick, int ticksPerMeasure)
        {
            return (int)(((long)tick * PulsesPerMeasure) / Math.Max(1, ticksPerMeasure));
        }

        private static void ApplyChainFixups(
            IList<ProjectedGameplayNote> notes,
            IList<string> diagnostics)
        {
            // Pass 1 - spans, delimited purely from the explicit typing. A .tech chains one
            // ChainHead through every ChainNode that follows (the waypoints cross lanes), and
            // the next head starts the next span; there is no single closing node. The legacy
            // dialect's span ends at its one node. Streaming "first node closes" chopped a
            // real dozen-node chain into a pair plus eleven orphans - and closing on any
            // other-family note killed a chain that shares a tick with, say, a repeat head
            // in another lane.
            var heads = new List<ProjectedGameplayNote>();
            var spanEnd = new List<int>();
            var spanNodePulses = new List<HashSet<int>>();
            int openSpan = -1;
            foreach (ProjectedGameplayNote note in notes)
            {
                if (note.Kind == GameplayPreviewNoteKind.ChainHead)
                {
                    heads.Add(note);
                    spanEnd.Add(note.Pulse);
                    spanNodePulses.Add(new HashSet<int>());
                    openSpan = heads.Count - 1;
                }
                else if (note.Kind == GameplayPreviewNoteKind.ChainNode)
                {
                    if (openSpan < 0)
                    {
                        diagnostics.Add("Orphan chain node at tick " + note.Source.Tick + ".");
                        continue;
                    }
                    spanEnd[openSpan] = note.Pulse;
                    spanNodePulses[openSpan].Add(note.Pulse);
                }
            }

            // Pass 2 - the legacy dialect traces its path through ordinary taps that the
            // file never re-tagged: absorb the basics strictly inside a span, except taps on
            // a node's own pulse, which are real taps in another lane sharing the waypoint's
            // tick.
            openSpan = -1;
            int noteIndex = 0;
            foreach (ProjectedGameplayNote note in notes)
            {
                if (note.Kind == GameplayPreviewNoteKind.ChainHead)
                {
                    // The heads list follows the same walk order, so a pointer is enough.
                    while (noteIndex < heads.Count && heads[noteIndex] != note)
                    {
                        noteIndex++;
                    }
                    openSpan = noteIndex < heads.Count ? noteIndex : -1;
                    noteIndex++;
                    continue;
                }

                if (note.Kind == GameplayPreviewNoteKind.ChainNode)
                {
                    // Explicit waypoint: nothing to absorb or hand back here.
                    continue;
                }

                if (openSpan >= 0 && note.Kind == GameplayPreviewNoteKind.Basic &&
                    note.Pulse > heads[openSpan].Pulse &&
                    note.Pulse <= spanEnd[openSpan] &&
                    !spanNodePulses[openSpan].Contains(note.Pulse))
                {
                    note.Kind = GameplayPreviewNoteKind.ChainNode;
                    note.IsImplicitChainNode = true;
                }
            }

            for (int i = 0; i < heads.Count; i++)
            {
                if (spanEnd[i] == heads[i].Pulse)
                {
                    diagnostics.Add(
                        "Unclosed chain beginning at pulse " + heads[i].Pulse + ".");
                }
            }
        }

        private static void ApplyRepeatFixups(
            IList<ProjectedGameplayNote> notes,
            IList<string> diagnostics)
        {
            var openByLane = new bool[4];
            // The legacy dialect tags the single closing tick with Repeat/RepeatHold, while
            // a .tech series names every post-head marker Repeat (a held RepeatHold may sit
            // among them). An end marker therefore joins the series but does not close it;
            // only a fresh head or a non-repeat note on the same lane does.
            var endSeenByLane = new bool[4];
            var headIndexByLane = new int[] { -1, -1, -1, -1 };
            for (int i = 0; i < notes.Count; i++)
            {
                ProjectedGameplayNote note = notes[i];
                note.ResolveThroughPulse = ResolveEndPulse(note);

                if (note.Kind == GameplayPreviewNoteKind.RepeatHead ||
                    note.Kind == GameplayPreviewNoteKind.RepeatHeadHold)
                {
                    if (openByLane[note.Lane] && !endSeenByLane[note.Lane])
                    {
                        // Legacy intermediate ticks keep the head attribute until the end marker.
                        note.Kind = note.Kind == GameplayPreviewNoteKind.RepeatHeadHold
                            ? GameplayPreviewNoteKind.RepeatHold
                            : GameplayPreviewNoteKind.Repeat;
                        ExtendResolveThrough(notes, headIndexByLane[note.Lane], note);
                    }
                    else
                    {
                        openByLane[note.Lane] = true;
                        endSeenByLane[note.Lane] = false;
                        headIndexByLane[note.Lane] = i;
                    }
                }
                else if (note.Kind == GameplayPreviewNoteKind.Repeat ||
                    note.Kind == GameplayPreviewNoteKind.RepeatHold)
                {
                    if (openByLane[note.Lane])
                    {
                        endSeenByLane[note.Lane] = true;
                        ExtendResolveThrough(notes, headIndexByLane[note.Lane], note);
                    }
                    else
                    {
                        diagnostics.Add(
                            "Orphan repeat node on lane " + note.Lane +
                            " at tick " + note.Source.Tick + ".");
                    }
                }
                else if (note.Lane >= 0 && note.Lane < openByLane.Length &&
                    openByLane[note.Lane])
                {
                    // A repeat never leaves its lane, so only a same-lane note closes the
                    // series; anything happening in other lanes is irrelevant.
                    openByLane[note.Lane] = false;
                    endSeenByLane[note.Lane] = false;
                    headIndexByLane[note.Lane] = -1;
                }
            }

            for (int lane = 0; lane < openByLane.Length; lane++)
            {
                if (openByLane[lane])
                {
                    diagnostics.Add("Unclosed repeat series on lane " + lane + ".");
                }
            }
        }

        private static int ResolveEndPulse(ProjectedGameplayNote note)
        {
            if (note == null)
            {
                return 0;
            }
            if (GameplayPreviewNoteKinds.HasHoldTrail(note.Kind) && note.DurationPulse > 0)
            {
                return note.Pulse + note.DurationPulse;
            }
            return note.Pulse;
        }

        private static void ExtendResolveThrough(
            IList<ProjectedGameplayNote> notes,
            int headIndex,
            ProjectedGameplayNote member)
        {
            if (notes == null || headIndex < 0 || headIndex >= notes.Count || member == null)
            {
                return;
            }

            int end = ResolveEndPulse(member);
            ProjectedGameplayNote head = notes[headIndex];
            if (end > head.ResolveThroughPulse)
            {
                head.ResolveThroughPulse = end;
            }
        }

        private static void ApplyEndOfScanMarkers(
            PlayerData model,
            IList<ProjectedGameplayNote> notes,
            int ticksPerMeasure)
        {
            foreach (TrackData track in model.Tracks)
            {
                if (track.Idx < 4 || track.Idx > 7) continue;
                int lane = (int)track.Idx - 4;
                foreach (EventData marker in track.Events)
                {
                    if (marker.EventType != EventType.Note) continue;
                    int pulse = TickToPulse(marker.Tick, ticksPerMeasure);
                    ProjectedGameplayNote match = notes.FirstOrDefault(
                        note => note.Lane == lane && note.Pulse == pulse);
                    if (match != null)
                    {
                        match.EndOfScan = true;
                    }
                }
            }
        }

        private static int DeriveLaneCount(IEnumerable<ProjectedGameplayNote> notes)
        {
            bool lane2 = notes.Any(note => note.Lane == 2);
            bool lane3 = notes.Any(note => note.Lane == 3);
            if (!lane2 && !lane3) return 2;
            return lane3 ? 4 : 3;
        }

        private static void PlaceTechnikaNote(
            ProjectedGameplayNote note,
            int laneCount,
            int beatsPerScan,
            TechnikaScrollDirection scrollDirection)
        {
            double pulsesPerScan = PulsesPerBeat * Math.Max(1, beatsPerScan);
            double floatScan = note.Pulse / pulsesPerScan;
            int intScan = (int)Math.Floor(floatScan);
            if (note.EndOfScan &&
                note.Kind != GameplayPreviewNoteKind.Drag &&
                note.Pulse > 0 &&
                note.Pulse % pulsesPerScan == 0)
            {
                intScan--;
            }

            double relative = floatScan - intScan;
            bool top = (intScan & 1) == 1;
            bool rightward = TechnikaSweepRightward(top, scrollDirection);
            double travel = (TechnikaFieldRight - TechnikaFieldLeft) * relative;
            double laneHeight = (1.0 - 0.05 - 0.05) / laneCount;
            double localY = 0.05 + laneHeight * (note.Lane + 0.5);

            note.ScanIndex = intScan;
            note.RelativeScan = relative;
            note.IsTopHalf = top;
            note.X = rightward ? TechnikaFieldLeft + travel : TechnikaFieldRight - travel;
            note.Y = top ? localY / 2.0 : 0.5 + localY / 2.0;
        }
    }

    /// <summary>
    /// The arcade's scroll-direction effector. Notes keep their scan, lane and time under
    /// every direction; only which way the sweep crosses the field changes, which is why
    /// one enum feeds note placement, the scanline, hold bodies and the approach glow.
    /// </summary>
    public enum TechnikaScrollDirection
    {
        Clockwise,
        CounterClockwise,
        AllLeft,
        AllRight
    }
}
