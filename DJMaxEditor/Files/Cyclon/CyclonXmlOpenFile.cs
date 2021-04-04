using DJMaxEditor.DJMax;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace DJMaxEditor.Files.Cyclon
{
    internal class CyclonXmlOpenFile : IOpenFile
    {
        public string GetDescription()
        {
            return "Cyclon xml pattern(*.xml)";
        }

        public string GetExtension()
        {
            return "xml";
        }

        public string GetName()
        {
            return "Cyclon xml pattern(*.xml)";
        }

        public Form GetSettingsForm()
        {
            return null;
        }

        public bool Open(string filename, out PlayerData playerData)
        {
            playerData = new PlayerData();

            var xmlRoot = DeserializeToObject<Root>(filename);

            var instruments = playerData.Instruments;
            var tracks = playerData.Tracks;

            var songInfo = xmlRoot.Header.SongInfo;
            double scale = 192.0 / (float)songInfo.Tpm;

            playerData.TickPerMinute = 192;
            playerData.Tempo = songInfo.Tempo;
            playerData.HeaderEndTick = System.Convert.ToUInt32(System.Math.Round(songInfo.EndTick * scale));
            playerData.TrackDuration = songInfo.Ms / 1000;  
            playerData.Version = 1;

            instruments.Add(new InstrumentData()
            {
                InsNum = 0,
                Name = "none",
            });

            for (ushort i = 0; i < songInfo.TrackCnt; i++) 
            {
                tracks.AddTrack(new TrackData(i));
            }

            foreach (var temp in xmlRoot.Tempo.List) 
            {
                float tempo = temp.Tempo;
                var track = tracks.SingleOrDefault(t => t.Idx == 0);

                EventData newEvent = new EventData()
                {
                    TrackId = 0,
                    Tick = (int)System.Math.Round(temp.Tick * scale),
                    EventType = EventType.Tempo,
                    Tempo = tempo,
                };

                track.AddEvent(newEvent);
            }

            foreach (var xmlInstrument in xmlRoot.Instruments.List) 
            {
                instruments.Add(new InstrumentData
                {
                    InsNum = xmlInstrument.Idx,
                    Name = xmlInstrument.Name,
                });
            }

            foreach (var xmlTrack in xmlRoot.NoteList.Tracks) 
            {
                var track = tracks.SingleOrDefault(t => t.Idx == xmlTrack.Idx);

                foreach (var xmlNote in xmlTrack.Notes) 
                {
                    InstrumentData inst =
                        playerData.Instruments.SingleOrDefault(ins => ins.InsNum == xmlNote.Ins);

                    EventData newEvent = new EventData()
                    {
                        TrackId = xmlTrack.Idx,
                        Tick = (int)System.Math.Round(xmlNote.Tick * scale),
                        EventType = EventType.Note,
                        Attribute = xmlNote.Attr,
                        Duration = (ushort)System.Math.Round(xmlNote.Dur * scale),
                        Instrument = inst,
                    };

                    track.AddEvent(newEvent);
                }
            }

            return true;
        }

        public T DeserializeToObject<T>(string filepath) where T : class
        {
            XmlSerializer ser = new XmlSerializer(typeof(T));
            using (StreamReader sr = new StreamReader(filepath))
            {
                return (T)ser.Deserialize(sr);
            }
        }
    }
}
