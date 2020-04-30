using System;
using System.Linq;
using System.IO;
using DJMaxEditor.Files;
using DJMaxEditor.DJMax;
using DJMaxEditor.Files.pt;
using System.Windows.Forms;

namespace DJMaxEditor
{
    internal class PTOpenFile : PTFile, IOpenFile
    {
        private enum OpenFileMode
        {
            Normal,
            OnlineDecrypt
        }

        private bool m_encrypted;

        public string WorkingDir { get; private set; }

        public string Filename { get; private set; }

        public PTOpenFile(bool encrypted = true)
        {
            m_encrypted = encrypted;
        }

        public bool Open(string filename, out PlayerData playerData)
        {

            bool res = _OpenFile2(filename, out playerData, OpenFileMode.Normal);

            if (res == false && m_encrypted)
            {
                return _OpenFile2(filename, out playerData, OpenFileMode.OnlineDecrypt);
            }

            return res;
        }

        private byte[] DecryptDataOnline(byte[] data)
        {
            byte[] res = null;
            TryDoStuffDataOnline(data, "decrypt", out res);
            return res;
        }

        private bool _OpenFile2(string filename, out PlayerData playerData, OpenFileMode mode)
        {

            UInt32 calcMinTick = ~0u;

            playerData = new PlayerData();

            byte[] buff = null;

            FileInfo fi = new FileInfo(filename);

            try
            {
                WorkingDir = fi.DirectoryName;
                Filename = fi.Name;
            }
            catch (Exception e)
            {
                Logs.Write("Failed to get working folder");
                return false;
            }

            buff = File.ReadAllBytes(filename);
            if (mode == OpenFileMode.OnlineDecrypt)
            {
                buff = DecryptDataOnline(buff);
            }

            if (buff == null) { return false; }

            ByteArrayStream stream = new ByteArrayStream(buff);

            string sign = stream.ReadString(4);

            if (sign != "PTFF")
            {
                return false;
            }

            byte version = stream.ReadByte();
            byte un = stream.ReadByte();
            UInt16 tickPerMinute = stream.ReadUShort();
            float tempo2 = stream.ReadFloat();
            UInt16 tracksCount = stream.ReadUShort();
            UInt32 headerEndTick = stream.ReadUInt();
            float trackDuration = stream.ReadFloat();

            Logs.Write(String.Format("tmp : {0}", tickPerMinute));
            Logs.Write(String.Format("tempo2 : {0}", tempo2));
            Logs.Write(String.Format("tracksCount : {0}", tracksCount));
            Logs.Write(String.Format("headerEndTick : {0}", headerEndTick));
            Logs.Write(String.Format("trackDuration : {0}", trackDuration));

            playerData.Version = version;
            playerData.TrackDuration = trackDuration;
            playerData.TickPerMinute = tickPerMinute;
            playerData.Tempo = tempo2;
            playerData.HeaderEndTick = headerEndTick;


            //Logs.Write("version : {0}", version);

            stream.Seek(0x16);
            ushort insCnt = stream.ReadUShort();

            stream.Seek(0x18);

            playerData.Clear();

            playerData.Instruments.Add(new InstrumentData()
            {
                InsNum = 0,
                Name = "none"
            });

            // read ogg instruments list
            for (int i = 0; i < insCnt; i++)
            {

                ushort insNo = 0;
                UInt16 unknown1 = 0;
                UInt16 unknown2 = 0;
                if (version == 1)
                {
                    insNo = stream.ReadUShort();
                    unknown1 = stream.ReadByte();
                    unknown2 = stream.ReadByte();
                }
                else
                {
                    insNo = stream.ReadByte();
                    unknown1 = stream.ReadByte();
                }

                if (insNo > 1000) 
                {
                    return false;
                }

                string oggName = stream.ReadString(0x40);

                Logs.Write("insNo : {0} - {1}; op: {2}, {3}", insNo, oggName, unknown1, unknown2);

                playerData.Instruments.Add(new InstrumentData()
                {
                    InsNum = insNo,
                    Name = oggName
                });
            }

            uint eventIndex = 0;
            uint trackIndex = 0;
            while (true)
            {

                if (stream.Available < 4)
                {
                    break;
                }

                uint eztr = stream.ReadUInt();
                if (eztr != EZTR)
                {
                    Logs.Write("invalid Magic");
                    return false;
                    break;
                };

                stream.Skip(0x02);

                string trackName = stream.ReadString(0x40);

                uint endTick = stream.ReadUInt();
                int blockSize = stream.ReadInt();

                if (version == 1)
                {
                    ushort un1 = stream.ReadUShort();
                }

                int eventSize = version == 1 ? 0x10 : 0x0B;

                int eventsCount = (int)(double)(blockSize / eventSize);

                TrackData track = new TrackData(trackIndex)
                {
                    TrackName = trackName
                };

                playerData.Tracks.AddTrack(track);

                for (int i = 0; i < eventsCount; i++)
                {

                    int tick = stream.ReadInt();
                    byte id = stream.ReadByte();

                    if (version == 1)
                    {
                        byte[] extraData = stream.ReadBytes(0xB);


                        switch ((EventType)id)
                        {
                            case EventType.None:
                                Logs.Write("NULL event");
                                break;
                            case EventType.Note:
                                {

                                    ushort insNo = BitConverter.ToUInt16(extraData, 3);
                                    byte vel = extraData[5];
                                    byte pan = extraData[6];

                                    byte attribute = extraData[7];
                                    ushort duration = BitConverter.ToUInt16(extraData, 8);

                                    /*
                                    Logs.Write(String.Format(
                                        "Note 0x0 : {0, 3}; 0x1: {1, 3}; 0x2: {2, 3}; insNo: {3, 10}; vel: {4, 3}; pan: {5, 3}; attribute: {6, 3}; duration: {7, 10}; 0xA: {8, 3}",
                                        extraData[0x0],
                                        extraData[0x1],
                                        extraData[0x2],

                                        insNo, // insNo

                                        extraData[0x5], // vel
                                        extraData[0x6], // pan
                                        extraData[0x7], // attribute

                                        duration, // duration

                                        extraData[0xA]
                                    ));*/


                                    InstrumentData inst =
                                        playerData.Instruments.SingleOrDefault(ins => ins.InsNum == insNo);

                                    EventData newEvent = new EventData()
                                    {
                                        TrackId = trackIndex,
                                        Tick = tick,
                                        Attribute = attribute,
                                        Duration = duration,
                                        EventType = EventType.Note,
                                        Instrument = inst,
                                        Vel = vel,
                                        Pan = pan
                                    };

                                    track.AddEvent(newEvent);

                                    eventIndex++;
                                    calcMinTick = (UInt32)Math.Min(calcMinTick, tick); // debug purpose

                                }
                                break;
                            case EventType.Volume:
                                {

                                    byte volume = extraData[3];

                                    EventData newEvent = new EventData()
                                    {
                                        TrackId = trackIndex,
                                        Tick = tick,
                                        EventType = EventType.Volume,
                                        Volume = volume
                                    };

                                    track.AddEvent(newEvent);

                                    eventIndex++;

                                }
                                break;
                            case EventType.Tempo:
                                {

                                    float tempo = BitConverter.ToSingle(extraData, 3);

                                    EventData newEvent = new EventData()
                                    {
                                        TrackId = trackIndex,
                                        Tick = tick,
                                        EventType = EventType.Tempo,
                                        Tempo = tempo
                                    };

                                    track.AddEvent(newEvent);

                                    eventIndex++;

                                }
                                break;
                            case EventType.Beat:
                                {

                                    ushort beat = BitConverter.ToUInt16(extraData, 3);

                                    EventData newEvent = new EventData()
                                    {
                                        TrackId = trackIndex,
                                        Tick = tick,
                                        EventType = EventType.Beat,
                                        Beat = beat
                                    };

                                    track.AddEvent(newEvent);

                                    eventIndex++;

                                }
                                break;
                            default:
                                Logs.Write("Unknow event : {0}", id);
                                break;
                        }


                    }
                    else
                    {

                        byte[] extraData = stream.ReadBytes(0x6);

                        switch ((EventType)id)
                        {
                            case EventType.None:
                                Logs.Write("NULL event");
                                break;
                            case EventType.Note:
                                {

                                    byte insNo = extraData[0];
                                    byte vel = extraData[1];
                                    byte pan = extraData[2];

                                    byte attribute = extraData[3];
                                    ushort duration = BitConverter.ToUInt16(extraData, 4);

                                    InstrumentData inst =
                                        playerData.Instruments.FirstOrDefault(ins => ins != null && ins.InsNum == insNo);

                                    EventData newEvent = new EventData()
                                    {
                                        TrackId = trackIndex,
                                        Tick = tick,
                                        Attribute = attribute,
                                        Duration = duration,
                                        EventType = EventType.Note,
                                        Instrument = inst,
                                        Vel = vel,
                                        Pan = pan
                                    };

                                    track.AddEvent(newEvent);

                                    eventIndex++;

                                }
                                break;

                            case EventType.Volume:
                                {

                                    byte volume = extraData[0];

                                    EventData newEvent = new EventData()
                                    {
                                        //TrackId = trackIndex,
                                        Tick = tick,
                                        EventType = EventType.Volume,
                                        Volume = volume
                                    };

                                    track.AddEvent(newEvent);

                                    eventIndex++;

                                }
                                break;

                            case EventType.Tempo:
                                {

                                    float tempo = BitConverter.ToSingle(extraData, 0);

                                    EventData newEvent = new EventData()
                                    {
                                        TrackId = trackIndex,
                                        Tick = tick,
                                        EventType = EventType.Tempo,
                                        Tempo = tempo
                                    };

                                    track.AddEvent(newEvent);

                                    eventIndex++;

                                }
                                break;
                            case EventType.Beat:
                                {

                                    ushort beat = BitConverter.ToUInt16(extraData, 0);

                                    EventData newEvent = new EventData()
                                    {
                                        TrackId = trackIndex,
                                        Tick = tick,
                                        EventType = EventType.Beat,
                                        Beat = beat
                                    };

                                    track.AddEvent(newEvent);

                                    eventIndex++;

                                }
                                break;

                            default:
                                Logs.Write("Unknow event : {0}", id);
                                break;

                        }
                    }
                }

                trackIndex++;
            }

            stream.Dispose();

            foreach (TrackData track in playerData.Tracks)
            {

                int minTick = 0, maxTick = 0;
                if (track.Events.Count() > 0)
                {
                    minTick = track.Events.Min(evnt => evnt.Tick);
                    maxTick = track.Events.Max(evnt => evnt.Tick);
                }

                int len = maxTick - minTick;

            }

            playerData.Encrypted = mode == OpenFileMode.OnlineDecrypt;

            return true;
        }

        public string GetName()
        {
            return "DJMax pattern";
        }

        public string GetDescription()
        {
            return "DJMax pattern";
        }

        public string GetExtension()
        {
            return "pt";
        }

        public Form GetSettingsForm()
        {
            return null;
        }
    }
}
