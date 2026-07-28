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

        // What the dragon will do next turn, without touching its state. Its rotation is fully
        // deterministic, so the player can genuinely plan around it — which is the point: this boss
        // is a PUZZLE, and a puzzle you cannot read is just noise.
        public override Ability PreviewIntent(BattleContext ctx, Entity self,
                                              IReadOnlyList<Entity> opponents)
        {
            if (self.telegraphedAbility != null) return self.telegraphedAbility;   // already committed
            var cycle = BuildCycle(self);
            if (cycle == null || cycle.Count == 0) return null;
            int idx = ((self.aiCycleIndex % cycle.Count) + cycle.Count) % cycle.Count;
            return cycle[idx];
        }

        // The HP-gated rotation. Shared by DecideAction and PreviewIntent so the preview can never
        // drift from what actually happens.
        private List<Ability> BuildCycle(Entity self)
        {
            float hpFrac = self.stats.maxHP > 0 ? (float)self.currentHP / self.stats.maxHP : 1f;
            var wing = wingBuffet != null ? wingBuffet : tailSweep;
            var charge = chargingBreath != null ? chargingBreath : flameBreath;
            if (hpFrac <= phase3HpFraction)
                return new List<Ability> { charge, clawSwipe, charge, wing };        // FINAL FURY
            if (hpFrac <= phase2HpFraction)
                return new List<Ability> { clawSwipe, charge, wing, clawSwipe };     // ENRAGED
            return new List<Ability> { clawSwipe, tailSweep, clawSwipe, charge };    // STALKING
        }

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

            // 2) The Dragon ATTACKS EVERY TURN — no charge-up / telegraph. It mostly mauls its biggest
            //    threat with Claw, with AoE (Flame Breath / Wing Buffet / Tail Sweep) mixed in more as it
            //    bloodies. Three HP-gated rotations escalate the pressure; AoE never charges, it just hits.
            // Every rotation CHARGES once, and more often as it bloodies. The charge turn is the
            // whole point of the fight's rhythm: it announces a huge AoE one turn ahead, which the
            // party can either race to BREAK (cancelling it outright) or eat. Without it the dragon
            // was a metronome that just mauled the same hero forever — no read, no counterplay.
            var cycle = BuildCycle(self);

            int idx = ((self.aiCycleIndex % cycle.Count) + cycle.Count) % cycle.Count;
            self.aiCycleIndex = (idx + 1) % cycle.Count;
            var chosen = cycle[idx];

            // Single-target moves FOCUS the biggest damage-dealer (threat); AoE/self moves self-resolve.
            if (chosen != null && chosen.targetRule == TargetRule.SingleEnemy)
                target = PickThreatTarget(self, opponents, ctx);

            return chosen;
        }

        // The Dragon focuses the hero who has dealt it the MOST damage this fight (threat), so the big
        // damage-dealer draws the heat and the party must peel (Taunt / Stealth / heal). A 20% random
        // pick keeps it from being perfectly solvable; it never targets a Stealthed hero (Dark Sight).
        private static Entity PickThreatTarget(Entity boss, IReadOnlyList<Entity> heroes, BattleContext ctx)
        {
            var pickable = new List<Entity>();
            foreach (var h in heroes)
                if (h != null && h.IsAlive && !h.Status.Has(RPGArena.Combat.Status.StatusFlag.Stealthed))
                    pickable.Add(h);
            if (pickable.Count == 0) return TargetingSystem.RandomAlive(heroes, ctx.rng);

            // A TAUNTING hero (Guardian Taunt) FORCES aggro onto the taunter(s), overriding threat — the
            // tank can peel for the squishy Mage. Restrict the pool (lethality math unchanged).
            var taunters = new List<Entity>();
            foreach (var h in pickable) if (h.Status.Has(RPGArena.Combat.Status.StatusFlag.Taunting)) taunters.Add(h);
            if (taunters.Count > 0) pickable = taunters;

            if (ctx.rng.NextDouble() < 0.33) return pickable[ctx.rng.Next(pickable.Count)];   // some spread so a focus can't instantly delete the squishy

            // Highest cumulative damage dealt to the boss wins; a tiny front-row nudge breaks early ties
            // (before anyone has hit it) so a front-line tank still draws the first blows.
            Entity top = null; float best = -1f;
            foreach (var h in pickable)
            {
                boss.threatFrom.TryGetValue(h, out float th);
                if (!h.backRow) th += 1f;
                if (th > best) { best = th; top = h; }
            }
            return top != null ? top : pickable[0];
        }
    }
}
