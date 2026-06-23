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

        [Header("Tuning")]
        [Range(0f, 1f)] public float tailSweepChance = 0.2f;
        [Range(0f, 1f)] public float phase2HpFraction = 0.4f;

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

            // 2) Otherwise advance the telegraphed cycle. The Dragon is outnumbered 3:1 and a Break
            //    freezes its cycle progress, so a long rotation means the signature Charging-Breath
            //    telegraph almost never fires. Keep it a tight attack -> CHARGE -> (Flame) loop so the
            //    "break the charge / defend the breath" decision actually comes up every fight. Phase 2
            //    drops the defensive Tail Guard beat (enrage) for pure aggression.
            bool phase2 = self.currentHP <= self.stats.maxHP * phase2HpFraction;
            var cycle = phase2
                ? new List<Ability> { chargingBreath, clawSwipe }
                : new List<Ability> { chargingBreath, clawSwipe, tailGuard };

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
            if (ctx.rng.NextDouble() < 0.3) return pickable[ctx.rng.Next(pickable.Count)];
            return TargetingSystem.LowestHP(pickable);
        }
    }
}
