using System.Collections.Generic;
using UnityEngine;
using RPGArena.Characters;

namespace RPGArena.Combat.AI
{
    // The Black Mage's brain (§7.3): high-variance chaos that punishes a static plan. It favours
    // party-wide DEBUFFS (a curse that blinds, a weakening hex) and occasional Dark nukes, with a
    // small chance of a devastating blast in phase 2 ("Reality Warp"). It rewards cleansing/
    // accuracy buffs (Bless, Eye of Amazon) and the Archer's can't-miss shots. All randomness
    // reads ctx.rng so a battle stays reproducible.
    [CreateAssetMenu(menuName = "RPGArena/AI/Chaotic AI", fileName = "ChaoticAI")]
    public class ChaoticAI : AIBehavior
    {
        [Header("Moves (assign the Black Mage's abilities)")]
        public Ability darkBolt;     // single-target Dark nuke
        public Ability darkNova;     // AoE Dark on the whole party
        public Ability curse;        // AllEnemies debuff (Blind)
        public Ability weakenHex;    // single-target debuff (Weaken)
        public Ability oblivion;     // rare, powerful phase-2 nuke (committed after channeling)
        public Ability channelMove;  // telegraphs Oblivion the turn before (break-cancellable)

        [Header("Tuning")]
        [Range(0f, 1f)] public float phase2HpFraction = 0.5f;

        public override Ability DecideAction(BattleContext ctx, Entity self,
                                             IReadOnlyList<Entity> opponents, out Entity target)
        {
            target = null;
            var alive = TargetingSystem.AllAlive(opponents);
            if (alive.Count == 0) return null;

            // Commit a telegraphed Oblivion from last turn (unless a Break already cancelled it).
            if (self.telegraphedAbility != null)
            {
                var commit = self.telegraphedAbility;
                self.telegraphedAbility = null;
                target = TargetingSystem.RandomAlive(opponents, ctx.rng);
                return commit;
            }

            bool phase2 = self.currentHP <= self.stats.maxHP * phase2HpFraction;
            double r = ctx.rng.NextDouble();

            // Phase 2 "Reality Warp": channel the big nuke (telegraphed, break-cancellable).
            if (phase2 && channelMove != null && r < 0.22)
                return channelMove;
            // Otherwise a high-variance spread: nuke / curse / nova / hex.
            if (darkBolt != null && r < 0.32) { target = TargetingSystem.RandomAlive(opponents, ctx.rng); return darkBolt; }
            if (curse != null && r < 0.52) return curse;            // AllEnemies => resolves itself
            if (darkNova != null && r < 0.72) return darkNova;      // AllEnemies
            if (weakenHex != null && r < 0.86) { target = TargetingSystem.RandomAlive(opponents, ctx.rng); return weakenHex; }

            // Fallback: always a valid action so the loop can't deadlock.
            target = TargetingSystem.RandomAlive(opponents, ctx.rng);
            return darkBolt != null ? darkBolt : darkNova;
        }
    }
}
