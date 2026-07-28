using System.Collections.Generic;
using UnityEngine;
using RPGArena.Characters;

namespace RPGArena.Combat.AI
{
    // The minions' brain: a fixed, authored attack SEQUENCE (ability 1 → 2 → 3 → repeat) so each
    // add has a readable, learnable rotation. Targeting is a designer knob. Stateless like every
    // AIBehavior — the per-battle cycle position lives on the Entity (aiCycleIndex).
    [CreateAssetMenu(menuName = "RPGArena/AI/Sequence AI")]
    public class SequenceAI : AIBehavior
    {
        public enum TargetPick { RandomHero, LowestHpHero, RoundRobin }

        [Tooltip("How this minion picks its victim each turn.")]
        public TargetPick targeting = TargetPick.RandomHero;

        public override Ability DecideAction(BattleContext ctx, Entity self, IReadOnlyList<Entity> opponents, out Entity target)
        {
            target = null;
            var alive = new List<Entity>();
            for (int i = 0; i < opponents.Count; i++)
                if (opponents[i] != null && opponents[i].IsAlive) alive.Add(opponents[i]);
            if (alive.Count == 0 || self.abilities == null || self.abilities.Count == 0) return null;

            switch (targeting)
            {
                case TargetPick.LowestHpHero:
                    target = alive[0];
                    foreach (var h in alive) if (h.currentHP < target.currentHP) target = h;
                    break;
                case TargetPick.RoundRobin:
                    target = alive[self.aiCycleIndex % alive.Count];
                    break;
                default:
                    target = alive[ctx.rng.Next(alive.Count)];
                    break;
            }

            // Skip unaffordable steps so a drained minion still acts instead of stalling the round.
            for (int tries = 0; tries < self.abilities.Count; tries++)
            {
                var a = self.abilities[self.aiCycleIndex % self.abilities.Count];
                self.aiCycleIndex++;
                if (a != null && self.currentMP >= a.mpCost && !self.IsOnCooldown(a)) return a;
            }
            return self.abilities[0];
        }
    }
}
