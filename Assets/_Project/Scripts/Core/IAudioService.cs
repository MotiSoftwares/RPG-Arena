namespace RPGArena.Core
{
    // The minimal audio surface that Core (e.g. GameBootstrap) can call WITHOUT depending
    // on the Audio assembly — Audio depends on Core, never the reverse. The real
    // AudioManager (RPGArena.Audio) implements this; an M0 stub just logs.
    public interface IAudioService
    {
        void PlaySfx(string id);
        void PlayMusic(string id, bool loop = true);
        void StopMusic();
    }
}
