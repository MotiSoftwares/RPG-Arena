using System;
using System.Collections.Generic;
using UnityEngine;
using RPGArena.Characters;
using RPGArena.Combat.AI;

namespace RPGArena.Combat
{
    // One behaviour phase of a boss, entered when its HP drops to/below a threshold (§7.1).
    [Serializable]
    public class BossPhase
    {
        public string name = "Phase";
        [Range(0f, 1f)] public float hpThresholdPercent = 1f;   // enters at/below this HP fraction
        public bool enrage;
        public float attackMultiplier = 1f;                     // enrage scales the boss's damage
    }

    // DATA ONLY: one ScriptableObject per boss (§4.5). Carries the moveset, the Strategy AI
    // asset, the HP-gated phases, the element profile (the puzzle), and the stagger tuning.
    [CreateAssetMenu(menuName = "RPGArena/Boss Definition", fileName = "Boss")]
    public class BossDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string bossName = "Boss";
        [TextArea] public string intro;
        public Sprite portrait;
        public Sprite stageSprite;          // full-body cutout billboarded on the arena (2.5D)
        public Sprite arenaBackdrop;        // cinematic background behind the combatants
        public GameObject modelPrefab;
        public PrimaryStat primaryStat = PrimaryStat.STR;

        [Header("Stats & kit")]
        public StatBlock baseStats = new();
        public List<Ability> abilities = new();
        public ElementProfile elementProfile;

        [Header("AI & phases")]
        public AIBehavior aiBehavior;
        public List<BossPhase> phases = new();

        [Header("Presentation")]
        [Tooltip("Auto-scale the model to this world height. A dragon looms at ~5; a humanoid boss reads right at ~3.2-3.6 (a 5u human is a parade balloon, not a threat).")]
        public float modelHeight = 5f;

        [Header("Control")]
        [Tooltip("This boss can still BE Frozen — the flag lands, so Shatter (Frozen + Physical x2.3) and Brittle pay out exactly as before — but it never sits a turn out for it. Use on a boss whose identity is relentless pressure; a boss standing frozen drains the tension out of its own fight. Does NOT affect Break: sitting out a Break is the payoff the player earned.")]
        public bool neverLosesTurnToControl;

        [Header("Stagger")]
        public float staggerThreshold = 100f;
        public int staggeredTurns = 1;          // how many of the boss's turns a Break costs it
        [Tooltip("Stagger bled off at the end of each of this boss's turns. >0 punishes chip-and-turtle: you must commit pressure in a burst. 0 = classic behaviour (Dragon, Black Mage).")]
        public float staggerDecayPerTurn = 0f;

        [Header("Minions (live battles only — headless tests stay trio-vs-boss)")]
        public List<MinionDefinition> minions = new();

#if UNITY_EDITOR
        // Catch a misconfigured boss in the Inspector instead of a silent NRE mid-fight (§3.4).
        private void OnValidate()
        {
            if (aiBehavior == null) Debug.LogWarning($"[{name}] BossDefinition has no AI Behavior assigned.", this);
            if (elementProfile == null) Debug.LogWarning($"[{name}] BossDefinition has no Element Profile (its weakness puzzle).", this);
            if (abilities == null || abilities.Count == 0) Debug.LogWarning($"[{name}] BossDefinition has no abilities.", this);
            if (staggerThreshold <= 0f) Debug.LogWarning($"[{name}] staggerThreshold must be > 0.", this);
        }
#endif
    }
}
