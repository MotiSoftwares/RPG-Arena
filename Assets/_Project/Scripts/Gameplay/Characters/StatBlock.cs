using System;
using UnityEngine;

namespace RPGArena.Characters
{
    // Plain serializable stat container. Definitions hold the BASE values; each runtime
    // Entity copies one of these (via Clone) so buffs/debuffs never mutate the shared
    // ScriptableObject data (CLAUDE.md §4.5 / §A.2).
    [Serializable]
    public class StatBlock
    {
        [Header("Primary stats (MapleStory mapping)")]
        public int STR, DEX, INT, LUK;

        [Header("Resources")]
        public int maxHP = 100;
        public int maxMP = 50;

        [Header("Base offense / defense knobs")]
        public int baseAttack;
        public int baseMagicAttack;
        public int baseDefense;
        public int baseSpeed = 10;

        [Header("Accuracy / evasion / crit (feeds the informed-gamble RNG, Appendix E.1)")]
        public float baseAccuracy;
        public float baseEvasion;
        public float baseCritChance = 0.05f;
        public float critDamage = 1.5f;

        // Returns a deep copy so a runtime entity never edits the shared base block.
        public StatBlock Clone() => (StatBlock)MemberwiseClone();
    }
}
