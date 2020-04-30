using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DJMaxEditor.DJMax;
using DJMaxEditor.Files.bms;

namespace DJMaxEditor.Files
{
    /// <summary>
    /// Allows exporting to Be-Music Score Extended (BME) format.
    /// </summary>
    class BMESaveFile : ISaveFile
    {
        // Channel mappings
        const int CHANNEL_BGM = 1;       // All non-playable notes
        const int CHANNEL_TEMPO = 8;     // All tempo/bpm changes
        const int CHANNEL_BGA = 4;       // Background animation changes
        const int CHANNEL_PLAYABLE = 11; // (11-1Z)
        const int CHANNEL_LONGNOTE = 51; // (51-5Z)
        // BME uses hexatrigesimal system (BASE36) instead of decimal
        const String base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        
        private String songname = "songname";
        private String artist = "artist";
        private String genre = "genre";
        private int playableTrackFrom = 0;
        private int playableTrackTo = 3;

        private Dictionary<String, String> instrumentList;
        private Dictionary<Double, String> tempoList;
        private List<String>[] messageList;

        /// <summary>
        /// Set which source tracks should be mapped into playable tracks/lanes.
        /// All other notes will be mapped to background music channel.
        /// </summary>
        public void SetPlayableTracks(int from, int to)
        {
            this.playableTrackFrom = from;
            this.playableTrackTo = to;
        }

        /// <summary>
        /// Set the text that will be written to the header.
        /// </summary>
        public void SetSongDetails(String songname, String artist, String genre)
        {
            this.songname = songname;
            this.artist = artist;
            this.genre = genre;
        }


        /// <summary>
        /// Export currently loaded pattern to BME format.
        /// </summary>
        public bool Save(string filename, PlayerData playerData)
        {
            instrumentList = new Dictionary<String, String>();
            tempoList = new Dictionary<Double, String>();

            // Process the chart first
            PutTempoToList(playerData.Tempo);
            foreach (InstrumentData instrument in playerData.Instruments)
            {   // Silent notes exist in DJMAX, so we take them into account by pointing them to a silent intrument
                instrumentList.Add(ToBase36(instrument.InsNum), instrument.Name.Equals("none") ? "none.wav" : instrument.Name);
            }
            ProcessTracks(playerData);

            // Write everything to file
            using (StreamWriter file = new StreamWriter(@filename))
            {
                WriteHeader(file, playerData.Tempo);
                file.WriteLine();
                WriteInstruments(file);
                file.WriteLine();
                WriteTempos(file);
                file.WriteLine();
                WriteMessages(file);
            }

            return true;
        }

        /// <summary>
        /// Write header to file.
        /// </summary>
        private void WriteHeader(StreamWriter file, double tempo)
        {
            file.WriteLine("*---------------------- Exported by DJMax Editor by HSReina");
            file.WriteLine("*---------------------- BME plugin by Drkie");
            file.WriteLine("*---------------------- Generated on " + DateTime.Now.ToString("MM/dd/yyyy h:mm tt"));
            file.WriteLine();
            file.WriteLine("*---------------------- HEADER FIELD");
            file.WriteLine("#PLAYER 1");
            file.WriteLine("#GENRE genre");
            file.WriteLine("#TITLE songname");
            file.WriteLine("#ARTIST artist");
            file.WriteLine("#BPM " + tempo);
            file.WriteLine("#PLAYLEVEL 1");
            file.WriteLine("#RANK 1");
            file.WriteLine("#TOTAL 100");
            file.WriteLine("#VOLWAV 100");
            file.WriteLine("#STAGEFILE title.bmp");
        }

        /// <summary>
        /// Write BPMs to file if necessary.
        /// </summary>
        private void WriteInstruments(StreamWriter file)
        {
            file.WriteLine("*---------------------- WAV FIELD");
            foreach (String id in instrumentList.Keys)
                file.WriteLine("#WAV" + id + " " + instrumentList[id]);
        }

        /// <summary>
        /// Write BPMs to file if necessary.
        /// </summary>
        private void WriteTempos(StreamWriter file)
        {
            if (tempoList.Count < 2) return;
            file.WriteLine("*---------------------- BPM FIELD");
            foreach (Double id in tempoList.Keys)
                file.WriteLine("#BPM" + tempoList[id] + " " + id);
        }

        /// <summary>
        /// Write all messages to file.
        /// </summary>
        private void WriteMessages(StreamWriter file)
        {
            file.WriteLine("*---------------------- MAIN DATA FIELD");
            for (int measure = 0; measure < messageList.Length; measure++)
            {
                String measureId = ZeroLPad(measure, 3);
                foreach (String message in messageList[measure])
                {
                    file.WriteLine("#" + measureId + message);
                }
                file.WriteLine();
            }
        }
        
        /// <summary>
        /// Decompose each track into measures and convert each measure and its events into messages.
        /// </summary>
        private void ProcessTracks(PlayerData playerData)
        {
            // Store special events separately
            IList<EventData> eventsTempo = new List<EventData>(); 
            IList<EventData> eventsLongnote = new List<EventData>();

            // Separate events by measure
            uint totalMeasures = (uint)((playerData.HeaderEndTick / playerData.TickPerMinute) + 1);
            messageList = new List<String>[totalMeasures];
            for (int measure = 0; measure < totalMeasures; measure++) messageList[measure] = new List<String>();

            foreach (TrackData track in playerData.Tracks)
            {
                Boolean playable = track.Idx >= playableTrackFrom && track.Idx <= playableTrackTo;
                int channel = playable ? CHANNEL_PLAYABLE + ((int)track.Idx - playableTrackFrom) : CHANNEL_BGM;

                List<EventData>[] eventsByMeasure = new List<EventData>[totalMeasures];
                for (int i = 0; i < eventsByMeasure.Length; i++)
                    eventsByMeasure[i] = new List<EventData>();

                foreach (EventData ev in track.Events)
                {
                    int measure = ev.Tick / playerData.TickPerMinute;
                    switch (ev.EventType)
                    {
                        case EventType.Note:
                            if (playable)
                            {
                                if (ev.Duration <= 6)
                                {
                                    eventsByMeasure[measure].Add(ev);
                                }
                                else
                                {   // For long notes, create another note to mark the end of it
                                    eventsLongnote.Add(ev);
                                    EventData noteEnd = new EventData
                                    {
                                        Tick = ev.Tick + ev.Duration,
                                        Instrument = ev.Instrument,
                                        EventType = EventType.Note
                                    };
                                    eventsLongnote.Add(noteEnd);
                                }
                            }
                            else if (ev.Instrument != null && ev.Instrument.InsNum > 0)
                            {   // Don't process silent notes if they aren't playable
                                eventsByMeasure[measure].Add(ev);
                            }
                            break;
                        case EventType.Tempo:
                            eventsTempo.Add(ev);
                            PutTempoToList(ev.Tempo);
                            break;
                    }
                }

                // Convert all notes in this track into messages
                for (int measure = 0; measure < totalMeasures; measure++)
                {
                    if (eventsByMeasure[measure].Any())
                    {
                        String message = EncodeMeasure(eventsByMeasure[measure], measure, channel, playerData);
                        messageList[measure].Add(message);
                    }
                }

                // Process Long notes
                foreach (EventData ev in eventsLongnote)
                {
                    int measure = ev.Tick / playerData.TickPerMinute;
                    String message = EncodeMeasure(new List<EventData>() { ev }, measure, CHANNEL_LONGNOTE + ((int)track.Idx - playableTrackFrom), playerData);
                    messageList[measure].Add(message);
                }
                eventsLongnote.Clear();
            }

            // Process Tempo changes
            if (tempoList.Keys.Count > 1)
            {
                foreach (EventData ev in eventsTempo)
                {
                    int measure = ev.Tick / playerData.TickPerMinute;
                    String message = EncodeMeasure(new List<EventData>() { ev }, measure, CHANNEL_TEMPO, playerData);
                    messageList[measure].Add(message);
                }
            }
        }

        /// <summary>
        /// Encode a list of events from the same measure into a message.
        /// </summary>
        private String EncodeMeasure(IList<EventData> events, int measure, int channel, PlayerData playerData)
        {
            int tickFrom = playerData.TickPerMinute * measure;
            int tickTo = playerData.TickPerMinute * (measure + 1); // Unused, but let's not delete it
            
            // Determine how many objects in the message and what goes in each object
            double messageLength = FindTimeSignature(events, playerData, tickFrom);
            String[] messageParts = new String[(int)messageLength];
            int beat = tickFrom; // The beat we'll start searching for notes from
            int offset = (int)(playerData.TickPerMinute / messageLength); // How many ticks we'll jump each time
            for (int i = 0; i < messageLength; i++)
            {
                EventData foundEvent = null;
                foreach (EventData ev in events)
                {
                    if (ev.Tick == beat)
                    {
                        foundEvent = ev;
                        break;
                    }
                }
                if (foundEvent != null)
                {
                    switch (foundEvent.EventType)
                    {
                        case EventType.Note:
                            messageParts[i] = ToBase36(foundEvent.Instrument.InsNum);
                            break;
                        case EventType.Tempo:
                            messageParts[i] = tempoList[foundEvent.Tempo];
                            break;
                    }
                    
                }
                else
                {
                    messageParts[i] = "00";
                }
                beat += offset;
            }

            // Create message
            String message = ZeroLPad(channel, 2) + ":";
            foreach (String s in messageParts) message += s;
            return message;
        }

        /// <summary>
        /// Converts numbers to 2-digit BASE36 (max 1296)
        /// </summary>
        private String ToBase36(int dec)
        {
            int idx1 = dec / base36.Length;
            int idx2 = dec % base36.Length;
            return "" + base36[idx1] + base36[idx2];
        }

        /// <summary>
        /// Adds left padding with zeroes to a number.
        /// </summary>
        private String ZeroLPad(int number, int padding)
        {
            String result = number.ToString();
            while (result.Length < padding)
                result = "0" + result;
            return result;
        }

        /// <summary>
        /// Add tempo to the list of all different tempos this song can have.
        /// </summary>
        private void PutTempoToList(double tempo)
        {
            if (!tempoList.ContainsKey(tempo))
                tempoList.Add(tempo, ToBase36(tempoList.Count + 1));
        }

        /// <summary>
        /// Find the time signature of a measure to determine the least number of divisions needed to store
        /// all existing events in a single message.
        /// </summary>
        private double FindTimeSignature(IList<EventData> events, PlayerData playerData, int tickFrom)
        {
            int division = 0;
            double segments = playerData.TickPerMinute / Math.Pow(2, division);
            foreach (EventData ev in events)
            {
                while ((ev.Tick - tickFrom) % segments != 0)
                {
                    division++;
                    segments = playerData.TickPerMinute / Math.Pow(2, division);
                    if (segments < 1)
                        segments = 1;
                }
            }
            return playerData.TickPerMinute / segments;
        }

        public string GetName()
        {
            return "bme";
        }

        public string GetDescription()
        {
            return "Be-Music Score";
        }

        public string GetExtension()
        {
            return "bme";
        }

        public Form GetSettingsForm()
        {
            return m_saveSettingsDialog;
        }

        private readonly SaveSettingsDialog m_saveSettingsDialog = new SaveSettingsDialog();
    }    
}
