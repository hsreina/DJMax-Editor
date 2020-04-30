using DJMaxEditor.DJMax;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers.Events
{
    internal class NullThemeRenderer : EventRenderer
    {
        public NullThemeRenderer()
        {
            _stringFormat.Alignment = StringAlignment.Center;
            _stringFormat.LineAlignment = StringAlignment.Center;
        }

        public override void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
        }

        public override string GetName()
        {
            return "Default";
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
                        DrawImage(g, DJMRessources.unknowNote, eventRectangle.X, eventRectangle.Y, 90, 90);
                    }
                    break;
            }
        }

        public override void RenderNote(GraphicsWrapper g, EventData eventData, Rectangle eventRectangle, int centerX, int centerY)
        {
            int rectangleX = centerX - (RECTANGLE_WIDTH / 2);
            int rectangleY = centerY - (RECTANGLE_HEIGHT / 2);
            int rectangleWidth = RECTANGLE_WIDTH + (eventData.Duration > 6 ? eventData.VirtualDuration : 0);
            int rectangleHeight = RECTANGLE_HEIGHT;

            g.FillRectangle(CustomBrushes.NoteBackground, rectangleX, rectangleY, rectangleWidth, rectangleHeight);
            g.DrawRectangle(Pens.Black, rectangleX, rectangleY, rectangleWidth, rectangleHeight);

            string text = String.Empty;
            switch (EventDisplayMode)
            {
                case EventDisplayMode.Attribute:
                    byte attribute = eventData.Attribute;
                    text = String.Format("Attr {0,3:000}", attribute);
                    break;
                case EventDisplayMode.Instrument:
                    var instrument = eventData.Instrument;
                    int insNo = instrument != null ? instrument.InsNum : 0;
                    text = String.Format("Ins {0,3:000}", insNo);
                    break;
                case EventDisplayMode.Duration:
                    ushort duration = eventData.Duration;
                    text = String.Format("Dur {0,3:000}", duration);
                    break;
                case EventDisplayMode.Pan:
                    byte pan = eventData.Pan;
                    text = String.Format("Pan {0,3:000}", pan);
                    break;
                case EventDisplayMode.Velocity:
                    byte vel = eventData.Vel;
                    text = String.Format("Vel {0,3:000}", vel);
                    break;
            }


            g.DrawString(text, this.m_textFont, Brushes.Black, centerX + SHADOW_DISTANCE, centerY + SHADOW_DISTANCE, _stringFormat);
            g.DrawString(text, this.m_textFont, Brushes.White, centerX, centerY, _stringFormat);
        }

        private Font m_textFont = new Font("Tahoma", 20, FontStyle.Bold);

        private StringFormat _stringFormat = new StringFormat();

        private const int RECTANGLE_WIDTH = 118;

        private const int RECTANGLE_HEIGHT = 45;

        private const int SHADOW_DISTANCE = 3;
    }
}
