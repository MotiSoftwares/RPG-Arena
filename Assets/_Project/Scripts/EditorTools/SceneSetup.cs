#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RPGArena.Core;
using RPGArena.Core.Events;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.Events;
using RPGArena.UI;
using RPGArena.Narrative;

namespace RPGArena.EditorTools
{
    // Wires the BattleArena scene reproducibly: drops a BattleController + BattleHUD and assigns
    // every content/channel reference by loading the authored assets, then saves the scene. Far
    // more reliable than hand-assigning ~20 serialized references via the MCP bridge, and it can
    // be re-run any time the wiring changes.
    public static class SceneSetup
    {
        private const string SO = "Assets/_Project/ScriptableObjects/";

        [MenuItem("RPGArena/Setup Battle Scene")]
        public static void SetupBattleArena()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/BattleArena.unity", OpenSceneMode.Single);

            var balance = L<BalanceConfig>("Config/BalanceConfig.asset");
            var dragon = L<BossDefinition>("Bosses/Dragon.asset");
            var roster = new List<CharacterDefinition>
            {
                L<CharacterDefinition>("Characters/Warrior.asset"),
                L<CharacterDefinition>("Characters/Mage.asset"),
                L<CharacterDefinition>("Characters/Thief.asset"),
                L<CharacterDefinition>("Characters/Archer.asset"),
            };
            var cStarted = L<VoidChannel>("Events/OnBattleStarted.asset");
            var cWon = L<VoidChannel>("Events/OnBattleWon.asset");
            var cLost = L<VoidChannel>("Events/OnBattleLost.asset");
            var cTurnS = L<EntityChannel>("Events/OnTurnStarted.asset");
            var cTurnE = L<EntityChannel>("Events/OnTurnEnded.asset");
            var cDied = L<EntityChannel>("Events/OnEntityDied.asset");
            var cBreak = L<EntityChannel>("Events/OnStaggerBroken.asset");
            var cDmg = L<DamageResultChannel>("Events/OnDamageDealt.asset");
            var cTele = L<AbilityChannel>("Events/OnBossTelegraph.asset");

            DestroyIfExists("BattleSystem");
            DestroyIfExists("BattleHUD");
            DestroyIfExists("PauseMenu");

            var sysGo = new GameObject("BattleSystem");
            var ctrl = sysGo.AddComponent<BattleController>();
            ctrl.balance = balance; ctrl.boss = dragon; ctrl.roster = roster;
            // Full boss roster (picked by RunState) + all boons (looked up by name at setup).
            ctrl.bossRoster = new List<BossDefinition>
            {
                dragon,
                L<BossDefinition>("Bosses/BlackMage.asset"),
                L<BossDefinition>("Bosses/EvilWarrior.asset"),
            };
            ctrl.boonRoster = LoadAll<BoonDefinition>("Boons");
            ctrl.onBattleStarted = cStarted; ctrl.onBattleWon = cWon; ctrl.onBattleLost = cLost;
            ctrl.onTurnStarted = cTurnS; ctrl.onTurnEnded = cTurnE; ctrl.onEntityDied = cDied;
            ctrl.onStaggerBroken = cBreak; ctrl.onDamageDealt = cDmg; ctrl.onBossTelegraph = cTele;

            var hudGo = new GameObject("BattleHUD");
            var hud = hudGo.AddComponent<BattleHUD>();
            hud.controller = ctrl;
            hud.onTurnStarted = cTurnS; hud.onStaggerBroken = cBreak; hud.onEntityDied = cDied;
            hud.onDamageDealt = cDmg; hud.onBossTelegraph = cTele;
            hud.onBattleWon = cWon; hud.onBattleLost = cLost;

            var pauseGo = new GameObject("PauseMenu");
            pauseGo.AddComponent<PauseMenu>();

            // Juice layer (presentation): floating numbers, hit-stop, shake, BREAK slow-mo.
            DestroyIfExists("JuiceController");
            var juiceGo = new GameObject("JuiceController");
            var juice = juiceGo.AddComponent<JuiceController>();
            juice.onDamageDealt = cDmg; juice.onStaggerBroken = cBreak; juice.onBossTelegraph = cTele;

            // Audio bridge (presentation): routes combat events to the AudioMixer-backed service.
            DestroyIfExists("BattleAudio");
            var audioGo = new GameObject("BattleAudio");
            var battleAudio = audioGo.AddComponent<BattleAudio>();
            battleAudio.onDamageDealt = cDmg; battleAudio.onStaggerBroken = cBreak; battleAudio.onBossTelegraph = cTele;
            battleAudio.onBattleStarted = cStarted; battleAudio.onBattleWon = cWon; battleAudio.onBattleLost = cLost;

            // Narrative (Ink): the pre-fight intro whose choice alters the opening (§13). The Ink
            // package compiles the .ink to a .json TextAsset; if it isn't compiled yet the runner
            // simply skips the intro (graceful) and a re-run wires it once the JSON exists.
            DestroyIfExists("Narrative");
            var narrGo = new GameObject("Narrative");
            var narr = narrGo.AddComponent<NarrativeRunner>();
            narr.introJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Ink/dragon_intro.json");
            narr.outroJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Ink/dragon_outro.json");
            if (narr.introJson == null) Debug.LogWarning("[SceneSetup] dragon_intro.json not found yet — re-run after Ink compiles.");

            // Run flow (presentation): owns boon-select between bosses, run-complete, and retry.
            DestroyIfExists("RunFlow");
            var runGo = new GameObject("RunFlow");
            var runFlow = runGo.AddComponent<RunFlow>();
            runFlow.onBattleWon = cWon; runFlow.onBattleLost = cLost;
            runFlow.boonRoster = LoadAll<BoonDefinition>("Boons");

            DressArena();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SceneSetup] BattleArena wired: BattleController + BattleHUD + PauseMenu + 9 channels + content.");
        }

        [MenuItem("RPGArena/Setup Menu Scene")]
        public static void SetupMenuScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
            DestroyIfExists("MenuCanvas");   // remove the M0 placeholder menu
            DestroyIfExists("MainMenu");
            var go = new GameObject("MainMenu");
            var menu = go.AddComponent<RPGArena.UI.MainMenuUI>();

            // Assign the title-screen background (imported as a Sprite).
            const string bgPath = "Assets/_Project/Art/Backdrops/TitleScreen.png";
            if (AssetImporter.GetAtPath(bgPath) is TextureImporter ti &&
                (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.SaveAndReimport();
            }
            menu.backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);
            if (menu.backgroundSprite == null) Debug.LogWarning("[SceneSetup] TitleScreen sprite not found/loaded.");

            // Hero art for the Character Select cards (cutouts, in the Classes order).
            string[] heroes = { "Warrior", "Mage", "Thief", "Archer" };
            menu.classPortraits = new Sprite[heroes.Length];
            for (int i = 0; i < heroes.Length; i++)
                menu.classPortraits[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/Sprites/Combatants/cutout/{heroes[i]}.png");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SceneSetup] MainMenu wired with MainMenuUI + title background.");
        }

        // Lava-glow stage: a dark ground, a warm point light by the boss, framed camera + a
        // moodier directional light. The orthographic 2.5D arena (§10.1).
        private static void DressArena()
        {
            DestroyIfExists("Ground");
            DestroyIfExists("LavaGlow");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0, 0, 1.5f);
            ground.transform.localScale = new Vector3(4f, 1f, 1.6f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = Mat("Arena_Ground", new Color(0.12f, 0.08f, 0.07f));

            var glow = new GameObject("LavaGlow");
            glow.transform.position = new Vector3(4.5f, 1.2f, 0.6f);
            var pl = glow.AddComponent<Light>();
            pl.type = LightType.Point; pl.color = new Color(1f, 0.42f, 0.16f); pl.intensity = 5f; pl.range = 14f;

            var sun = GameObject.Find("Directional Light");
            if (sun) { var l = sun.GetComponent<Light>(); if (l) { l.intensity = 0.85f; l.color = new Color(0.7f, 0.75f, 1f); } }

            var cam = GameObject.FindWithTag("MainCamera");
            if (cam)
            {
                cam.transform.position = new Vector3(-0.2f, 1.5f, -10f);
                var c = cam.GetComponent<Camera>();
                if (c)
                {
                    c.orthographic = true; c.orthographicSize = 4.7f; c.allowHDR = true;
                    // GetUniversalAdditionalCameraData() adds the component if missing, so the
                    // post-processing flag actually persists on the camera.
                    c.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                }
            }

            SetupPostFX();
        }

        // Global post-processing Volume (§10.6 / §12.5): Bloom for the lava/VFX glow, a warm
        // colour grade, and a vignette to focus the stage.
        private static void SetupPostFX()
        {
            const string dir = "Assets/_Project/Settings";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Project", "Settings");
            const string path = dir + "/BattlePostFX.asset";

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }

            if (!profile.TryGet<Bloom>(out var bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = true; bloom.intensity.Override(0.9f); bloom.threshold.Override(0.85f);
            bloom.scatter.Override(0.7f); bloom.tint.Override(new Color(1f, 0.75f, 0.5f));

            if (!profile.TryGet<ColorAdjustments>(out var ca)) ca = profile.Add<ColorAdjustments>(true);
            ca.active = true; ca.postExposure.Override(0.1f); ca.contrast.Override(14f); ca.saturation.Override(8f);

            if (!profile.TryGet<Vignette>(out var vig)) vig = profile.Add<Vignette>(true);
            vig.active = true; vig.intensity.Override(0.34f); vig.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);

            DestroyIfExists("PostFXVolume");
            var volGo = new GameObject("PostFXVolume");
            var vol = volGo.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 1; vol.sharedProfile = profile;
        }

        // Creates (or reuses) a URP/Lit material asset so scene objects keep a valid reference.
        private static Material Mat(string name, Color color)
        {
            const string dir = "Assets/_Project/Art/Materials";
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art")) AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Project/Art", "Materials");
            string path = $"{dir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { existing.SetColor("_BaseColor", color); return existing; }
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static T L<T>(string rel) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(SO + rel);
            if (a == null) Debug.LogError($"[SceneSetup] Missing asset: {SO + rel}");
            return a;
        }

        // Load every asset of a type from a ScriptableObjects subfolder (e.g. all the boons).
        private static List<T> LoadAll<T>(string subfolder) where T : Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { SO + subfolder }))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) list.Add(a);
            }
            return list;
        }

        private static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go) Object.DestroyImmediate(go);
        }
    }
}
#endif
