using System.Collections.Generic;
using System.Xml.Serialization;

namespace DJMaxEditor.Files.Cyclon
{
    public class Header
    {
        [XmlElement(ElementName = "version")]
        public Version Version;

        [XmlElement(ElementName = "songinfo")]
        public SongInfo SongInfo;
    }

    public class SongInfo 
    {
        [XmlAttribute("tempo")]
        public long Tempo;

        [XmlAttribute("tpm")]
        public ushort Tpm;

        [XmlAttribute("start_tick")]
        public long StartTick;

        [XmlAttribute("end_tick")]
        public uint EndTick;

        [XmlAttribute("tick")]
        public long Tick;

        [XmlAttribute("ms")]
        public long Ms;

        [XmlAttribute("track_cnt")]
        public long TrackCnt;

        [XmlAttribute("tps")]
        public float Tps;
    }

    public class Instruments 
    {
        [XmlElement(ElementName = "ins")]
        public List<Ins> List { get; set; }
    }

    public class Ins 
    {
        [XmlAttribute("idx")]
        public ushort Idx;

        [XmlAttribute("name")]
        public string Name;
    }

    public class TempoDesc 
    {
        [XmlAttribute("tick")]
        public int Tick;

        [XmlAttribute("tempo")]
        public long Tempo;

        [XmlAttribute("tps")]
        public float Tps;
    }

    public class TempoList
    {
        [XmlElement(ElementName = "tempo")]
        public List<TempoDesc> List { get; set; }
    }


    public class Version 
    {
        [XmlAttribute("date")]
        public string Date;
    }

    public class Note 
    {
        [XmlAttribute("tick")]
        public int Tick;

        [XmlAttribute("ins")]
        public long Ins;

        [XmlAttribute("attr")]
        public byte Attr;

        [XmlAttribute("dur")]
        public ushort Dur;
    }

    public class Track 
    {
        [XmlAttribute("idx")]
        public uint Idx;

        [XmlElement(ElementName = "note")]
        public List<Note> Notes { get; set; }
    }

    public class NoteList 
    {
        [XmlElement(ElementName = "track")]
        public List<Track> Tracks { get; set; }
    }

    [XmlRoot("root", Namespace = "", IsNullable = false)]
    public class Root
    {
        [XmlElement(ElementName = "header")]
        public Header Header;

        [XmlElement(ElementName = "instrument")]
        public Instruments Instruments;

        [XmlElement(ElementName = "tempo")]
        public TempoList Tempo;        
        
        [XmlElement(ElementName = "note_list")]
        public NoteList NoteList;
    }
}
