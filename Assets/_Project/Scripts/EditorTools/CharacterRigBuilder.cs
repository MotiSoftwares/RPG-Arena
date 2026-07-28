#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using RPGArena.Combat;

namespace RPGArena.EditorTools
{
    // Turns the three "Pro" animation packs (rigged Mixamo character + clip FBX) into game-ready
    // 3D combatants: configures Humanoid import, fixes the URP magenta (extract textures -> build
    // URP/Lit -> remap onto the FBX), retargets the clips, builds an Animator Controller per class,
    // builds a prefab per combatant, and assigns it to the CharacterDefinition/BossDefinition.
    // Finds FBX by file name so it works wherever the packs live. Re-runnable.
    // NOTE: run AFTER "Build All Content" (which recreates the definitions and clears modelPrefab).
    public static class CharacterRigBuilder
    {
        private const string Mats = "Assets/_Project/Art/Rigs/Materials";
        private const string Ctrls = "Assets/_Project/Art/Rigs/Controllers";
        private const string Prefabs = "Assets/_Project/Art/Rigs/Prefabs";
        private const string SO = "Assets/_Project/ScriptableObjects/";

        // One animation pack: the character FBX + the clips we drive (by file name, no extension).
        // Optional clips (idle2/attack2/attack3/cast/area/victory) may be null — their state is skipped.
        private class Pack { public string key, charName, idle, idle2, attack, attack2, attack3, cast, area, hit, die, victory; }
        // One combatant prefab: which pack it uses, whether it's a boss def, and an optional tint.
        private class Target { public string defName, packKey; public bool boss; public Color tint; }

        private static readonly Pack[] Packs =
        {
            new Pack { key = "Warrior", charName = "Paladin WProp J Nordstrom",
                idle = "sword and shield idle", idle2 = "sword and shield idle (2)",
                attack = "sword and shield slash", attack2 = "sword and shield slash (2)", attack3 = "sword and shield slash (3)",
                cast = "sword and shield casting", area = "sword and shield slash (4)",
                hit = "sword and shield impact", die = "sword and shield death", victory = "sword and shield power up" },
            new Pack { key = "Mage", charName = "Ch39_nonPBR",
                idle = "standing idle", idle2 = "standing idle 02",
                attack = "Standing 1H Magic Attack 01", attack2 = "Standing 1H Magic Attack 02", attack3 = "Standing 1H Magic Attack 03",
                cast = "Standing 2H Cast Spell 01", area = "Standing 2H Magic Area Attack 01",
                hit = "Standing React Small From Front", die = "Standing React Death Forward", victory = "Standing 2H Magic Attack 05" },
            new Pack { key = "Archer", charName = "Arissa",
                idle = "standing idle 01", idle2 = "standing idle 02 looking",
                attack = "standing aim recoil", attack2 = "standing draw arrow", attack3 = "standing aim overdraw",
                cast = null, area = "standing aim overdraw",
                hit = "standing react small from front", die = "standing death forward 01", victory = null },
        };

        private static readonly Target[] Targets =
        {
            new Target { defName = "Warrior", packKey = "Warrior", boss = false, tint = Color.white },
            new Target { defName = "Mage", packKey = "Mage", boss = false, tint = Color.white },
            new Target { defName = "Archer", packKey = "Archer", boss = false, tint = Color.white },
            new Target { defName = "Thief", packKey = "Archer", boss = false, tint = new Color(0.5f, 0.5f, 0.62f) },

            // THE BOSSES ARE DELIBERATELY NOT LISTED HERE ANY MORE.
            //
            // They used to be: EvilWarrior built from the "Warrior" pack and BlackMage from "Mage",
            // which meant both bosses wore a tinted copy of a hero rig — the final boss was literally
            // the player's own Warrior in red. Both now have hand-authored rigs from other packs
            // (EvilWarriorMutant from the Assassin Pack, BlackMageWizard from WizardPolyArt) with
            // their own controllers and materials.
            //
            // Leaving them in was a live footgun rather than dead config: AssignToDef writes
            // def.modelPrefab, so one run of this menu item would silently re-point both bosses back
            // at hero-lookalike prefabs and undo the work with no error and no visible diff until
            // someone entered play mode. If a boss ever needs rebuilding, do it deliberately.
        };

        [MenuItem("RPGArena/Build Character Rigs")]
        public static void Build()
        {
            EnsureFolder("Assets/_Project/Art", "Rigs");
            EnsureFolder("Assets/_Project/Art/Rigs", "Materials");
            EnsureFolder("Assets/_Project/Art/Rigs", "Controllers");
            EnsureFolder("Assets/_Project/Art/Rigs", "Prefabs");

            var charPaths = new Dictionary<string, string>();
            var avatars = new Dictionary<string, Avatar>();
            var baseMats = new Dictionary<string, Material>();
            var controllers = new Dictionary<string, AnimatorController>();

            foreach (var p in Packs)
            {
                var charPath = FindFbx(p.charName);
                if (charPath == null) { Debug.LogError($"[Rigs] Character FBX not found: {p.charName}"); continue; }
                charPaths[p.key] = charPath;

                var avatar = ConfigureCharacter(charPath, p.key, out var urpMat);
                if (avatar == null) Debug.LogWarning($"[Rigs] No Avatar generated for {p.charName} — retarget may fail.");
                avatars[p.key] = avatar; baseMats[p.key] = urpMat;

                var c = new Clips
                {
                    idle = ConfigureClip(p.idle, avatar, true),
                    idle2 = ConfigureClip(p.idle2, avatar, true),
                    attack = ConfigureClip(p.attack, avatar, false),
                    attack2 = ConfigureClip(p.attack2, avatar, false),
                    attack3 = ConfigureClip(p.attack3, avatar, false),
                    cast = ConfigureClip(p.cast, avatar, false),
                    area = ConfigureClip(p.area, avatar, false),
                    hit = ConfigureClip(p.hit, avatar, false),
                    die = ConfigureClip(p.die, avatar, false),
                    victory = ConfigureClip(p.victory, avatar, false),
                };
                controllers[p.key] = BuildController(p.key, c);
            }

            int built = 0;
            foreach (var t in Targets)
            {
                if (!charPaths.ContainsKey(t.packKey)) continue;
                var prefab = BuildPrefab(t, charPaths[t.packKey], avatars[t.packKey], controllers[t.packKey], baseMats[t.packKey]);
                if (prefab != null && AssignToDef(t, prefab)) built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Rigs] Build Character Rigs complete — {built} combatants now have a 3D model prefab.");
        }

        // --- character FBX: Humanoid + URP material (the magenta fix) ------------------
        private static Avatar ConfigureCharacter(string charPath, string key, out Material urpMat)
        {
            urpMat = null;
            var imp = (ModelImporter)AssetImporter.GetAtPath(charPath);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.SaveAndReimport();

            // Extract the embedded textures so we can rebuild URP materials from them.
            string exDir = $"{Mats}/{key}";
            EnsureFolder(Mats, key);
            try { imp.ExtractTextures(exDir); } catch { /* some FBX have no embedded textures */ }
            AssetDatabase.Refresh();

            Texture2D diffuse = null, normal = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { exDir }))
            {
                var tp = AssetDatabase.GUIDToAssetPath(guid);
                var lower = Path.GetFileNameWithoutExtension(tp).ToLowerInvariant();
                if (lower.Contains("normal") || lower.Contains("_nm"))
                {
                    if (AssetImporter.GetAtPath(tp) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap)
                    { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
                    normal = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                }
                else if (lower.Contains("diffuse") || lower.Contains("albedo") || lower.Contains("basecolor") || lower.Contains("_diff"))
                {
                    diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                }
            }

            urpMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (diffuse != null) urpMat.SetTexture("_BaseMap", diffuse);
            if (normal != null) { urpMat.SetTexture("_BumpMap", normal); urpMat.EnableKeyword("_NORMALMAP"); urpMat.SetFloat("_BumpScale", 1f); }
            urpMat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(urpMat, $"{Mats}/{key}_URP.mat");

            // Remap every embedded material on the FBX to the URP material so it never renders magenta.
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(charPath))
                if (o is Material em)
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), em.name), urpMat);
            imp.SaveAndReimport();

            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(charPath))
                if (o is Avatar a) return a;
            return null;
        }

        // --- animation FBX: Humanoid retarget onto the character's avatar --------------
        private static AnimationClip ConfigureClip(string clipName, Avatar avatar, bool loop)
        {
            if (string.IsNullOrEmpty(clipName)) return null;   // optional clip — skip silently
            var path = FindFbx(clipName);
            if (path == null) { Debug.LogWarning($"[Rigs] Animation FBX not found: {clipName}"); return null; }
            var ci = (ModelImporter)AssetImporter.GetAtPath(path);
            ci.animationType = ModelImporterAnimationType.Human;
            ci.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            ci.sourceAvatar = avatar;
            ci.importAnimation = true;
            if (loop)
            {
                var clips = ci.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    for (int i = 0; i < clips.Length; i++) { clips[i].loopTime = true; clips[i].loopPose = true; }
                    ci.clipAnimations = clips;
                }
            }
            ci.SaveAndReimport();

            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (o is AnimationClip a && !a.name.StartsWith("__preview")) return a;
            return null;
        }

        // The retargeted clips for one class.
        private class Clips { public AnimationClip idle, idle2, attack, attack2, attack3, cast, area, hit, die, victory; }

        // --- Animator Controller: blended Idle + varied Attack + Cast/AreaAttack/Victory ----
        // Idle is a 2-clip BlendTree (over IdleBlend) so combatants desync; Attack is a 3-variant
        // BlendTree (over AttackVariant); Cast/AreaAttack/Victory are optional one-shot states.
        private static AnimatorController BuildController(string key, Clips c)
        {
            string path = $"{Ctrls}/{key}.controller";
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AttackVariant", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IdleBlend", AnimatorControllerParameterType.Float);
            if (c.cast != null) ctrl.AddParameter("Cast", AnimatorControllerParameterType.Trigger);
            if (c.area != null) ctrl.AddParameter("AreaAttack", AnimatorControllerParameterType.Trigger);
            if (c.victory != null) ctrl.AddParameter("Victory", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            // Idle — a blend of two idles (single child if only one exists).
            var sIdle = ctrl.CreateBlendTreeInController("Idle", out var idleTree);
            idleTree.blendType = BlendTreeType.Simple1D; idleTree.blendParameter = "IdleBlend"; idleTree.useAutomaticThresholds = false;
            idleTree.AddChild(c.idle != null ? c.idle : c.idle2, 0f);
            if (c.idle != null && c.idle2 != null) idleTree.AddChild(c.idle2, 1f);
            sm.defaultState = sIdle;

            // Attack — a blend of up to three slash/shot variants.
            var sAtk = ctrl.CreateBlendTreeInController("Attack", out var atkTree);
            atkTree.blendType = BlendTreeType.Simple1D; atkTree.blendParameter = "AttackVariant"; atkTree.useAutomaticThresholds = false;
            atkTree.AddChild(c.attack, 0f);
            if (c.attack2 != null) atkTree.AddChild(c.attack2, 1f);
            if (c.attack3 != null) atkTree.AddChild(c.attack3, 2f);
            Oneshot(sIdle, sAtk, "Attack", 0.85f);

            var sHit = sm.AddState("Hit"); sHit.motion = c.hit;
            var sDie = sm.AddState("Die"); sDie.motion = c.die;
            var toHit = sm.AddAnyStateTransition(sHit); toHit.hasExitTime = false; toHit.duration = 0.05f; toHit.canTransitionToSelf = false;
            toHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            var hitBack = sHit.AddTransition(sIdle); hitBack.hasExitTime = true; hitBack.exitTime = 0.7f; hitBack.duration = 0.1f;
            var toDie = sm.AddAnyStateTransition(sDie); toDie.hasExitTime = false; toDie.duration = 0.05f; toDie.canTransitionToSelf = false;
            toDie.AddCondition(AnimatorConditionMode.If, 0, "Die");

            if (c.cast != null) { var s = sm.AddState("Cast"); s.motion = c.cast; Oneshot(sIdle, s, "Cast", 0.85f); }
            if (c.area != null) { var s = sm.AddState("AreaAttack"); s.motion = c.area; Oneshot(sIdle, s, "AreaAttack", 0.85f); }
            if (c.victory != null)
            {
                var s = sm.AddState("Victory"); s.motion = c.victory;
                var t = sm.AddAnyStateTransition(s); t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0, "Victory");   // no return — the fight is over
            }

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // Idle -> state on a trigger (no exit time), then state -> Idle after exitTime.
        private static void Oneshot(AnimatorState from, AnimatorState to, string trigger, float exitTime)
        {
            var go = from.AddTransition(to); go.hasExitTime = false; go.duration = 0.05f;
            go.AddCondition(AnimatorConditionMode.If, 0, trigger);
            var back = to.AddTransition(from); back.hasExitTime = true; back.exitTime = exitTime; back.duration = 0.1f;
        }

        // --- prefab: normalize scale + feet, set animator, tint, AnimationDriver -------
        private static GameObject BuildPrefab(Target t, string charPath, Avatar avatar, AnimatorController ctrl, Material baseMat)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(charPath);
            if (fbx == null) return null;

            var root = new GameObject(t.defName);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.SetParent(root.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;

            // Normalize to ~1.8 m tall and drop the feet to y = 0.
            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                if (b.size.y > 0.01f) inst.transform.localScale = Vector3.one * (2.1f / b.size.y);
                rends = inst.GetComponentsInChildren<Renderer>();
                b = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                inst.transform.localPosition -= new Vector3(0, b.min.y, 0);
            }

            var anim = inst.GetComponent<Animator>(); if (anim == null) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.avatar = avatar;
            anim.applyRootMotion = false;

            if (t.tint != Color.white && baseMat != null)
            {
                var tintMat = new Material(baseMat); tintMat.SetColor("_BaseColor", t.tint);
                AssetDatabase.CreateAsset(tintMat, $"{Mats}/{t.defName}_URP.mat");
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var arr = smr.sharedMaterials;
                    for (int i = 0; i < arr.Length; i++) arr[i] = tintMat;
                    smr.sharedMaterials = arr;
                }
            }

            root.AddComponent<RPGArena.Characters.AnimationDriver>();

            string prefabPath = $"{Prefabs}/{t.defName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static bool AssignToDef(Target t, GameObject prefab)
        {
            if (t.boss)
            {
                var def = AssetDatabase.LoadAssetAtPath<BossDefinition>($"{SO}Bosses/{t.defName}.asset");
                if (def == null) { Debug.LogWarning($"[Rigs] Boss def not found: {t.defName}"); return false; }
                def.modelPrefab = prefab; EditorUtility.SetDirty(def); return true;
            }
            var hero = AssetDatabase.LoadAssetAtPath<RPGArena.Characters.CharacterDefinition>($"{SO}Characters/{t.defName}.asset");
            if (hero == null) { Debug.LogWarning($"[Rigs] Hero def not found: {t.defName}"); return false; }
            hero.modelPrefab = prefab; EditorUtility.SetDirty(hero); return true;
        }

        // --- helpers ------------------------------------------------------------------
        private static string FindFbx(string fileNameNoExt)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(p), fileNameNoExt, System.StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}")) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
