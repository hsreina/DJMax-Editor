using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.Files.pt
{
    internal class PTSaveFile : PTFile, ISaveFile
    {
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
            return m_settingsDialog;
        }

        public bool Save(string filename, PlayerData playerData)
        {
            ByteArrayStream stream = new ByteArrayStream();
            stream.WriteString("PTFF", 4);

            byte version = playerData.Version;

            stream.WriteByte(version);

            stream.WriteByte(0); // ??

            stream.WriteUShort(playerData.TickPerMinute);
            stream.WriteFloat(playerData.Tempo);

            UInt16 trackCount = playerData.Tracks.Count;
            stream.WriteUShort(trackCount);
            stream.writeUInt(playerData.HeaderEndTick);
            stream.WriteFloat(playerData.TrackDuration);

            ushort insCount = (ushort)(playerData.Instruments.Count - 1);

            stream.WriteUShort(insCount);

            for (int i = 0, l = playerData.Instruments.Count; i < l; i++)
            {

                InstrumentData ins = playerData.Instruments[i];
                if (ins != null && ins.InsNum != 0)
                {
                    if (version == 1)
                    {
                        stream.WriteUShort(ins.InsNum);
                        stream.WriteUShort(0);
                    }
                    else
                    {
                        stream.WriteByte((byte)ins.InsNum);
                        stream.WriteByte(0);
                    }

                    stream.WriteString(ins.Name, 0x40);
                }

            }

            int eventSize = version == 1 ? 0x10 : 0x0B;

            foreach (TrackData track in playerData.Tracks)
            {
                stream.writeUInt(EZTR);

                int eventsCount = track.Events.Count();
                int endTick = 0;
                if (eventsCount > 0)
                {
                    endTick = track.Events.Max(evnt => evnt.Tick);
                }

                uint blockSize = (uint)(eventsCount * eventSize);

                stream.WriteString("", 0x02); // unknown

                stream.WriteString(track.TrackName, 0x40);

                stream.writeInt(endTick);
                stream.writeUInt(blockSize);

                if (version == 1)
                {
                    stream.WriteUShort(0); // Unknown
                }

                foreach (EventData evnt in track.Events)
                {

                    stream.writeInt(evnt.Tick);
                    stream.WriteByte((byte)evnt.EventType);

                    switch (evnt.EventType)
                    {
                        case EventType.Volume:
                            {
                                if (version == 1)
                                {
                                    stream.WriteByte(0);
                                    stream.WriteByte(0);
                                    stream.WriteByte(0);
                                }

                                stream.WriteByte(evnt.Volume);
                                stream.WriteByte(0);

                                stream.WriteByte(0);
                                stream.WriteByte(0);

                                stream.WriteByte(0);
                                stream.WriteByte(0);

                                if (version == 1)
                                {
                                    stream.WriteByte(0);
                                    stream.WriteByte(0);
                                }
                            }
                            break;
                        case EventType.Note:
                            {

                                if (version == 1)
                                {
                                    stream.WriteByte(0);
                                    stream.WriteByte(0);
                                    stream.WriteByte(0);
                                }

                                ushort insno = 0;

                                if (evnt.Instrument != null)
                                {
                                    insno = evnt.Instrument.InsNum;
                                }

                                if (version == 1)
                                {
                                    stream.WriteUShort(insno);
                                }
                                else
                                {
                                    stream.WriteByte((byte)insno);
                                }

                                stream.WriteByte(evnt.Vel);
                                stream.WriteByte(evnt.Pan);

                                stream.WriteByte(evnt.Attribute);
                                stream.WriteUShort(evnt.Duration);

                                if (version == 1)
                                {
                                    stream.WriteByte(0);
                                }
                            }
                            break;
                        case EventType.Tempo:

                            if (version == 1)
                            {
                                stream.WriteByte(0);
                                stream.WriteByte(0);
                                stream.WriteByte(0);
                            }

                            stream.Write(BitConverter.GetBytes(evnt.Tempo));

                            stream.WriteByte(0);
                            stream.WriteByte(0);

                            if (version == 1)
                            {
                                stream.WriteByte(0);
                                stream.WriteByte(0);
                            }

                            break;
                        case EventType.Beat:

                            if (version == 1)
                            {
                                stream.WriteByte(0);
                                stream.WriteByte(0);
                                stream.WriteByte(0);
                            }

                            stream.WriteUShort(evnt.Beat);

                            stream.WriteByte(0);
                            stream.WriteByte(0);

                            stream.WriteByte(0);
                            stream.WriteByte(0);

                            if (version == 1)
                            {
                                stream.WriteByte(0);
                                stream.WriteByte(0);
                            }

                            break;
                        default:
                            {
                            }
                            break;
                    }
                }
            }

            byte[] ptffData = stream.GetData();

            int sizeIn = ptffData.Count();

            if (m_settingsDialog.EncryptFile/*playerData.Encrypted*/)
            {
                ptffData = EncryptDataOnline(ptffData);
                if (ptffData == null || sizeIn != ptffData.Count())
                {
                    return false;
                }
            }

            File.WriteAllBytes(filename, ptffData);

            return true;
        }

        private byte[] EncryptDataOnline(byte[] data)
        {
            byte[] result = null;
            TryDoStuffDataOnline(data, "encrypt", out result);
            return result;
        }

        private SaveSettingsDialog m_settingsDialog = new SaveSettingsDialog();
    }
}
