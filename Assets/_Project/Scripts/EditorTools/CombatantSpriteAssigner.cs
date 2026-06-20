#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using RPGArena.Characters;
using RPGArena.Combat;

namespace RPGArena.EditorTools
{
    // Imports the background-removed combatant cutouts as Sprites and assigns each to its
    // CharacterDefinition / BossDefinition (stageSprite + portrait), by file name. The battle
    // then billboards them on the orthographic stage instead of capsules. Re-runnable.
    public static class CombatantSpriteAssigner
    {
        private const string Cutouts = "Assets/_Project/Art/Sprites/Combatants/cutout";
        private const string Characters = "Assets/_Project/ScriptableObjects/Characters";
        private const string Bosses = "Assets/_Project/ScriptableObjects/Bosses";

        [MenuItem("RPGArena/Assign Combatant Sprites")]
        public static void Assign()
        {
            if (!AssetDatabase.IsValidFolder(Cutouts)) { Debug.LogError($"[CombatantSprites] No cutouts at {Cutouts}"); return; }

            // 1) Ensure each cutout imports as a Sprite (single).
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Cutouts }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter imp && imp.textureType != TextureImporterType.Sprite)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.SaveAndReimport();
                }
            }

            int assigned = 0;

            // 2) Heroes: file name == className.
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterDefinition", new[] { Characters }))
            {
                var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                var sprite = LoadSprite(def.className);
                if (sprite != null) { def.stageSprite = sprite; def.portrait = sprite; EditorUtility.SetDirty(def); assigned++; }
            }

            // 3) Boss: match the last word of the boss name (e.g. "The Dragon" -> Dragon).
            foreach (var guid in AssetDatabase.FindAssets("t:BossDefinition", new[] { Bosses }))
            {
                var def = AssetDatabase.LoadAssetAtPath<BossDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                var key = def.bossName.Split(' ').Last();
                var sprite = LoadSprite(key);
                if (sprite != null) { def.stageSprite = sprite; def.portrait = sprite; EditorUtility.SetDirty(def); assigned++; }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[CombatantSprites] Assigned stage sprites to {assigned} definitions.");
        }

        private static Sprite LoadSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{Cutouts}/{name}.png");
    }
}
#endif
