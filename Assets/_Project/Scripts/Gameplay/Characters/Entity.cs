using System.Collections.Generic;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Combat;
using RPGArena.Combat.Status;
using RPGArena.Combat.AI;

namespace RPGArena.Characters
{
    // Which side of the fight a combatant is on.
    public enum Team { Heroes, Enemies }

    // Which primary stat a class keys off for physical scaling (MapleStory mapping, §5.2).
    public enum PrimaryStat { STR, DEX, INT, LUK }

    // A combatant in a battle. It is COMPOSED of a stat block, an ability list, a status
    // container, and (for bosses) an AI brain — rather than a deep class hierarchy (§4.5).
    // It operates on a runtime COPY of its definition's stats so buffs never corrupt shared data.
    public class Entity : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "Combatant";
        public Team team = Team.Heroes;
        public bool isBoss;
        public PrimaryStat primaryStat = PrimaryStat.STR;
        // Positioning / row tactics: a BACK-ROW hero is shielded from single-target melee but still
        // caught by AoE; the FRONT row (melee/STR) draws the boss's single-target aggro.
        public bool backRow;

        [Header("Runtime state")]
        public StatBlock stats;                 // a COPY, never the definition's block
        public int currentHP;
        public int currentMP;
        public ElementProfile elementProfile;
        public List<Ability> abilities = new();
        public Sprite stageSprite;              // optional full-body art billboarded on the stage
        public GameObject modelPrefab;          // optional rigged 3D model (replaces the billboard)

        [Header("Stance / attunement (player toggles, §5.9)")]
        // Mage: which elemental school the attunement-following basic (Magic Bolt) uses.
        public ElementType currentAttunement = ElementType.Ice;

        // Thaw cooldown: while >0 this combatant cannot be hit with a turn-skipping control status
        // (Frozen). Counts down on its OWN turn ends, so it is measured in the victim's turns rather
        // than rounds. See AbilityCommands.ApplyStatuses for why control needs diminishing returns.
        public int controlLockTurns;

        // Difficulty's damage knob. Multiplied into outgoing damage in DamagePipeline step 2, so it
        // scales PHYSICAL and MAGIC alike — scaling stats.baseAttack instead did neither properly:
        // derived Attack is baseAttack + primary*k1 and the primary term dominates (Hard's advertised
        // x1.15 landed as ~x1.04), while a caster like the Black Mage (baseAttack 0, damage via
        // MagicAttack) was untouched entirely. Kept separate from damageOutMultiplier because a boss
        // enrage phase OVERWRITES that field (CheckPhaseTransition), which would erase the setting.
        public float difficultyDamageMult = 1f;

        [Header("Phases")]
        public float damageOutMultiplier = 1f;   // raised by a boss enrage phase (§7.1)
        public System.Collections.Generic.List<RPGArena.Combat.BossPhase> phases;   // boss only
        private int phaseEntered = -1;

        // Threat table (boss only): cumulative damage each hero has dealt this fight. The boss's
        // single-target attacks focus the biggest damage-dealer (recorded in DamagePipeline.Apply).
        public readonly System.Collections.Generic.Dictionary<Entity, float> threatFrom = new();

        // Searing Fury (boss only): a stacking outgoing-damage escalation gained each of the boss's
        // turns and VENTED to zero whenever it is Broken. Makes Break a "use it or lose it" pressure
        // valve — ignore the Break meter and the dragon's damage runs away; spamming basics (which
        // barely build stagger) can't keep the Fury in check, so you must combo/break to survive.
        public int rageStacks;

        // Bleed off unconverted stagger at the owner's turn end. Pinned during a Break (the window
        // you earned is not clawed back) and inert wherever staggerDecayPerTurn is 0.
        public void DecayStagger(RPGArena.Combat.BattleContext ctx)
        {
            if (!isBoss || isStaggered || staggerDecayPerTurn <= 0f || staggerMeter <= 0f) return;
            float before = staggerMeter;
            staggerMeter = Mathf.Max(0f, staggerMeter - staggerDecayPerTurn);
            ctx?.Log($"    {displayName} shakes it off — Break {before:0} -> {staggerMeter:0}");
        }

        // Minions only: gold paid out when this entity dies (read by presentation for the "+Xg"
        // floater; awarded to the RunState by the battle loop). 0 for heroes and bosses.
        public int goldDrop;

        // The Evil Warrior's identity: his guard RE-SETTLES. Stagger you fail to convert bleeds away
        // each of his turns, so chip-and-turtle can never accumulate a Break — you must commit
        // pressure in a burst. 0 (the Dragon, the Black Mage) is a literal no-op.
        public float staggerDecayPerTurn;

        [Header("Stagger (bosses)")]
        public float staggerMeter;
        public float staggerThreshold = 100f;
        public bool isStaggered;
        public int staggeredTurnsRemaining;     // how long the Break window lasts

        // Runtime-only helpers (not serialized content).
        public StatusEffectContainer Status { get; private set; } = new();
        public AIBehavior Brain;                // null => player-controlled
        public int ConsecutiveMisses;           // feeds the RNG pity counter (Appendix E.1)

        [Header("Boss AI runtime state (per-battle, never on the shared AI asset)")]
        public int aiCycleIndex;                // where the boss is in its telegraphed cycle
        public Ability telegraphedAbility;      // committed to cast next turn UNLESS Broken first (§7.2)

        private BalanceConfig balance;
        private readonly Dictionary<Ability, int> cooldowns = new();

        // --- Queries ------------------------------------------------------------------
        public bool IsAlive => currentHP > 0;
        // Can act this turn unless dead, stunned/frozen, or sitting out a Break.
        public bool CanAct => IsAlive && !Status.HasControlEffect && !(isStaggered && staggeredTurnsRemaining > 0);

        // --- Derived stats (base + primary scaling + active status modifiers) ----------
        // These read the global k-constants so balancing stays in one ScriptableObject.
        public int Attack => Mathf.RoundToInt(stats.baseAttack + GetPrimary(primaryStat) * K(c => c.k1)) + Status.AttackMod;
        public int MagicAttack => Mathf.RoundToInt(stats.baseMagicAttack + stats.INT * K(c => c.k2));
        public int Defense => Mathf.Max(0, Mathf.RoundToInt(stats.baseDefense + stats.STR * K(c => c.k3)) + Status.DefenseMod);
        public int Speed => Mathf.RoundToInt(stats.baseSpeed + stats.DEX * K(c => c.k4)) + Status.SpeedMod;
        public float Accuracy => stats.baseAccuracy + stats.DEX * K(c => c.k5) + Status.AccuracyMod;
        public float Evasion => stats.baseEvasion + stats.LUK * K(c => c.k6) + stats.DEX * K(c => c.k7) + Status.EvasionMod;
        public float CritChance => stats.baseCritChance + stats.LUK * K(c => c.k8);
        public float CritDamage => stats.critDamage;

        // --- Initialisation -----------------------------------------------------------
        // Build the runtime entity from base stats + content references. Deep-copies stats.
        public void Initialize(StatBlock baseStats, BalanceConfig cfg, ElementProfile profile,
                               List<Ability> abilityList, AIBehavior brain)
        {
            balance = cfg;
            stats = baseStats != null ? baseStats.Clone() : new StatBlock();
            elementProfile = profile;
            abilities = abilityList != null ? new List<Ability>(abilityList) : new List<Ability>();
            Brain = brain;
            currentHP = stats.maxHP;
            currentMP = stats.maxMP;
            staggerMeter = 0f;
            isStaggered = false;
            damageOutMultiplier = 1f;
            phaseEntered = -1;
            Status.Clear();
            cooldowns.Clear();
            ConsecutiveMisses = 0;
            aiCycleIndex = 0;
            telegraphedAbility = null;
            rageStacks = 0;
            threatFrom.Clear();
            currentAttunement = ElementType.Ice;
        }

        // --- Combat mutators ----------------------------------------------------------
        public void TakeDamage(int amount)
        {
            currentHP = Mathf.Clamp(currentHP - Mathf.Max(0, amount), 0, stats.maxHP);
        }

        public void Heal(int amount)
        {
            currentHP = Mathf.Clamp(currentHP + Mathf.Max(0, amount), 0, stats.maxHP);
        }

        public bool TrySpendMP(int amount)
        {
            if (currentMP < amount) return false;
            currentMP -= amount;
            return true;
        }

        public void RegenMP(int amount)
        {
            currentMP = Mathf.Clamp(currentMP + Mathf.Max(0, amount), 0, stats.maxMP);
        }

        // --- Cooldowns ----------------------------------------------------------------
        public bool IsOnCooldown(Ability a) => cooldowns.TryGetValue(a, out int t) && t > 0;
        public int CooldownRemaining(Ability a) => cooldowns.TryGetValue(a, out int t) ? t : 0;
        public void StartCooldown(Ability a) { if (a != null && a.cooldown > 0) cooldowns[a] = a.cooldown; }

        // --- Turn lifecycle -----------------------------------------------------------
        // Start of this entity's turn: tick DoTs and return the total damage they dealt
        // (so the battle log can report it).
        public int TickStartOfTurn()
        {
            // Flat MP trickle each turn so refueling is a baseline, not something you must farm by
            // spamming a free basic. This restores the poke-vs-burst decision on every turn (§5.6).
            if (balance != null && balance.mpRegenPerTurn > 0) RegenMP(balance.mpRegenPerTurn);
            return Status.TickStartOfTurn(this, balance);
        }

        // Boss phases (§7.1): once HP crosses a phase threshold, apply its enrage damage
        // multiplier (each phase fires once). Both battle loops call this so the authored
        // BossPhase data is a real, shared mechanic. No-op for non-boss / phaseless entities.
        public void CheckPhaseTransition(RPGArena.Combat.BattleContext ctx)
        {
            if (!isBoss || !IsAlive || phases == null) return;
            float frac = stats.maxHP > 0 ? (float)currentHP / stats.maxHP : 0f;
            for (int i = 0; i < phases.Count; i++)
            {
                var ph = phases[i];
                if (i > phaseEntered && frac <= ph.hpThresholdPercent)
                {
                    phaseEntered = i;
                    if (ph.enrage && ph.attackMultiplier > 0f) damageOutMultiplier = ph.attackMultiplier;
                    ctx?.Log($"    *** {displayName} enters {ph.name}!  (x{ph.attackMultiplier:0.0} damage) ***");
                }
            }
        }

        // End of turn: durations decrement, cooldowns count down, the Break window shrinks.
        public void TickEndOfTurn()
        {
            Status.TickEndOfTurn(this);
            DecrementCooldowns();
            if (controlLockTurns > 0) controlLockTurns--;   // thaw ticks on the victim's OWN turns
            if (isStaggered)
            {
                staggeredTurnsRemaining--;
                if (staggeredTurnsRemaining <= 0)
                {
                    isStaggered = false;
                    staggerMeter = 0f;          // meter resets after the Break window
                }
            }
        }

        private void DecrementCooldowns()
        {
            if (cooldowns.Count == 0) return;
            // Copy keys so we can edit the dictionary while iterating.
            var keys = new List<Ability>(cooldowns.Keys);
            foreach (var a in keys)
                if (cooldowns[a] > 0) cooldowns[a]--;
        }

        // --- Helpers ------------------------------------------------------------------
        private int GetPrimary(PrimaryStat p) => p switch
        {
            PrimaryStat.STR => stats.STR,
            PrimaryStat.DEX => stats.DEX,
            PrimaryStat.INT => stats.INT,
            PrimaryStat.LUK => stats.LUK,
            _ => 0
        };

        // Safe access to a k-constant even if no BalanceConfig was wired (returns 0 scaling).
        private float K(System.Func<BalanceConfig, float> selector) => balance != null ? selector(balance) : 0f;
    }
}
