using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.AI;

namespace RPGArena.Tests
{
    // Validates the §6.5 design rule: ALL FOUR party trios (of the 4 classes, pick 3) must be
    // able to clear the Dragon — including the no-Mage physical trio. Loads the authored
    // ScriptableObject content and runs each trio to completion with the placeholder hero AI.
    public class PartyTrioTests
    {
        private const string P = "Assets/_Project/ScriptableObjects/";

        private static T Load<T>(string rel) where T : Object => AssetDatabase.LoadAssetAtPath<T>(P + rel);

        [Test]
        public void All_Four_Trios_Can_Clear_The_Dragon()
        {
            var cfg = Load<BalanceConfig>("Config/BalanceConfig.asset");
            var w = Load<CharacterDefinition>("Characters/Warrior.asset");
            var m = Load<CharacterDefinition>("Characters/Mage.asset");
            var t = Load<CharacterDefinition>("Characters/Thief.asset");
            var a = Load<CharacterDefinition>("Characters/Archer.asset");
            var dragon = Load<BossDefinition>("Bosses/Dragon.asset");
            Assert.IsTrue(cfg && w && m && t && a && dragon,
                "Authored content missing — run menu 'RPGArena/Build All Content' first.");

            var trios = new[]
            {
                new { name = "Warrior+Mage+Thief",  defs = new[] { w, m, t } },
                new { name = "Warrior+Mage+Archer",  defs = new[] { w, m, a } },
                new { name = "Warrior+Thief+Archer", defs = new[] { w, t, a } },   // no Mage / no heal
                new { name = "Mage+Thief+Archer",    defs = new[] { m, t, a } },
            };

            foreach (var trio in trios)
            {
                var outcome = RunFight(trio.defs, cfg, dragon);
                Assert.AreEqual(BattleManager.Outcome.Victory, outcome,
                    $"Trio '{trio.name}' must be able to clear the Dragon (§6.5).");
            }
        }

        private static BattleManager.Outcome RunFight(CharacterDefinition[] defs, BalanceConfig cfg, BossDefinition dragon)
        {
            var heroAI = ScriptableObject.CreateInstance<SimpleHeroAI>();
            var ctx = new BattleContext
            {
                balance = cfg, rng = new System.Random(99), echoToConsole = false,
                BossStaggeredTurns = dragon.staggeredTurns,
                damage = new DamagePipeline(cfg, new System.Random(100)),
                stagger = new StaggerSystem(), turns = new TurnSystem()
            };
            foreach (var def in defs) ctx.heroes.Add(BattleSpawner.SpawnHero(def, cfg, heroAI));
            ctx.boss = BattleSpawner.SpawnBoss(dragon, cfg);

            var outcome = new BattleManager().RunToCompletion(ctx);

            foreach (var h in ctx.heroes) if (h) Object.DestroyImmediate(h.gameObject);
            if (ctx.boss) Object.DestroyImmediate(ctx.boss.gameObject);
            return outcome;
        }
    }
}
