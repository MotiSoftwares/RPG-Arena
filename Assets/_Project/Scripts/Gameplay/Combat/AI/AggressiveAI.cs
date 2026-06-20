using System.Collections.Generic;
using UnityEngine;
using RPGArena.Characters;

namespace RPGArena.Combat.AI
{
    // The Evil Warrior's brain (§7.4): a relentless aggressor that picks your party apart. Utility
    // scoring biased ~80% to the LOWEST-HP hero, it EXECUTES a hero who drops below a threshold,
    // occasionally self-buffs, and in phase 2 ("Last Stand") switches to a multi-hit flurry. It
    // rewards Guardian Taunt / Puppet / healing and a fast Stagger to strip its armour.
    [CreateAssetMenu(menuName = "RPGArena/AI/Aggressive AI", fileName = "AggressiveAI")]
    public class AggressiveAI : AIBehavior
    {
        [Header("Moves (assign the Evil Warrior's abilities)")]
        public Ability strike;       // standard strong single-target hit
        public Ability execute;      // high-power finisher vs a low-HP hero
        public Ability selfRage;     // self attack buff (TargetRule.Self)
        public Ability flurry;       // phase-2 multi-hit assault (committed after a wind-up)
        public Ability windUpMove;   // telegraphs the flurry the turn before (break-cancellable)

        [Header("Tuning")]
        [Range(0f, 1f)] public float executeHpFraction = 0.3f;   // execute heroes below this HP %
        [Range(0f, 1f)] public float selfBuffChance = 0.22f;
        [Range(0f, 1f)] public float phase2HpFraction = 0.4f;
        [Range(0f, 1f)] public float focusLowestChance = 0.8f;   // how often it focuses the weakest

        public override Ability DecideAction(BattleContext ctx, Entity self,
                                             IReadOnlyList<Entity> opponents, out Entity target)
        {
            target = null;
            var alive = TargetingSystem.AllAlive(opponents);
            if (alive.Count == 0) return null;

            var lowest = TargetingSystem.LowestHP(opponents);
            bool phase2 = self.currentHP <= self.stats.maxHP * phase2HpFraction;

            // Commit a telegraphed Blade Flurry from last turn (unless a Break cancelled it).
            if (self.telegraphedAbility != null)
            {
                var commit = self.telegraphedAbility;
                self.telegraphedAbility = null;
                target = lowest;
                return commit;
            }

            // 1) Finish off a hero who has dropped below the execute threshold.
            if (execute != null && lowest != null && lowest.currentHP <= lowest.stats.maxHP * executeHpFraction)
            {
                target = lowest;
                return execute;
            }
            // 2) Last Stand (phase 2): wind up the relentless flurry (telegraphed).
            if (phase2 && windUpMove != null)
                return windUpMove;
            // 3) Occasionally whip itself into a rage (Self target resolves itself).
            if (selfRage != null && ctx.rng.NextDouble() < selfBuffChance)
                return selfRage;

            // 4) Otherwise strike — focus the lowest-HP hero ~80% of the time, else a random one.
            target = ctx.rng.NextDouble() < focusLowestChance ? lowest : TargetingSystem.RandomAlive(opponents, ctx.rng);
            return strike;
        }
    }
}
