using System.Collections.Generic;
using UnityEngine;
using RPGArena.Characters;
using RPGArena.Combat.Status;

namespace RPGArena.Combat.AI
{
    // The reference hero brain: it plays the fight the way the DESIGN says the fight should be
    // played, so the headless balance gate actually exercises the intended win-path instead of a
    // strategy no real player would use.
    //
    // It used to score purely on raw ability power, which meant setup skills (Water Bomb, Shadow
    // Mark, Mark Target) scored 0 and were NEVER chosen — measured across 12 full battles, not one
    // single synergy ever fired. The combo web that the whole game is built around was dead weight
    // in every test. Now it plays the loop: SET UP a status, then DETONATE it with the element that
    // pays off, while still healing when low and preferring the boss's weakness.
    [CreateAssetMenu(menuName = "RPGArena/AI/Simple Hero AI", fileName = "SimpleHeroAI")]
    public class SimpleHeroAI : AIBehavior
    {
        [Range(0f, 1f)] public float healBelowHpFraction = 0.4f;
        [Tooltip("Score bonus for a hit that will DETONATE a setup already on the target (Shatter/Freeze/Brittle).")]
        public float comboPayoffBonus = 6f;
        [Tooltip("Score for laying a setup status the party can cash in next turn.")]
        public float setupScore = 3.2f;

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

            var status = boss != null ? boss.Status : null;
            bool wet = status != null && status.Has(StatusFlag.Wet);
            bool frozen = status != null && status.Has(StatusFlag.Frozen);
            bool oiled = status != null && status.Has(StatusFlag.Oiled);
            bool marked = status != null && status.Has(StatusFlag.Marked);

            Ability best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var a in self.abilities)
            {
                if (a == null) continue;
                if (self.currentMP < a.mpCost || self.IsOnCooldown(a)) continue;

                bool damaging = a.effectType == EffectType.Attack || a.effectType == EffectType.MultiHit;
                float score;

                if (damaging)
                {
                    ElementType el = a.followsAttunement ? self.currentAttunement : a.element;
                    score = a.power;
                    if (boss != null && boss.elementProfile != null &&
                        boss.elementProfile.GetReaction(el) == ElementReaction.Weak) score += 2f;
                    if (a.HasTag("BreakSkill")) score += 1f;
                    if (a.mpCost == 0) score += 0.1f;     // tiny nudge toward free basics for tempo

                    // CASH IN a setup that is already on the target — this is the whole design.
                    if (frozen && el == ElementType.Physical) score += comboPayoffBonus + (marked ? 2f : 0f);  // SHATTER (x2.3, pierces resist)
                    else if (wet && el == ElementType.Ice) score += comboPayoffBonus;                          // forced FREEZE
                    else if (oiled && el == ElementType.Fire) score += comboPayoffBonus * 0.8f;                // IGNITE
                    else if (wet && el == ElementType.Physical) score += 1.5f;                                 // soaked bonus
                    else if (marked && el == ElementType.Physical) score += 1.2f;
                }
                else if (a.effectType == EffectType.ApplyStatus && a.statusesToApply != null && a.statusesToApply.Length > 0)
                {
                    // LAY a setup — only worth a turn if it isn't already up and someone can pay it off.
                    var lays = a.statusesToApply[0];
                    if (lays == null) continue;
                    bool alreadyUp = status != null && lays.flag != StatusFlag.None && status.Has(lays.flag);
                    if (alreadyUp) continue;
                    score = setupScore;
                    // Wet is the keystone (it opens Freeze -> Shatter), so it's worth the most.
                    if (lays.flag == StatusFlag.Wet) score += 1.4f;
                    if (lays.flag == StatusFlag.Marked) score += 0.4f;
                }
                else continue;   // buffs/stances/defend: not modelled by the reference brain

                if (score > bestScore) { bestScore = score; best = a; }
            }

            target = boss;
            return best;   // may be null if nothing is affordable; the manager treats that as a pass
        }
    }
}
