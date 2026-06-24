using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.AI;

namespace RPGArena.Tests
{
    // The designer's headline rule: "spamming the first attack skill won't get you to win — you'll
    // die beforehand. You only win by combos / charge / breaking." This test encodes that contract:
    // a party that mindlessly mashes its cheapest basic attack (never breaking, never combing,
    // never healing) must LOSE to the every-turn, Fury-escalating dragon — while the real AI (which
    // builds stagger and Breaks) clears it (PartyTrioTests). If spam ever starts winning, the
    // anti-spam pressure (Searing Fury + stagger economy) has regressed and needs re-tuning.
    public class SpamLosesTests
    {
        private const string P = "Assets/_Project/ScriptableObjects/";
        private static T Load<T>(string rel) where T : Object => AssetDatabase.LoadAssetAtPath<T>(P + rel);

        // Always mashes the cheapest available basic ATTACK at the boss. No heals, no break skills,
        // no combo setups — the worst-practice "spam the first skill" play the design must punish.
        private class BasicSpamAI : AIBehavior
        {
            public override Ability DecideAction(BattleContext ctx, Entity self,
                                                 IReadOnlyList<Entity> opponents, out Entity target)
            {
                target = ctx.boss;
                Ability cheapest = null;
                foreach (var a in self.abilities)
                {
                    if (a == null) continue;
                    if (a.effectType != EffectType.Attack && a.effectType != EffectType.MultiHit) continue;
                    if (self.currentMP < a.mpCost || self.IsOnCooldown(a)) continue;
                    if (cheapest == null || a.mpCost < cheapest.mpCost) cheapest = a;
                }
                return cheapest;
            }
        }

        [Test]
        public void Spamming_The_Basic_Attack_Loses_To_The_Dragon()
        {
            var cfg = Load<BalanceConfig>("Config/BalanceConfig.asset");
            var w = Load<CharacterDefinition>("Characters/Warrior.asset");
            var m = Load<CharacterDefinition>("Characters/Mage.asset");
            var t = Load<CharacterDefinition>("Characters/Thief.asset");
            var dragon = Load<BossDefinition>("Bosses/Dragon.asset");
            Assert.IsTrue(cfg && w && m && t && dragon, "Authored content missing.");

            var spam = ScriptableObject.CreateInstance<BasicSpamAI>();
            var ctx = new BattleContext
            {
                balance = cfg, rng = new System.Random(99), echoToConsole = false,
                BossStaggeredTurns = dragon.staggeredTurns,
                damage = new DamagePipeline(cfg, new System.Random(100)),
                stagger = new StaggerSystem(), turns = new TurnSystem()
            };
            foreach (var def in new[] { w, m, t }) ctx.heroes.Add(BattleSpawner.SpawnHero(def, cfg, spam));
            ctx.boss = BattleSpawner.SpawnBoss(dragon, cfg);

            var outcome = new BattleManager().RunToCompletion(ctx);

            foreach (var h in ctx.heroes) if (h) Object.DestroyImmediate(h.gameObject);
            if (ctx.boss) Object.DestroyImmediate(ctx.boss.gameObject);

            Assert.AreEqual(BattleManager.Outcome.Defeat, outcome,
                "Spamming the basic attack must LOSE — winning requires combos / breaking / charge (design §). " +
                "If this passes as a Victory, the anti-spam pressure (Searing Fury / stagger) has regressed.");
        }
    }
}
