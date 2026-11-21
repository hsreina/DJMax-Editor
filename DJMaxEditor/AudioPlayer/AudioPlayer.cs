namespace DJMaxEditor
{
    public interface IAudioPlayer
    {
        bool LoadSound(uint index, string name, int mode = 0);

        bool PauseSound(uint channelIndex);

        bool StopSound(uint channelIndex);

        bool SetVolume(uint channelIndex, float volume);

        uint GetPosition(uint channelIndex);

        void StopAllSounds();

        void PauseAllSounds();

        // audition: play outside the transport channel group, so a keysound triggered
        // from the audio list stays audible while chart playback is paused.
        // offsetMilliseconds: start this far into the sample (BGM / long pads on seek).
        bool PlaySound(uint channelIndex, uint soundIndex, float volume, byte pan, uint offsetMilliseconds = 0, bool audition = false);

        /// <summary>Loaded sample length in milliseconds, or 0 when the slot is empty.</summary>
        uint GetSoundLengthMilliseconds(uint soundIndex);

        object GetDebugInfo();

        void SetSoundContext(string key);

        bool IsSoundContextLoaded(string key);

        void MarkSoundContextLoaded(string key);

        void ReleaseSoundContext(string key);
    }
}
