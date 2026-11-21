using DJMaxEditor.DJMax;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers.Events
{
    internal class TechnikaThemeRenderer : EventRenderer
    {
        public override void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player1Visible, "P1 Visible", 0, 3);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, Brushes.Red, "P1 notes special", 4, 7);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player2Visible, "P2 Visible", 8, 11);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, Brushes.Azure, "P2 notes special", 12, 15);
        }

        public override string GetName()
        {
            return "Technika";
        }

        public override IEnumerable<KeyValuePair<string, EventData>> GetTemplates()
        {
            var list = new List<KeyValuePair<string, EventData>>();

            list.Add(this.CreateNoteFromEventData(
                "Basic note",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.BasicNote,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Basic note long",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.BasicNote,
                    Duration = 9,
                }
            ));

            /*
            list.Add(this.CreateNoteFromEventData(
                "Basic note long up flag",
                new EventData()
                {
                    EventType = PlayerData.EventType.Note,
                    Attribute = (byte)90,
                }
            ));
            */

            list.Add(this.CreateNoteFromEventData(
                "Repeat note",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.RepeatNote,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Repeat note end",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.RepeatNoteEnd,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Long hold note",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.LongHoldNote,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Press note",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.PressNote,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Press note end",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.PressNoteEnd,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Tempo",
                new EventData()
                {
                    EventType = EventType.Tempo,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Volume",
                new EventData()
                {
                    EventType = EventType.Volume,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Beat",
                new EventData()
                {
                    EventType = EventType.Beat,
                }
            ));

            list.Add(this.CreateNoteFromEventData(
                "Video start",
                new EventData()
                {
                    EventType = EventType.Note,
                    Attribute = (byte)EventAttribute.VideoStart,
                }
            ));

            return list;
        }

        public override void RenderEventData(GraphicsWrapper g, EventData eventData, Rectangle eventRectangle, int centerX, int centerY)
        {
            //g.DrawRectangle(Pens.Red, eventRectangle);

            switch (eventData.EventType)
            {
                case EventType.Note:
                    {
                        RenderNote(g, eventData, eventRectangle, centerX, centerY);
                    }
                    break;
                case EventType.Tempo:
                    {
                        DrawImage(g, DJMRessources.metronome, centerX - (90 / 2), centerY - (90 / 2), 90, 90);
                    }
                    break;
                case EventType.Volume:
                    {
                        DrawImage(g, DJMRessources.volume, centerX - (64 / 2), centerY - (64 / 2), 64, 64);
                    }
                    break;
                case EventType.Beat:
                    {
                        DrawImage(g, DJMRessources.beat, centerX - (90 / 2), centerY - (90 / 2), 90, 90);
                    }
                    break;
                default:
                    {
                        g.DrawImage(DJMRessources.unknowNote, eventRectangle, 0, 0, 90, 90, GraphicsUnit.Pixel, null);
                    }
                    break;
            }
        }

        public override void RenderNote(GraphicsWrapper g, EventData eventData, Rectangle eventRectangle, int centerX, int centerY)
        {
            int x = centerX;
            int y = centerY;

            if (eventData.Attribute == (byte)EventAttribute.BasicNote)
            { // normal note

                // if the note have a more than 6 as duration, it could be a hold note
                if (eventData.Duration > 6)
                {
                    // Same layout as Trilogy / arcade BasicNote: longnoteline stem + tip past
                    // the hold end. LongBody's full-frame D tip read as a dark square.
                    DrawDragBody(g, DJMRessources.longnoteline, x, y, eventData.VirtualDuration, 76);
                    DrawImageHd(g, TechnikaHdAssets.LongHead, x - (116 / 2), y - (116 / 2), 116, 116);
                }
                else if (_implicitChainNodes != null && _implicitChainNodes.Contains(eventData))
                {
                    // Legacy .pt left this tap untagged; preview promotes it to a chain node.
                    DrawImageHd(g, TechnikaHdAssets.PressEnd, x - (76 / 2), y - (76 / 2), 76, 76);
                }
                else
                {
                    DrawImageHd(g, TechnikaHdAssets.Basic, x - (90 / 2), y - (90 / 2), 90, 90);
                }
            }

            else if (eventData.Attribute == (byte)EventAttribute.PressNote)
            { // note press start
                DrawImageHd(g, TechnikaHdAssets.LongHead, x - (116 / 2), y - (116 / 2), 116, 116);
            }
            else if (eventData.Attribute == (byte)EventAttribute.PressNoteEnd)
            { // note press start end
                DrawImageHd(g, TechnikaHdAssets.PressEnd, x - (76 / 2), y - (76 / 2), 76, 76);
            }
            else if (eventData.Attribute == (byte)EventAttribute.RepeatNote)
            { // note repeat — series link is drawn by RenderSeriesLinks
                if (eventData.Duration > 6)
                {
                    // Held body only (RepeatHeadHold). The Head→End connector is the thin
                    // series pen, same role as preview's noterepeatline.
                    DrawSolidTrail(g, RepeatHoldTrailBrush, x, y, eventData.VirtualDuration, 90);
                }
                if (_intermediateRepeatHeads != null &&
                    _intermediateRepeatHeads.Contains(eventData))
                {
                    DrawImageHd(g, TechnikaHdAssets.RepeatTail, x - (90 / 2), y - (90 / 2), 90, 90);
                }
                else
                {
                    DrawImageHd(g, TechnikaHdAssets.RepeatHead, x - (90 / 2), y - (90 / 2), 90, 90);
                }
            }
            else if (eventData.Attribute == (byte)EventAttribute.RepeatNoteEnd)
            { // note repeat end
                if (eventData.Duration > 6)
                {
                    DrawSolidTrail(g, RepeatHoldTrailBrush, x, y, eventData.VirtualDuration, 90);
                }
                DrawImageHd(g, TechnikaHdAssets.RepeatTail, x - (90 / 2), y - (90 / 2), 90, 90);
            }
            else if (eventData.Attribute == (byte)EventAttribute.LongHoldNote)
            { // long hold note
                DrawSolidTrail(g, HoldTrailBrush, x, y, eventData.VirtualDuration, 90);
                DrawImageHd(g, TechnikaHdAssets.HoldHead, x - (90 / 2), y - (90 / 2), 90, 90);
            }
            else if (eventData.Attribute == (byte)EventAttribute.VideoStart)
            { // Video Start
                DrawImage(g, DJMRessources.videoStart, x - (90 / 2), y - (90 / 2), 90, 90);
            }
            else
            {
                DrawImage(g, DJMRessources.unknowNote, x - (90 / 2), y - (90 / 2), 90, 90);
            }
        }

        // Draws an image with its full source rectangle mapped to the destination,
        // so every pixel of the HD art feeds the zoom transform.
        private static void DrawImageHd(GraphicsWrapper g, Image image, int x, int y, int width, int height)
        {
            if (image == null)
            {
                return;
            }
            g.DrawImage(
                image,
                new Rectangle(x, y, width, height),
                0,
                0,
                image.Width,
                image.Height,
                GraphicsUnit.Pixel,
                null);
        }

        // Drag-note body: Trilogy/arcade longnoteline — 2px stem stretched for the hold,
        // rounded tip drawn past the end (dest 25×76 from source 28×76). Tip-past-end is
        // what keeps the authored right-edge alpha from reading as a hollow tail.
        private const int DragStemSourceWidth = 2;
        private const int DragTipDestWidth = 25;
        private const int DragTipSourceWidth = 28;

        private static void DrawDragBody(GraphicsWrapper g, Image body, int x, int y, int duration, int height)
        {
            if (body == null || duration <= 0)
            {
                return;
            }
            int half = height / 2;
            int frameH = Math.Min(height, body.Height);
            int stemW = Math.Min(DragStemSourceWidth, body.Width);
            int tipSrcW = Math.Min(DragTipSourceWidth, body.Width);
            g.DrawImage(
                body,
                new Rectangle(x, y - half, duration, height),
                0,
                0,
                stemW,
                frameH,
                GraphicsUnit.Pixel,
                null);
            g.DrawImage(
                body,
                new Rectangle(x + duration, y - half, DragTipDestWidth, height),
                0,
                0,
                tipSrcW,
                frameH,
                GraphicsUnit.Pixel,
                null);
        }

        // Hold bodies stay a soft filled tube; repeat/chain series connectors are pens so
        // they match the playfield's thin noterepeatline / chain run (≈18% of note size).
        private static readonly Brush HoldTrailBrush =
            new SolidBrush(Color.FromArgb(120, 45, 100, 235));

        private static readonly Brush RepeatHoldTrailBrush =
            new SolidBrush(Color.FromArgb(140, 155, 65, 225));

        private static readonly Pen RepeatSeriesPen =
            new Pen(Color.FromArgb(200, 175, 80, 235), 10f);

        private static readonly Pen PressTrailPen =
            new Pen(Color.FromArgb(180, 70, 190, 80), 10f);

        private static void DrawSolidTrail(GraphicsWrapper g, Brush brush, int x, int y, int duration, int height)
        {
            if (duration <= 0)
            {
                return;
            }
            // Preview's flat hold fallback is ~0.42 of the note frame - thin enough to read
            // as a tube, not a lane-filling block.
            int lineHeight = Math.Max(10, (height * 2) / 5);
            g.FillRectangle(brush, x, y - lineHeight / 2, duration, lineHeight);
        }

        /// <summary>
        /// Same-track Repeat bars, plus cross-lane Press/chain polylines in chart-time order.
        /// Rebuilds the implicit-chain and intermediate-repeat sets first so note glyphs
        /// painted later this frame match the playfield preview.
        /// </summary>
        public void RenderSeriesLinks(
            GraphicsWrapper g,
            TracksList tracks,
            Rectangle bounds)
        {
            _implicitChainNodes = TechnikaChainTopology.FindImplicitChainNodes(tracks);
            _intermediateRepeatHeads = TechnikaRepeatTopology.FindIntermediateHeads(tracks);

            if (g == null || tracks == null)
            {
                return;
            }

            foreach (TrackData track in tracks)
            {
                if (track == null || track.Events == null)
                {
                    continue;
                }

                int centerY = (int)track.Idx * EventsRenderer.VirtualTrackheight +
                    EventsRenderer.VirtualTrackheight / 2;
                RenderRepeatSeriesLinks(g, track, bounds, centerY);
            }

            RenderPressChainLinks(g, tracks, bounds);
        }

        /// <summary>
        /// Same-lane Repeat Head→…→End connector. Drawn as a thin pen stroke (preview's
        /// noterepeatline), not a filled block — held bodies are painted on the notes.
        /// </summary>
        private void RenderRepeatSeriesLinks(
            GraphicsWrapper g,
            TrackData track,
            Rectangle bounds,
            int centerY)
        {
            bool open = false;
            bool endSeen = false;
            int headX = 0;
            int endX = 0;

            foreach (EventData evt in track.Events)
            {
                if (evt == null || evt.EventType != EventType.Note)
                {
                    continue;
                }

                byte attr = evt.Attribute;
                if (attr == (byte)EventAttribute.RepeatNote)
                {
                    if (open && !endSeen)
                    {
                        endX = Math.Max(endX, MemberEndX(evt));
                    }
                    else
                    {
                        FlushRepeatSeriesLink(g, bounds, headX, endX, centerY, open);
                        open = true;
                        endSeen = false;
                        headX = evt.VirtualTick;
                        endX = MemberEndX(evt);
                    }
                }
                else if (attr == (byte)EventAttribute.RepeatNoteEnd)
                {
                    if (open)
                    {
                        endSeen = true;
                        endX = Math.Max(endX, MemberEndX(evt));
                    }
                }
                else if (open)
                {
                    FlushRepeatSeriesLink(g, bounds, headX, endX, centerY, open);
                    open = false;
                    endSeen = false;
                }
            }

            FlushRepeatSeriesLink(g, bounds, headX, endX, centerY, open);
        }

        private static void FlushRepeatSeriesLink(
            GraphicsWrapper g,
            Rectangle bounds,
            int headX,
            int endX,
            int centerY,
            bool open)
        {
            if (!open || endX <= headX)
            {
                return;
            }

            if (headX > bounds.Right || endX < bounds.Left)
            {
                return;
            }

            g.DrawLine(RepeatSeriesPen, headX, centerY, endX, centerY);
        }

        /// <summary>
        /// Press/chain paths cross lanes. Head first, then nodes by VirtualTick (then lane Y)
        /// — same pulse order DJMAX STUDIO uses for the zigzag path. Implicit Basic waypoints
        /// inside a span are included so the polyline matches Gameplay Preview.
        /// </summary>
        private void RenderPressChainLinks(
            GraphicsWrapper g,
            TracksList tracks,
            Rectangle bounds)
        {
            var members = new List<PressChainMember>(64);
            foreach (TrackData track in tracks)
            {
                if (track == null || track.Idx > 3 || track.Events == null)
                {
                    continue;
                }

                int centerY = (int)track.Idx * EventsRenderer.VirtualTrackheight +
                    EventsRenderer.VirtualTrackheight / 2;
                foreach (EventData evt in track.Events)
                {
                    if (evt == null || evt.EventType != EventType.Note)
                    {
                        continue;
                    }
                    bool explicitChain =
                        evt.Attribute == (byte)EventAttribute.PressNote ||
                        evt.Attribute == (byte)EventAttribute.PressNoteEnd;
                    bool implicitNode =
                        evt.Attribute == (byte)EventAttribute.BasicNote &&
                        evt.Duration <= 6 &&
                        _implicitChainNodes != null &&
                        _implicitChainNodes.Contains(evt);
                    if (!explicitChain && !implicitNode)
                    {
                        continue;
                    }

                    members.Add(new PressChainMember(evt, centerY));
                }
            }

            if (members.Count < 2)
            {
                return;
            }

            members.Sort(ComparePressChainMembers);

            int spanStart = -1;
            for (int i = 0; i < members.Count; i++)
            {
                PressChainMember item = members[i];
                bool isHead = item.Event.Attribute == (byte)EventAttribute.PressNote;
                if (isHead)
                {
                    if (spanStart >= 0)
                    {
                        DrawPressChainSpan(g, bounds, members, spanStart, i - 1);
                    }
                    spanStart = i;
                }
            }
            if (spanStart >= 0)
            {
                DrawPressChainSpan(g, bounds, members, spanStart, members.Count - 1);
            }
        }

        /// <summary>
        /// Basic notes promoted to chain waypoints for the current paint. Filled by
        /// <see cref="RenderSeriesLinks"/> before note glyphs are drawn.
        /// </summary>
        private HashSet<EventData> _implicitChainNodes;

        /// <summary>
        /// Legacy attr-10 waypoints demoted to repeat tails for the current paint.
        /// </summary>
        private HashSet<EventData> _intermediateRepeatHeads;

        private static void DrawPressChainSpan(
            GraphicsWrapper g,
            Rectangle bounds,
            List<PressChainMember> members,
            int start,
            int end)
        {
            if (end <= start)
            {
                return;
            }

            // members[] is already tick-sorted; keep that path (do not reorder by InsNum).
            for (int i = start + 1; i <= end; i++)
            {
                PressChainMember a = members[i - 1];
                PressChainMember b = members[i];
                int ax = a.Event.VirtualTick;
                int bx = b.Event.VirtualTick;
                if (Math.Max(ax, bx) < bounds.Left || Math.Min(ax, bx) > bounds.Right)
                {
                    continue;
                }
                g.DrawLine(PressTrailPen, ax, a.CenterY, bx, b.CenterY);
            }
        }

        private static int ComparePressChainMembers(PressChainMember a, PressChainMember b)
        {
            int tick = a.Event.VirtualTick.CompareTo(b.Event.VirtualTick);
            if (tick != 0)
            {
                return tick;
            }
            return a.CenterY.CompareTo(b.CenterY);
        }

        private struct PressChainMember
        {
            public PressChainMember(EventData evt, int centerY)
            {
                Event = evt;
                CenterY = centerY;
            }

            public readonly EventData Event;
            public readonly int CenterY;
        }
        private static int MemberEndX(EventData evt)
        {
            int end = evt.VirtualTick;
            if (evt.Duration > 6)
            {
                end += evt.VirtualDuration;
            }
            return end;
        }
    }
}
