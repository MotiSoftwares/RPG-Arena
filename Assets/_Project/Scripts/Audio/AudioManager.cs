using UnityEngine;
using RPGArena.Core;

namespace RPGArena.Audio
{
    // M0 stub audio service. It satisfies the IAudioService contract so the rest of the
    // game can request audio without caring about the implementation; for now it only logs.
    // The real AudioMixer-routed manager (Music/SFX/UI groups, pooling, cross-fade) arrives
    // in Milestone 5 (CLAUDE.md §11).
    public class AudioManager : MonoBehaviour, IAudioService
    {
        public void PlaySfx(string id) => Debug.Log($"[AudioManager] (stub) PlaySfx: {id}");

        public void PlayMusic(string id, bool loop = true) =>
            Debug.Log($"[AudioManager] (stub) PlayMusic: {id} (loop={loop})");

        public void StopMusic() => Debug.Log("[AudioManager] (stub) StopMusic");
    }
}
