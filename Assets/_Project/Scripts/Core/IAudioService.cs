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
        // Does the SFX bank contain this id? Callers use it to pick a specific variant
        // (e.g. "hit_fire") and gracefully fall back to the generic clip when absent.
        bool HasSfx(string id);
        void PlayMusic(string id, bool loop = true);
        void StopMusic();

        // Briefly dip the music bus (e.g. under a BREAK stinger) to toLinear, hold, then swell back
        // to the persisted level. Does NOT change the saved volume — purely a transient duck.
        void DuckMusic(float toLinear, float hold, float release);

        // Volume on a 0..1 linear scale (mapped to dB internally) for the settings sliders;
        // bus is one of the AudioBus constants. GetVolume returns the persisted value.
        void SetVolume(string bus, float linear01);
        float GetVolume(string bus);

        // Pause soundscape: transitions the mixer to the "Paused" snapshot (lowpass muffle on the
        // Master bus) and freezes pooled SFX via AudioListener.pause; music keeps playing, muffled.
        void SetPaused(bool paused);
    }
}
