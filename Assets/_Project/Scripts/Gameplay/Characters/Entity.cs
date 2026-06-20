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

        [Header("Runtime state")]
        public StatBlock stats;                 // a COPY, never the definition's block
        public int currentHP;
        public int currentMP;
        public ElementProfile elementProfile;
        public List<Ability> abilities = new();
        public Sprite stageSprite;              // optional full-body art billboarded on the stage

        [Header("Stance / attunement (player toggles, §5.9)")]
        // Mage: which elemental school the attunement-following basic (Magic Bolt) uses.
        public ElementType currentAttunement = ElementType.Ice;

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
            Status.Clear();
            cooldowns.Clear();
            ConsecutiveMisses = 0;
            aiCycleIndex = 0;
            telegraphedAbility = null;
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
        public int TickStartOfTurn() => Status.TickStartOfTurn(this, balance);

        // End of turn: durations decrement, cooldowns count down, the Break window shrinks.
        public void TickEndOfTurn()
        {
            Status.TickEndOfTurn(this);
            DecrementCooldowns();
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
