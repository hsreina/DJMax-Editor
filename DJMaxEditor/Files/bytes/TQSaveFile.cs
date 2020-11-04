using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.Files.bytes
{
    internal class TQSaveFile : ISaveFile
    {
        public string GetName()
        {
            return "Technika Q pattern(*.bytes)";
        }

        public string GetDescription()
        {
            return "Technika Q pattern(*.bytes)";
        }

        public string GetExtension()
        {
            return "bytes";
        }

        public Form GetSettingsForm()
        {
            return null;
        }

        public bool Save(string filename, PlayerData playerData)
        {
            using (FileStream ofs = new FileStream(filename, FileMode.Create))
            using (MemoryStream track = new MemoryStream(100))
            using (MemoryStream sounds = new MemoryStream(100))
            using (MemoryStream info = new MemoryStream(100))
            {
                // Write info
                info.Seek(0x0, SeekOrigin.Begin);
                info.Write(BitConverter.GetBytes(playerData.Instruments.Count - 1), 0, 2);
                info.Write(BitConverter.GetBytes(playerData.Tracks.Count), 0, 2);
                info.Write(BitConverter.GetBytes(playerData.TickPerMinute), 0, 2);
                info.Write(BitConverter.GetBytes(playerData.Tempo), 0, 4);
                info.Write(BitConverter.GetBytes(playerData.HeaderEndTick), 0, 4);
                info.Write(BitConverter.GetBytes(playerData.TrackDuration), 0, 4);
                info.Write(BitConverter.GetBytes(playerData.HeaderEndTick), 0, 4);

                // Write sound table
                for (int i = 0, l = playerData.Instruments.Count; i < l; i++)
                {

                    InstrumentData ins = playerData.Instruments[i];
                    if (ins != null && ins.InsNum != 0)
                    {
                        sounds.Write(BitConverter.GetBytes(ins.InsNum), 0, 2);
                        sounds.Write(new byte[] { 0x0 }, 0, 1);

                        byte[] fileNameBytes = new byte[0x40];
                        byte[] fileNameTemp = Encoding.ASCII.GetBytes(ins.Name);
                        if (fileNameTemp.Length > 0x40)
                        {
                            Console.WriteLine("Warning: File name too long: " + ins.Name);
                            return false;
                        }
                        for (int j = 0; j < fileNameTemp.Length && j < 0x80; j++)
                        {
                            fileNameBytes[j] = fileNameTemp[j];
                        }
                        sounds.Write(fileNameBytes, 0, 0x40);
                    }

                }

                // Write track
                foreach (TrackData td in playerData.Tracks)
                {
                    // Write track header and name
                    track.Write(new byte[] { 0, 0 }, 0, 2);
                    byte[] emptyName = Enumerable.Repeat((byte)0x0, 0x3B).ToArray();
                    track.Write(emptyName, 0, 0x3B);

                    track.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                    track.Write(new byte[] { 0 }, 0, 1);

                    int eventsCount = td.Events.Count();
                    track.Write(BitConverter.GetBytes(eventsCount << 4), 0, 4);
                    track.Write(BitConverter.GetBytes(eventsCount), 0, 4);


                    foreach (EventData evnt in td.Events)
                    {
                        track.Write(BitConverter.GetBytes(evnt.Tick), 0, 4);

                        switch (evnt.EventType)
                        {
                            case EventType.Volume:
                                {
                                    track.Write(new byte[] { 2 }, 0, 1);
                                    track.Write(BitConverter.GetBytes(evnt.Volume), 0, 1);
                                    track.Write(BitConverter.GetBytes(0), 0, 1);
                                    track.Write(BitConverter.GetBytes(0), 0, 1);
                                    track.Write(BitConverter.GetBytes(0), 0, 1);
                                    track.Write(BitConverter.GetBytes(0), 0, 4);
                                }
                                break;
                            case EventType.Note:
                                {
                                    track.Write(new byte[] { 1 }, 0, 1);

                                    ushort insno = 0;

                                    if (evnt.Instrument != null)
                                    {
                                        insno = evnt.Instrument.InsNum;
                                    }

                                    track.Write(BitConverter.GetBytes(insno), 0, 2);
                                    track.Write(BitConverter.GetBytes(evnt.Vel), 0, 1);
                                    track.Write(BitConverter.GetBytes(evnt.Pan), 0, 1);
                                    track.Write(BitConverter.GetBytes(evnt.Attribute), 0, 1);
                                    track.Write(BitConverter.GetBytes(evnt.Duration), 0, 2); // Is Duration 2 bytes? or 1 bytes
                                    track.Write(BitConverter.GetBytes(0), 0, 1);

                                }
                                break;
                            case EventType.Tempo:

                                track.Write(new byte[] { 3 }, 0, 1);

                                track.Write(BitConverter.GetBytes(evnt.Tempo), 0, 4);
                                track.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);

                                break;
                            default:
                                {
                                }
                                break;
                        }
                    }
                }

                ofs.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                // Write music table
                ofs.Seek(0x8, SeekOrigin.Begin);
                sounds.Seek(0x0, SeekOrigin.Begin);
                sounds.CopyTo(ofs);

                // Write tracks
                track.Seek(0, SeekOrigin.Begin);
                track.CopyTo(ofs);

                // Write Info Offset
                long infoOffset = ofs.Position;
                // Write Info
                info.Seek(0, SeekOrigin.End);
                while (info.Length < 0x1A)
                {
                    info.WriteByte(0x0);
                }
                info.Seek(0, SeekOrigin.Begin);
                info.CopyTo(ofs);

                ofs.Seek(0x4, SeekOrigin.Begin);
                ofs.Write(BitConverter.GetBytes(infoOffset), 0, 4);
            }

            return true;
        }

    }
}
