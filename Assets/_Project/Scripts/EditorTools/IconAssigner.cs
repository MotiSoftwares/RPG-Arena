#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using RPGArena.Combat;

namespace RPGArena.EditorTools
{
    // Imports the Pollinations-generated icon PNGs as Sprites and assigns each to the matching
    // Ability asset (by file name). Run after Tools/gen_icons.mjs has produced the PNGs.
    public static class IconAssigner
    {
        private const string Icons = "Assets/_Project/Art/UI/icons";
        private const string Abilities = "Assets/_Project/ScriptableObjects/Abilities";

        [MenuItem("RPGArena/Assign Ability Icons")]
        public static void AssignIcons()
        {
            if (!AssetDatabase.IsValidFolder(Icons)) { Debug.LogError($"[IconAssigner] No icons folder at {Icons}"); return; }

            // 1) Ensure every icon texture imports as a Sprite.
            int sprites = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Icons }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter imp && imp.textureType != TextureImporterType.Sprite)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.SaveAndReimport();
                    sprites++;
                }
            }

            // 2) Assign each sprite to the ability whose asset file name matches.
            int assigned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Ability", new[] { Abilities }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ability = AssetDatabase.LoadAssetAtPath<Ability>(path);
                if (ability == null) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Icons}/{name}.png");
                if (sprite != null) { ability.icon = sprite; EditorUtility.SetDirty(ability); assigned++; }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[IconAssigner] {sprites} textures set to Sprite, {assigned} abilities now have icons.");
        }
    }
}
#endif
