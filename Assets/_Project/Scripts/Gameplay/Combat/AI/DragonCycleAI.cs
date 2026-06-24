using System.Collections.Generic;
using UnityEngine;
using RPGArena.Characters;

namespace RPGArena.Combat.AI
{
    // The Dragon's brain (§4.14/§7.2): a deterministic, telegraphed cycle with light variance.
    //   Claw Swipe -> Tail Guard -> Charging Breath -> (Flame Breath next turn) -> repeat.
    // Charging Breath commits the Dragon to Flame Breath next turn UNLESS it is Broken first —
    // breaking it mid-charge cancels the attack (handled in StaggerSystem). Phase 2 (<=40% HP)
    // drops Tail Guard and shortens the cycle (enrage). All per-battle state lives on the
    // boss Entity (aiCycleIndex / telegraphedAbility), never on this shared asset.
    [CreateAssetMenu(menuName = "RPGArena/AI/Dragon Cycle AI", fileName = "DragonCycleAI")]
    public class DragonCycleAI : AIBehavior
    {
        [Header("Cycle moves (assign the Dragon's abilities)")]
        public Ability clawSwipe;
        public Ability tailSweep;
        public Ability tailGuard;
        public Ability chargingBreath;
        public Ability flameBreath;
        public Ability wingBuffet;          // a second AoE — phase-2+ pressure on a spread party

        [Header("Tuning")]
        [Range(0f, 1f)] public float tailSweepChance = 0.2f;
        [Range(0f, 1f)] public float phase2HpFraction = 0.5f;
        [Range(0f, 1f)] public float phase3HpFraction = 0.15f;

        public override Ability DecideAction(BattleContext ctx, Entity self,
                                             IReadOnlyList<Entity> opponents, out Entity target)
        {
            target = null;

            // 1) If we committed to a telegraphed attack and survived the charge un-Broken,
            //    unleash it now (Flame Breath, an AoE on all heroes).
            if (self.telegraphedAbility != null)
            {
                var commit = self.telegraphedAbility;
                self.telegraphedAbility = null;
                return commit;   // AllEnemies => the manager expands targets
            }

            // 2) Otherwise advance a telegraphed rotation that ESCALATES as the Dragon bloodies (§7.1).
            //    Three HP-gated phases give the fight a real arc instead of a flat loop:
            //      STALKING (>50%): patient — Claw, a defensive Tail Guard, the Charge->Flame telegraph,
            //                       and a Tail Sweep. The window to build Valor/Break.
            //      ENRAGED (<=50%): drops Tail Guard, adds Wing Buffet AoE and a second Charge — the
            //                       "spread party can't out-heal the AoE" pressure.
            //      FINAL FURY (<=15%): a sprint of Charge->Flame + Wing Buffet + Claw at x1.7 that
            //                       punishes a party that hasn't closed the kill.
            //    The telegraph commit/cancel path (step 1 above) is unchanged, so Break-cancels-Flame holds.
            float hpFrac = self.stats.maxHP > 0 ? (float)self.currentHP / self.stats.maxHP : 1f;
            var wing = wingBuffet != null ? wingBuffet : tailSweep;   // graceful fallback if unassigned
            List<Ability> cycle;
            if (hpFrac <= phase3HpFraction)
                cycle = new List<Ability> { chargingBreath, wing, clawSwipe };
            else if (hpFrac <= phase2HpFraction)
                cycle = new List<Ability> { clawSwipe, wing, chargingBreath, clawSwipe, chargingBreath };
            else
                cycle = new List<Ability> { clawSwipe, tailGuard, chargingBreath, tailSweep };

            int idx = ((self.aiCycleIndex % cycle.Count) + cycle.Count) % cycle.Count;
            self.aiCycleIndex = (idx + 1) % cycle.Count;
            var chosen = cycle[idx];

            // Light variance: occasionally substitute the AoE Tail Sweep for a Claw Swipe.
            if (chosen == clawSwipe && tailSweep != null && ctx.rng.NextDouble() < tailSweepChance)
                chosen = tailSweep;

            // Single-target moves now READ player state instead of firing at random; AoE/self moves
            // resolve their own targets.
            if (chosen != null && chosen.targetRule == TargetRule.SingleEnemy)
                target = PickThreatTarget(opponents, ctx);

            return chosen;
        }

        // The Dragon mostly focuses the lowest-HP hero (so "protect the squishy Mage" — Guardian
        // Taunt / Puppet / healing — finally has something to bite on), with a 30% random pick so it
        // isn't fully solvable, and it never targets a Stealthed hero (Dark Sight = untargetable).
        private static Entity PickThreatTarget(IReadOnlyList<Entity> heroes, BattleContext ctx)
        {
            var pickable = new List<Entity>();
            foreach (var h in heroes)
                if (h != null && h.IsAlive && !h.Status.Has(RPGArena.Combat.Status.StatusFlag.Stealthed))
                    pickable.Add(h);
            if (pickable.Count == 0) return TargetingSystem.RandomAlive(heroes, ctx.rng);

            // A TAUNTING hero (Warrior's Guardian Taunt) FORCES the Dragon's single-target aggro onto
            // the taunter(s) — the tank can finally peel for the squishy Mage (§6). We RESTRICT the pool
            // (not invert lethality), so total damage is unchanged and the trio-clear test still holds;
            // only WHO gets hit changes. Taunt overrides even the front/back row preference below.
            var taunters = new List<Entity>();
            foreach (var h in pickable) if (h.Status.Has(RPGArena.Combat.Status.StatusFlag.Taunting)) taunters.Add(h);
            if (taunters.Count > 0) pickable = taunters;
            // Positioning: the FRONT line draws the Dragon's single-target aggro; it only reaches the
            // back row once the front has fallen — so a tank up front shields the squishy casters.
            var front = new List<Entity>();
            foreach (var h in pickable) if (!h.backRow) front.Add(h);
            var pool = front.Count > 0 ? front : pickable;
            if (ctx.rng.NextDouble() < 0.3) return pool[ctx.rng.Next(pool.Count)];
            return TargetingSystem.LowestHP(pool);
        }
    }
}
