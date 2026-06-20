namespace RPGArena.Core
{
    // Stable names for the AudioMixer's exposed volume parameters. Kept in Core so both the
    // settings UI and the AudioManager refer to the same buses without a hard dependency.
    public static class AudioBus
    {
        public const string Master = "MasterVolume";
        public const string Music = "MusicVolume";
        public const string Sfx = "SFXVolume";
    }

    // The minimal audio surface that Core (e.g. GameBootstrap) and the UI can call WITHOUT
    // depending on the Audio assembly — Audio depends on Core, never the reverse. The real
    // AudioManager (RPGArena.Audio) implements this; an M0 stub just logged.
    public interface IAudioService
    {
        void PlaySfx(string id);
        void PlayMusic(string id, bool loop = true);
        void StopMusic();

        // Volume on a 0..1 linear scale (mapped to dB internally) for the settings sliders;
        // bus is one of the AudioBus constants. GetVolume returns the persisted value.
        void SetVolume(string bus, float linear01);
        float GetVolume(string bus);
    }
}
