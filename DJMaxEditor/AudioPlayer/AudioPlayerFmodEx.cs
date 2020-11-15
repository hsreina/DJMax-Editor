using System;
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
            m_updateTimer.Interval = 1000;
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

        public bool LoadSound(uint index, string name, int mode = 0)
        {
            if (index >= MAX_SOUND)
            {
                return false;
            }

            if (m_sounds[index] != null)
            {
                m_sounds[index].release();
                m_sounds[index] = null;
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
                    ref m_sounds[index]
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
            for (uint i = 0; i < MAX_CHANNEL; i++)
            {
                StopSound(i);
            }

        }

        public bool PlaySound(uint channelIndex, uint soundIndex, float volume, byte pan, uint offset = 0)
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

            if (m_sounds[soundIndex] == null)
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
            FMODEX.RESULT result = m_system.playSound(FMODEX.CHANNELINDEX.FREE, m_sounds[soundIndex], true, ref _channels[channelIndex]);
            if (result == FMODEX.RESULT.OK)
            {
                chan = _channels[channelIndex];
                chan.setVolume(volume);
                chan.setPan(fpan);
                chan.setPaused(false);

                if (offset != 0)
                {
                    Logs.Write("Seek sound {0} to position {1} ms", soundIndex, offset);
                    chan.setPosition(offset, FMODEX.TIMEUNIT.PCM);
                }
            }

            return result == FMODEX.RESULT.OK;
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
            for (uint i = 0; i < MAX_CHANNEL; i++)
            {
                PauseSound(i);
            }
        }

        private Timer m_updateTimer = new Timer(); 

        private FMODEX.System m_system = null;

        private FMODEX.Channel[] _channels = new FMODEX.Channel[MAX_CHANNEL];

        private FMODEX.Sound[] m_sounds = new FMODEX.Sound[MAX_SOUND];

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

            m_system.init(32, FMODEX.INITFLAGS.NORMAL, (IntPtr)null);
            return FMODEX.RESULT.OK;
        }
    }
}
