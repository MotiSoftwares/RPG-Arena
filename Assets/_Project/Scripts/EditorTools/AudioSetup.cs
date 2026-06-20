#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using RPGArena.Core;
using RPGArena.Audio;

namespace RPGArena.EditorTools
{
    // Wires the AudioManager reproducibly (CLAUDE.md §11): attaches it to the Boot scene's
    // GameBootstrap object, points it at the GameMixer's Master/Music/SFX groups, and fills its
    // SFX + Music banks from the generated clips (file name = id). Re-runnable any time the audio
    // set changes — far more reliable than hand-assigning every clip in the Inspector.
    public static class AudioSetup
    {
        private const string MixerPath = "Assets/_Project/Audio/GameMixer.mixer";
        private const string SfxDir = "Assets/_Project/Audio/SFX";
        private const string MusicDir = "Assets/_Project/Audio/Music";
        private const string BootScene = "Assets/_Project/Scenes/Boot.unity";

        [MenuItem("RPGArena/Setup Audio")]
        public static void Setup()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) { Debug.LogError("[AudioSetup] GameMixer.mixer not found — run the mixer builder first."); return; }
            var music = FindGroup(mixer, "Music");
            var sfx = FindGroup(mixer, "SFX");

            var scene = EditorSceneManager.OpenScene(BootScene, OpenSceneMode.Single);
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            if (boot == null) { Debug.LogError("[AudioSetup] No GameBootstrap in the Boot scene."); return; }

            var am = boot.GetComponent<AudioManager>();
            if (am == null) am = boot.gameObject.AddComponent<AudioManager>();

            var so = new SerializedObject(am);
            so.FindProperty("mixer").objectReferenceValue = mixer;
            so.FindProperty("musicGroup").objectReferenceValue = music;
            so.FindProperty("sfxGroup").objectReferenceValue = sfx;
            FillBank(so.FindProperty("sfx"), SfxDir);
            FillBank(so.FindProperty("music"), MusicDir);
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AudioSetup] AudioManager wired on Boot: mixer + Music/SFX groups + banks (sfx & music) from generated clips.");
        }

        // Fill a List<Clip> serialized property with every AudioClip in a folder, keyed by file name.
        private static void FillBank(SerializedProperty listProp, string dir)
        {
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { dir });
            listProp.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(clips[i]);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var el = listProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("id").stringValue = Path.GetFileNameWithoutExtension(path);
                el.FindPropertyRelative("clip").objectReferenceValue = clip;
            }
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            var groups = mixer.FindMatchingGroups(name);
            return groups != null && groups.Length > 0 ? groups[0] : null;
        }
    }
}
#endif
