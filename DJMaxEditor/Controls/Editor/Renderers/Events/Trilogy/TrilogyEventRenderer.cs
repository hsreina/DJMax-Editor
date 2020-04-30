using DJMaxEditor.DJMax;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace DJMaxEditor.Controls.Editor.Renderers.Events
{
    class TrilogyThemeRenderer : EventRenderer
    {
        public override string GetName()
        {
            return "Trilogy";
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
                        this.RenderNote(g, eventData, eventRectangle, centerX, centerY);
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
            Bitmap img;

            int x = centerX;
            int y = centerY;

            if (eventData.Attribute == (byte)EventAttribute.BasicNote)
            { // normal note

                // if the note have a more than 6 as duration, it could be a hold note
                if (eventData.Duration > 6)
                {
                    img = DJMRessources.longnoteline;
                    g.DrawImage(
                        img,
                        new Rectangle(x + eventData.VirtualDuration, y - 76 / 2, 25, 76),
                        0,
                        0,
                        28,
                        76,
                        GraphicsUnit.Pixel,
                        null
                    );

                    g.DrawImage(img,
                        new Rectangle(x, y - 76 / 2, eventData.VirtualDuration, 76),
                        0,
                        0,
                        2,
                        76,
                        GraphicsUnit.Pixel,
                        null
                    );

                    DrawImage(g, DJMRessources.longnote, x - (116 / 2), y - (116 / 2), 116, 116);
                }
                else
                {
                    DrawImage(g, DJMRessources.Note_Basic, x - (90 / 2), y - (90 / 2), 90, 90);
                }
            }

            else if (eventData.Attribute == (byte)EventAttribute.PressNote)
            { // note press start
                DrawImage(g, DJMRessources.notepressstart, x - (116 / 2), y - (116 / 2), 116, 116);

            }
            else if (eventData.Attribute == (byte)EventAttribute.PressNoteEnd)
            { // note press start end
                DrawImage(g, DJMRessources.notepressnote, x - (76 / 2), y - (76 / 2), 76, 76);
            }
            else if (eventData.Attribute == (byte)EventAttribute.RepeatNote)
            { // note repeat

                if (eventData.Duration > 6)
                {
                    img = DJMRessources.long_note_end;

                    g.DrawImage(img, new Rectangle(x + eventData.VirtualDuration, y - 90 / 2, 20, 90), 0, 0, 20, 90, GraphicsUnit.Pixel, null);
                    g.DrawImage(img, new Rectangle(x, y - 90 / 2, eventData.VirtualDuration, 90), 0, 0, 4, 90, GraphicsUnit.Pixel, null);
                }

                DrawImage(g, DJMRessources.noterepeat, x - (90 / 2), y - (90 / 2), 90, 90);
            }
            else if (eventData.Attribute == (byte)EventAttribute.RepeatNoteEnd)
            { // note repeat end
                DrawImage(g, DJMRessources.repeattail, x - (90 / 2), y - (90 / 2), 90, 90);
            }
            else if (eventData.Attribute == (byte)EventAttribute.LongHoldNote)
            { // long hold note

                img = DJMRessources.line_in_end;

                g.DrawImage(img, new Rectangle(x + eventData.VirtualDuration, y - 90 / 2, 20, 90), 0, 0, 20, 90, GraphicsUnit.Pixel, null);
                g.DrawImage(img, new Rectangle(x, y - 90 / 2, eventData.VirtualDuration, 90), 0, 0, 4, 90, GraphicsUnit.Pixel, null);

                DrawImage(g, DJMRessources.longnotehold, x - (90 / 2), y - (90 / 2), 90, 90);
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

        public override void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
            
        }

    }
}
