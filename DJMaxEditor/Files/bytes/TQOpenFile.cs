using DJMaxEditor.DJMax;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DJMaxEditor.Files.bytes
{
    internal class TQOpenFile : IOpenFile
    {
        public string GetDescription()
        {
            return "Technika Q pattern";
        }

        public string GetExtension()
        {
            return "bytes";
        }

        public string GetName()
        {
            return "Technika Q pattern";
        }

        private OpenSettingsDialog m_settingsDialog = new OpenSettingsDialog();

        public Form GetSettingsForm()
        {
            return m_settingsDialog;
        }

        public bool Open(string filename, out PlayerData playerData)
        {
            playerData = null;

            if (String.IsNullOrEmpty(filename))
            {
                return false;
            }

            using (var stream = new ByteArrayStream(File.ReadAllBytes(filename)))
            {
                return OpenFromByteArrayStream(stream, out playerData);
            }
        }

        private bool OpenFromByteArrayStream(ByteArrayStream stream, out PlayerData playerData)
        {
            playerData = null;

            if (stream == null)
            {
                return false;
            }

            playerData = new PlayerData();

            var instruments = playerData.Instruments;
            var tracks = playerData.Tracks;
            var sign = stream.ReadUInt(); // zero??
            var num = stream.ReadUInt();
            var pos = stream.ReadUInt();

            stream.Seek(num);

            var insCnt = stream.ReadUShort();
            var trackCnt = (byte)stream.ReadUShort();
            var tickPerMinute = stream.ReadUShort();
            var tempo = stream.ReadFloat();
            var tick1 = stream.ReadUInt();
            playerData.HeaderEndTick = tick1;
            var playTime = stream.ReadFloat();
            var endTick = stream.ReadUInt();
            stream.ReadUInt();

            playerData.Encrypted = true;
            playerData.Version = 1;
            playerData.TrackDuration = playTime;
            playerData.TickPerMinute = tickPerMinute;
            playerData.Tempo = tempo;
            playerData.HeaderEndTick = endTick;

            stream.Seek(8);

            instruments.Add(new InstrumentData()
            {
                InsNum = 0,
                Name = "none"
            });

            for (int i = 0; i < insCnt; i++)
            {
                ushort insNo = stream.ReadUShort();
                stream.Skip(1);
                string oggName = stream.ReadString(0x40);

                if (m_settingsDialog.RenameInst)
                {
                    oggName = oggName.Replace(".wav", ".ogg");
                }

                instruments.Add(new InstrumentData()
                {
                    InsNum = insNo,
                    Name = oggName,
                });
            }

            for (byte trackIdx = 0; (int)trackIdx < (int)trackCnt; ++trackIdx)
            {
                stream.Skip(2);
                var trackName = stream.ReadString(0x40);
                stream.Skip(4);

                var track = new TrackData(trackIdx)
                {
                    TrackName = trackName,
                };

                tracks.AddTrack(track);

                var eventsCount = (ushort)stream.ReadUInt();
                for (ushort index = 0; (int)index < (int)eventsCount; ++index)
                {
                    uint tick = stream.ReadUInt();
                    byte eventCode = stream.ReadByte();

                    switch (eventCode)
                    {
                        case 1:
                            { // Note
                                var insNo = stream.ReadUShort();
                                var vel = stream.ReadByte();
                                var pan = stream.ReadByte();
                                var attribute = stream.ReadByte();
                                var duration = stream.ReadUShort();
                                stream.Skip(1);


                                InstrumentData inst =
                                    playerData.Instruments.SingleOrDefault(ins => ins.InsNum == insNo);

                                EventData newEvent = new EventData()
                                {
                                    TrackId = trackIdx,
                                    Tick = (int)tick,
                                    Attribute = attribute,
                                    Duration = duration,
                                    EventType = EventType.Note,
                                    Instrument = inst,
                                    Vel = vel,
                                    Pan = pan
                                };

                                track.AddEvent(newEvent);
                            }
                            break;
                        case 2:
                            {
                                var volume = stream.ReadByte();
                                stream.Skip(7);

                                var newEvent = new EventData()
                                {
                                    TrackId = trackIdx,
                                    Tick = (int)tick,
                                    EventType = EventType.Volume,
                                    Volume = volume
                                };

                                track.AddEvent(newEvent);
                            }
                            break;
                        case 3:
                            {
                                var tempo2 = stream.ReadFloat();
                                stream.Skip(4);

                                var newEvent = new EventData()
                                {
                                    TrackId = trackIdx,
                                    Tick = (int)tick,
                                    EventType = EventType.Tempo,
                                    Tempo = tempo2
                                };

                                track.AddEvent(newEvent);
                            }
                            break;
                        case 4:
                            stream.Skip(8);
                            break;
                        default:
                            throw new Exception("Failed to read op");
                    }
                }
            }

            return true;
        }
    }
}
