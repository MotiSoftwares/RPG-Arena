using System.Collections.Generic;
using UnityEngine;
using RPGArena.Characters;

namespace RPGArena.Combat.AI
{
    // A placeholder hero brain so the M1 fight can run headlessly end-to-end before the real
    // player UI exists (M3). Heuristic: heal when low, otherwise hit the boss with the
    // strongest affordable attack (preferring the boss's weakness and Break skills). Replaced
    // by genuine player input in Milestone 3.
    [CreateAssetMenu(menuName = "RPGArena/AI/Simple Hero AI", fileName = "SimpleHeroAI")]
    public class SimpleHeroAI : AIBehavior
    {
        [Range(0f, 1f)] public float healBelowHpFraction = 0.4f;

        public override Ability DecideAction(BattleContext ctx, Entity self,
                                             IReadOnlyList<Entity> opponents, out Entity target)
        {
            target = null;
            var boss = ctx.boss;

            // Heal myself if low and a heal is affordable.
            if (self.currentHP <= self.stats.maxHP * healBelowHpFraction)
            {
                var heal = self.abilities.Find(a =>
                    a != null && a.effectType == EffectType.Heal && self.currentMP >= a.mpCost && !self.IsOnCooldown(a));
                if (heal != null) { target = self; return heal; }
            }

            // Otherwise pick the best affordable damaging ability against the boss.
            Ability best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var a in self.abilities)
            {
                if (a == null) continue;
                if (a.effectType != EffectType.Attack && a.effectType != EffectType.MultiHit) continue;
                if (self.currentMP < a.mpCost || self.IsOnCooldown(a)) continue;

                // Score: power, boosted if it hits the boss's weakness or builds the Break.
                float score = a.power;
                if (boss != null && boss.elementProfile != null &&
                    boss.elementProfile.GetReaction(a.element) == ElementReaction.Weak) score += 2f;
                if (a.HasTag("BreakSkill")) score += 1f;
                if (a.mpCost == 0) score += 0.1f;     // tiny nudge toward free basics for tempo

                if (score > bestScore) { bestScore = score; best = a; }
            }

            target = boss;
            return best;   // may be null if nothing is affordable; the manager treats that as a pass
        }
    }
}
