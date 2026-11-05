using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DJMaxEditor
{
    struct AudioPlayerFmodExDebugInfo
    {
        public float Dsp, Stream, Geometry, Update, Total;
    }

    class AudioPlayerFmodEx : IAudioPlayer
    {
        public AudioPlayerFmodEx()
        {
            m_updateTimer.Tick += UpdateTimer_Tick;
            // FMOD reclaims finished channels and manages virtual voices from update();
            // calling it rarely lets dead channels fill the channel pool.
            m_updateTimer.Interval = 10;
            m_updateTimer.Enabled = true;
            FMODEX.RESULT result = Initialize();
            ERRCHECK(result);
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (m_system != null)
            {
                m_system.update();
            }
        }

        public object GetDebugInfo()
        {
            AudioPlayerFmodExDebugInfo debugInfo = new AudioPlayerFmodExDebugInfo();
            m_system.getCPUUsage(ref debugInfo.Dsp, ref debugInfo.Stream, ref debugInfo.Geometry, ref debugInfo.Update, ref debugInfo.Total);
            return debugInfo;
        }

        // Sounds live in per-document tables so switching charts never reloads samples.
        private sealed class SoundTable
        {
            public readonly FMODEX.Sound[] Sounds = new FMODEX.Sound[MAX_SOUND];
            public bool FullyLoaded;
        }

        private readonly Dictionary<string, SoundTable> _soundTables =
            new Dictionary<string, SoundTable>(StringComparer.OrdinalIgnoreCase);

        private string _soundContextKey = string.Empty;

        private SoundTable CurrentTable
        {
            get
            {
                SoundTable table;
                if (!_soundTables.TryGetValue(_soundContextKey, out table))
                {
                    table = new SoundTable();
                    _soundTables[_soundContextKey] = table;
                }
                return table;
            }
        }

        public void SetSoundContext(string key)
        {
            _soundContextKey = key ?? string.Empty;
        }

        public bool IsSoundContextLoaded(string key)
        {
            SoundTable table;
            return _soundTables.TryGetValue(key ?? string.Empty, out table) && table.FullyLoaded;
        }

        public void MarkSoundContextLoaded(string key)
        {
            CurrentTable.FullyLoaded = true;
        }

        public void ReleaseSoundContext(string key)
        {
            SoundTable table;
            if (!_soundTables.TryGetValue(key ?? string.Empty, out table))
            {
                return;
            }
            foreach (FMODEX.Sound sound in table.Sounds)
            {
                if (sound != null)
                {
                    sound.release();
                }
            }
            _soundTables.Remove(key ?? string.Empty);
        }

        public bool LoadSound(uint index, string name, int mode = 0)
        {
            if (index >= MAX_SOUND)
            {
                return false;
            }

            FMODEX.Sound[] sounds = CurrentTable.Sounds;
            if (sounds[index] != null)
            {
                sounds[index].release();
                sounds[index] = null;
            }

            FMODEX.MODE fmodMode = mode == 0 ? FMODEX.MODE.CREATESAMPLE : FMODEX.MODE.CREATESTREAM;

            FMODEX.RESULT result =
                m_system.createSound(
                    name,
                    fmodMode |
                    FMODEX.MODE._2D |
                    FMODEX.MODE.SOFTWARE |
                    FMODEX.MODE.LOOP_OFF |
                    FMODEX.MODE.ACCURATETIME
                    /*| (FMODEX.MODE._2D | FMODEX.MODE.HARDWARE | FMODEX.MODE.CREATESTREAM)*/,
                    ref sounds[index]
                );
            return result == FMODEX.RESULT.OK;
        }

        public bool PauseSound(uint channelIndex)
        {
            channelIndex %= MAX_CHANNEL;

            if (channelIndex >= MAX_CHANNEL)
            {
                return false;
            }

            FMODEX.Channel channel = _channels[channelIndex];
            if (channel == null)
            {
                return false;
            }

            bool paused = false;
            FMODEX.RESULT iresult = channel.getPaused(ref paused);
            if (iresult == FMODEX.RESULT.OK)
            {
                channel.setPaused(!paused);
            }

            return iresult == FMODEX.RESULT.OK;
        }

        public bool StopSound(uint channelIndex)
        {
            channelIndex %= MAX_CHANNEL;

            if (channelIndex >= MAX_CHANNEL)
            {
                return false;
            }

            FMODEX.Channel channel = _channels[channelIndex];
            if (channel == null)
            {
                return false;
            }

            bool isPlaying = false;
            FMODEX.RESULT iresult = channel.isPlaying(ref isPlaying);
            if (iresult == FMODEX.RESULT.OK && isPlaying)
            {
                iresult = channel.stop();
            }

            return iresult == FMODEX.RESULT.OK;
        }

        public void StopAllSounds()
        {
            if (m_playbackGroup != null)
            {
                m_playbackGroup.stop();
                m_playbackGroup.setPaused(false);
            }
            // Auditioned sounds live outside the playback group; stop them explicitly.
            foreach (FMODEX.Channel channel in _channels)
            {
                if (channel != null)
                {
                    channel.stop();
                }
            }
            Array.Clear(_channels, 0, _channels.Length);
        }

        public bool PlaySound(uint channelIndex, uint soundIndex, float volume, byte pan, uint offsetMilliseconds = 0, bool audition = false)
        {
            channelIndex %= MAX_CHANNEL;

            if ((soundIndex >= MAX_SOUND) || (channelIndex >= MAX_CHANNEL))
            {
                return false;
            }

            FMODEX.Channel channel = _channels[channelIndex];

            if (channel != null)
            {
                bool isPlaying = false;
                FMODEX.RESULT iresult = channel.isPlaying(ref isPlaying);
                if (iresult == FMODEX.RESULT.OK && isPlaying)
                {
                    //channel.stop();
                    //channel.setPaused(true);
                }
            }

            if (CurrentTable.Sounds[soundIndex] == null)
            {
                return false;
            }

            // convert pan settings
            float fpan = 0;
            if (64 < (int)pan)
            {
                fpan = (float)(((double)pan - 64.0) / 63.0);
            }
            else
            {
                fpan = (float)(((double)pan - 64.0) / 64.0);
            }

            FMODEX.Channel chan = null;
            FMODEX.RESULT result = m_system.playSound(FMODEX.CHANNELINDEX.FREE, CurrentTable.Sounds[soundIndex], true, ref _channels[channelIndex]);
            if (result == FMODEX.RESULT.OK)
            {
                chan = _channels[channelIndex];
                // A paused group mutes its channels no matter the channel's own paused
                // state, so auditioned sounds stay out of the transport group.
                if (m_playbackGroup != null && !audition)
                {
                    chan.setChannelGroup(m_playbackGroup);
                }
                chan.setVolume(volume);
                chan.setPan(fpan);

                // Seek while still paused so the first audible buffer is already mid-sample
                // (BGM restore on mid-chart play).
                if (offsetMilliseconds != 0)
                {
                    chan.setPosition(offsetMilliseconds, FMODEX.TIMEUNIT.MS);
                }
                chan.setPaused(false);
            }

            return result == FMODEX.RESULT.OK;
        }

        public uint GetSoundLengthMilliseconds(uint soundIndex)
        {
            if (soundIndex >= MAX_SOUND)
            {
                return 0;
            }
            FMODEX.Sound sound = CurrentTable.Sounds[soundIndex];
            if (sound == null)
            {
                return 0;
            }
            uint length = 0;
            if (sound.getLength(ref length, FMODEX.TIMEUNIT.MS) != FMODEX.RESULT.OK)
            {
                return 0;
            }
            return length;
        }

        public bool SetVolume(uint channelIndex, float volume)
        {
            channelIndex %= MAX_CHANNEL;

            FMODEX.Channel channel = _channels[channelIndex];

            if (channel == null)
            {
                return false;
            }

            FMODEX.RESULT result = channel.setVolume(volume);

            return result == FMODEX.RESULT.OK;
        }

        public uint GetPosition(uint channelIndex)
        {
            channelIndex %= MAX_CHANNEL;

            uint result = 0;

            FMODEX.Channel channel = _channels[channelIndex];

            if (channel != null)
            {
                FMODEX.RESULT fmodResult =
                    channel.getPosition(ref result, FMODEX.TIMEUNIT.PCM);
            }

            return result;
        }

        public void PauseAllSounds()
        {
            if (m_playbackGroup == null)
            {
                return;
            }

            bool paused = false;
            if (m_playbackGroup.getPaused(ref paused) == FMODEX.RESULT.OK)
            {
                m_playbackGroup.setPaused(!paused);
            }
        }

        private Timer m_updateTimer = new Timer(); 

        private FMODEX.System m_system = null;

        private FMODEX.ChannelGroup m_playbackGroup = null;

        private FMODEX.Channel[] _channels = new FMODEX.Channel[MAX_CHANNEL];

        public const int MAX_CHANNEL = 100;

        public const int MAX_SOUND = 2000;

        private int n_numdrivers;

        private int m_controlpaneloutput;

        private FMODEX.SPEAKERMODE _speakermode;

        private FMODEX.CAPS m_caps;

        private void ERRCHECK(FMODEX.RESULT result)
        {
            if (result != FMODEX.RESULT.OK)
            {
                Logs.Write("FMOD error! " + result + " - " + FMODEX.Error.String(result));
                Environment.Exit(-1);
            }
        }

        private FMODEX.RESULT Initialize()
        {
            FMODEX.Factory.System_Create(ref m_system);

            FMODEX.RESULT result;

            if ((result = m_system.getNumDrivers(ref n_numdrivers)) != FMODEX.RESULT.OK)
            {
                return result;
            }

            if (n_numdrivers == 0)
            {
                if ((result = m_system.setOutput(FMODEX.OUTPUTTYPE.NOSOUND)) != FMODEX.RESULT.OK)
                {
                    return result;
                }
            }
            else
            {
                if ((result = m_system.getDriverCaps(0, ref m_caps, ref m_controlpaneloutput, ref _speakermode)) != FMODEX.RESULT.OK)
                {
                    return result;
                }

                if ((result = m_system.setSpeakerMode(_speakermode)) != FMODEX.RESULT.OK)
                    return result;

                if (m_caps == FMODEX.CAPS.HARDWARE_EMULATED)
                {
                    result = m_system.setDSPBufferSize(1024, 10);
                }
            }

            result = m_system.setDSPBufferSize(512, 4);

            m_system.setSoftwareFormat(44100, FMODEX.SOUND_FORMAT.PCMFLOAT, 0, 0, FMODEX.DSP_RESAMPLER.LINEAR);

            // Keysound-dense charts can keep dozens of samples sounding at once
            // (long cymbal/song tails), so 32 voices is not enough.
            result = m_system.init(256, FMODEX.INITFLAGS.NORMAL, (IntPtr)null);
            if (result != FMODEX.RESULT.OK)
            {
                return result;
            }

            return m_system.createChannelGroup("DJMax Editor playback", ref m_playbackGroup);
        }
    }
}
