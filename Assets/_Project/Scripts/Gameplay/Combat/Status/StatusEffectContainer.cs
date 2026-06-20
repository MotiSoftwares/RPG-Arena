using System.Collections.Generic;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;

namespace RPGArena.Combat.Status
{
    // Runtime bag of active statuses on one Entity. It applies stat modifiers, ticks DoTs
    // and durations, manages stacks, and answers flag/control queries (CLAUDE.md §4.10).
    // Lives in memory on each Entity; never serialized into content.
    public class StatusEffectContainer
    {
        // One active instance of a status: its definition, remaining turns, and stack count.
        public class Active
        {
            public StatusEffectDefinition def;
            public int remaining;
            public int stacks;
        }

        private readonly List<Active> active = new();
        public IReadOnlyList<Active> Effects => active;

        // --- Apply --------------------------------------------------------------------
        // Add a status, or refresh/stack an existing one of the same definition.
        public void Apply(StatusEffectDefinition def)
        {
            if (def == null) return;
            var existing = active.Find(a => a.def == def);
            if (existing != null)
            {
                existing.remaining = Mathf.Max(existing.remaining, def.durationTurns);
                if (def.stacks) existing.stacks = Mathf.Min(def.maxStacks, existing.stacks + 1);
            }
            else
            {
                active.Add(new Active { def = def, remaining = def.durationTurns, stacks = 1 });
            }
        }

        public void Clear() => active.Clear();

        // Remove a specific status (used when a stance toggle swaps Berserk <-> Guardian).
        public void Remove(StatusEffectDefinition def) => active.RemoveAll(a => a.def == def);

        // Is a specific status definition currently active?
        public bool Has(StatusEffectDefinition def) => active.Exists(a => a.def == def);

        // --- Queries ------------------------------------------------------------------
        public bool Has(StatusFlag flag)
        {
            if (flag == StatusFlag.None) return false;
            for (int i = 0; i < active.Count; i++)
                if (active[i].def.flag == flag) return true;
            return false;
        }

        // Frozen (skip-turn) is the only control effect after the E.3 trim.
        public bool HasControlEffect
        {
            get
            {
                for (int i = 0; i < active.Count; i++)
                    if (active[i].def.skipsTurn) return true;
                return false;
            }
        }

        // Aggregate stat modifiers across all active statuses (buffs usually don't stack).
        public int AttackMod => Sum(a => a.def.attackMod);
        public int DefenseMod => Sum(a => a.def.defenseMod);
        public int SpeedMod => Sum(a => a.def.speedMod);
        public float AccuracyMod => SumF(a => a.def.accuracyMod);
        public float EvasionMod => SumF(a => a.def.evasionMod);

        // --- Turn lifecycle -----------------------------------------------------------
        // Apply damage-over-time at the owner's turn start; returns NET HP lost (negative if the
        // owner was healed). The DoT respects the owner's element profile, so a Fire Burn on the
        // fire-ABSORBING Dragon heals it instead of hurting it — the central "don't burn the
        // dragon" puzzle now also holds for the burn it leaves behind (§4.11/§7.2).
        public int TickStartOfTurn(Entity owner, BalanceConfig cfg)
        {
            int net = 0;
            for (int i = 0; i < active.Count; i++)
            {
                var a = active[i];
                if (a.def.kind != StatusKind.DoT) continue;
                float dmg = (a.def.perTurnPercentMaxHP * owner.stats.maxHP + a.def.perTurnFlatDamage) * a.stacks;
                if (dmg <= 0f) continue;

                var reaction = owner.elementProfile != null
                    ? owner.elementProfile.GetReaction(a.def.dotElement) : ElementReaction.Neutral;
                float mult = cfg != null ? ElementProfile.MultiplierFor(reaction, cfg) : 1f;
                if (mult < 0f)                       // absorb: the DoT heals the owner
                {
                    int heal = Mathf.RoundToInt(dmg);
                    owner.Heal(heal); net -= heal;
                }
                else
                {
                    int rounded = Mathf.RoundToInt(dmg * mult);
                    if (rounded > 0) { owner.TakeDamage(rounded); net += rounded; }
                }
            }
            return net;
        }

        // Decrement durations at the owner's turn end and drop expired statuses.
        public void TickEndOfTurn(Entity owner)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                active[i].remaining--;
                if (active[i].remaining <= 0) active.RemoveAt(i);
            }
        }

        // --- Helpers ------------------------------------------------------------------
        private int Sum(System.Func<Active, int> sel)
        {
            int total = 0;
            for (int i = 0; i < active.Count; i++) total += sel(active[i]);
            return total;
        }

        private float SumF(System.Func<Active, float> sel)
        {
            float total = 0f;
            for (int i = 0; i < active.Count; i++) total += sel(active[i]);
            return total;
        }
    }
}
