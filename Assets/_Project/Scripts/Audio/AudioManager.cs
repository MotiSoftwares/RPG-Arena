using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using RPGArena.Core;

namespace RPGArena.Audio
{
    // The real audio service (CLAUDE.md §11). All output is ROUTED THROUGH AN AudioMixer
    // (Master → Music / SFX), never hardcoded volumes — the settings sliders drive the mixer's
    // exposed parameters. SFX use a small round-robin AudioSource pool (no per-shot
    // Instantiate/Destroy churn); music cross-fades. It self-subscribes to nothing — combat
    // SFX are routed by the presentation-side BattleAudio bridge, keeping this assembly free of
    // any Gameplay dependency.
    public class AudioManager : MonoBehaviour, IAudioService
    {
        [System.Serializable] public class Clip { public string id; public AudioClip clip; }

        [Header("Mixer routing")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("Banks (id -> clip)")]
        [SerializeField] private List<Clip> sfx = new();
        [SerializeField] private List<Clip> music = new();

        [Header("Pool")]
        [SerializeField] private int sfxVoices = 8;

        private readonly Dictionary<string, AudioClip> sfxMap = new();
        private readonly Dictionary<string, AudioClip> musicMap = new();
        private AudioSource[] pool;
        private int nextVoice;
        private AudioSource musicSource;
        private Coroutine musicFade;
        private Coroutine duckCo;

        private void Awake()
        {
            // Build fast lookups from the inspector-authored banks.
            foreach (var e in sfx) if (e != null && e.clip != null && !sfxMap.ContainsKey(e.id)) sfxMap[e.id] = e.clip;
            foreach (var e in music) if (e != null && e.clip != null && !musicMap.ContainsKey(e.id)) musicMap[e.id] = e.clip;

            // SFX voice pool — all routed through the SFX mixer group.
            pool = new AudioSource[Mathf.Max(1, sfxVoices)];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject($"SfxVoice{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false; src.outputAudioMixerGroup = sfxGroup;
                pool[i] = src;
            }

            // Dedicated looping music source on the Music group.
            var mgo = new GameObject("MusicSource");
            mgo.transform.SetParent(transform, false);
            musicSource = mgo.AddComponent<AudioSource>();
            musicSource.playOnAwake = false; musicSource.loop = true; musicSource.outputAudioMixerGroup = musicGroup;

            // Apply persisted volumes so the mixer matches the saved settings on boot.
            ApplySaved(AudioBus.Master);
            ApplySaved(AudioBus.Music);
            ApplySaved(AudioBus.Sfx);
        }

        // --- IAudioService ------------------------------------------------------------
        public void PlaySfx(string id)
        {
            if (string.IsNullOrEmpty(id) || !sfxMap.TryGetValue(id, out var clip) || clip == null) return;
            // Round-robin so overlapping hits don't cut each other off.
            var src = pool[nextVoice];
            nextVoice = (nextVoice + 1) % pool.Length;
            src.PlayOneShot(clip);
        }

        public void PlayMusic(string id, bool loop = true)
        {
            if (string.IsNullOrEmpty(id) || !musicMap.TryGetValue(id, out var clip) || clip == null) return;
            if (musicSource.clip == clip && musicSource.isPlaying) return;   // already playing it
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(CrossFade(clip, loop));
        }

        public void StopMusic()
        {
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(FadeOutStop());
        }

        // Transient music duck (§12.3): the BREAK stinger lands harder when the loop dips under it.
        // Restores to the persisted level — never writes PlayerPrefs. Unscaled so it rides the slow-mo.
        public void DuckMusic(float toLinear, float hold, float release)
        {
            if (mixer == null) return;
            if (duckCo != null) StopCoroutine(duckCo);
            duckCo = StartCoroutine(DuckRoutine(Mathf.Clamp01(toLinear), Mathf.Max(0f, hold), Mathf.Max(0.01f, release)));
        }

        private IEnumerator DuckRoutine(float toLinear, float hold, float release)
        {
            float baseLin = GetVolume(AudioBus.Music);     // the persisted level we swell back to
            const float attack = 0.08f;
            for (float t = 0f; t < attack; t += Time.unscaledDeltaTime) { SetMusicDb(Mathf.Lerp(baseLin, toLinear, t / attack)); yield return null; }
            SetMusicDb(toLinear);
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0f; t < release; t += Time.unscaledDeltaTime) { SetMusicDb(Mathf.Lerp(toLinear, baseLin, t / release)); yield return null; }
            SetMusicDb(baseLin);
            duckCo = null;
        }

        // Set the music bus dB directly WITHOUT persisting (unlike SetVolume) — for the transient duck.
        private void SetMusicDb(float linear) { if (mixer != null) mixer.SetFloat(AudioBus.Music, LinearToDb(linear)); }

        public void SetVolume(string bus, float linear01)
        {
            if (mixer != null) mixer.SetFloat(bus, LinearToDb(linear01));
            PlayerPrefs.SetFloat(bus, Mathf.Clamp01(linear01));
        }

        public float GetVolume(string bus) => PlayerPrefs.GetFloat(bus, 0.85f);

        // --- helpers ------------------------------------------------------------------
        private void ApplySaved(string bus) => SetVolume(bus, GetVolume(bus));

        // Perceptual mapping: a linear slider feels right when mapped to decibels by log10.
        private static float LinearToDb(float linear) =>
            linear <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp01(linear)) * 20f;

        private IEnumerator CrossFade(AudioClip next, bool loop)
        {
            const float dur = 0.6f;
            // Fade the current track down.
            float startVol = musicSource.volume;
            for (float t = 0; t < dur && musicSource.isPlaying; t += Time.unscaledDeltaTime)
            { musicSource.volume = Mathf.Lerp(startVol, 0f, t / dur); yield return null; }
            // Swap and fade the new track up.
            musicSource.clip = next; musicSource.loop = loop; musicSource.volume = 0f; musicSource.Play();
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            { musicSource.volume = Mathf.Lerp(0f, 1f, t / dur); yield return null; }
            musicSource.volume = 1f;
        }

        private IEnumerator FadeOutStop()
        {
            const float dur = 0.5f;
            float startVol = musicSource.volume;
            for (float t = 0; t < dur && musicSource.isPlaying; t += Time.unscaledDeltaTime)
            { musicSource.volume = Mathf.Lerp(startVol, 0f, t / dur); yield return null; }
            musicSource.Stop(); musicSource.volume = 1f;
        }
    }
}
