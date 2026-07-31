#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using RPGArena.Combat;

namespace RPGArena.EditorTools
{
    // Maps each ability to a fitting spell-VFX prefab from the imported effect packs (ErbGameArt
    // Fantasy effects + Hovl Studio Magic effects), filling the previously-unused Ability.vfxPrefab.
    // Chosen by name override (ults) -> effect type (heal/buff/debuff) -> element. Run after
    // "Build All Content" (which recreates the abilities and clears vfxPrefab). Matches the
    // established assigner pattern (IconAssigner / CombatantSpriteAssigner).
    public static class VfxAssigner
    {
        // ERB ONLY. All 45 materials under Assets/Hovl Studio (38 in Magic effects pack, 7 in
        // MoonSword) reference shader guids 0406db5a14f94604a8c57ccfbc9f3b46 /
        // 933532a4fcc9baf4fa0491de14d08ed7, and NEITHER shader asset exists in this project — the
        // packs ship no .shader files at all. Anything picked from there draws the error shader.
        // Re-adding this root silently re-breaks two dozen abilities with no console error.
        private static readonly string[] VfxRoots = { "Assets/ErbGameArt" };
        private const string Abilities = "Assets/_Project/ScriptableObjects/Abilities";

        [MenuItem("RPGArena/Assign Ability VFX")]
        public static void Assign()
        {
            int n = 0, miss = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Ability", new[] { Abilities }))
            {
                var ab = AssetDatabase.LoadAssetAtPath<Ability>(AssetDatabase.GUIDToAssetPath(guid));
                if (ab == null) continue;
                var vfx = Pick(ab);
                if (vfx != null) { ab.vfxPrefab = vfx; EditorUtility.SetDirty(ab); n++; }
                else miss++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[VFX] Assigned spell VFX to {n} abilities ({miss} unmatched).");
        }

        private static GameObject Pick(Ability a)
        {
            string nm = a.name;
            // Named ults / specific skills first.
            if (nm.Contains("Meteor")) return Find("Meteor rain");
            if (nm.Contains("Blizzard")) return Find("Ice freeze skill") ?? Find("Ice arrow");
            if (nm.Contains("ArrowRain") || nm.Contains("Arrow Rain")) return Find("Spears rain");
            if (nm.Contains("Heal")) return Find("Healing buff") ?? Find("Healing");
            // HAND-AUTHORED (the Thief water/coating pass). The generic rules below send every
            // ApplyStatus skill to the golden 'Magic buff' and every Debuff to the broken Hovl
            // 'Debuff', which is how three of the Thief's five skills ended up invisible or magenta.
            if (nm == "Thief_WaterBomb") return Find("Water attack");        // blue WATER, thrown
            if (nm == "ItemFx_SlickFlask") return Find("Water attack");      // same coating, in a bottle
            if (nm == "Thief_ShadowMark") return Find("Dard magic shoot");   // dark bolt, thrown
            if (nm == "Thief_DarkSight") return Find("Glowing orbs");        // the vanish aura
            if (nm == "Thief_Assassinate") return Find("Death magic circle");// the d20 finisher

            // By effect type (non-damaging support).
            switch (a.effectType)
            {
                case EffectType.Heal: return Find("Healing buff") ?? Find("Healing");
                case EffectType.Buff: return Find("Magic buff") ?? Find("Buff");
                case EffectType.Debuff: return Find("Debuff") ?? Find("Magic buff");
                case EffectType.Stance: return Find("Magic buff") ?? Find("Star aura");
                case EffectType.ApplyStatus: return Find("Magic buff");
                case EffectType.BossMove: return Find("Charge slash red") ?? Find("Magic circle");
            }

            // By element (attacks).
            switch (a.element)
            {
                case ElementType.Fire: return Find("Fireball");
                case ElementType.Ice: return Find("Ice arrow") ?? Find("Snow hit");
                case ElementType.Lightning: return Find("Lightning attack") ?? Find("Electro hit");
                case ElementType.Holy: return Find("Holy hit") ?? Find("Healing buff");
                case ElementType.Dark: return Find("Red energy explosion") ?? Find("Debuff");
                default: return a.targetRule == TargetRule.AllEnemies ? (Find("Spears rain") ?? Find("Magic arrow"))
                                                                      : (Find("Magic arrow") ?? Find("Stones hit"));
            }
        }

        private static GameObject Find(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", VfxRoots))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(p), name, System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }
    }
}
#endif
