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

        bool PlaySound(uint channelIndex, uint soundIndex, float volume, byte pan);

        object GetDebugInfo();
    }
}
