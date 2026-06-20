using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.AI;

namespace RPGArena.Tests
{
    // Regression coverage for the authored boss roster (§16.1): a full four-hero party must be
    // able to drive every authored boss to a terminal outcome (no hang/exception). This exercises
    // the real ScriptableObject content + each boss's Strategy AI (DragonCycle/Chaotic/Aggressive)
    // through the synchronous engine. Editor-only (loads authored assets via AssetDatabase).
    public class BossRosterTests
    {
        private const string SO = "Assets/_Project/ScriptableObjects/";

        [TestCase("Dragon")]
        [TestCase("BlackMage")]
        [TestCase("EvilWarrior")]
        public void Authored_Boss_Resolves_Against_Full_Party(string bossName)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<BalanceConfig>(SO + "Config/BalanceConfig.asset");
            Assert.IsNotNull(cfg, "BalanceConfig missing");
            var bossDef = AssetDatabase.LoadAssetAtPath<BossDefinition>(SO + "Bosses/" + bossName + ".asset");
            Assert.IsNotNull(bossDef, "Boss asset missing: " + bossName);
            Assert.IsNotNull(bossDef.aiBehavior, "Boss has no AI brain: " + bossName);

            var ctx = new BattleContext
            {
                balance = cfg, rng = new System.Random(3), echoToConsole = false,
                BossStaggeredTurns = bossDef.staggeredTurns,
                damage = new DamagePipeline(cfg, new System.Random(4)),
                stagger = new StaggerSystem(), turns = new TurnSystem(),
            };
            foreach (var hn in new[] { "Warrior", "Mage", "Thief", "Archer" })
            {
                var hd = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(SO + "Characters/" + hn + ".asset");
                Assert.IsNotNull(hd, "Hero asset missing: " + hn);
                var brain = ScriptableObject.CreateInstance<SimpleHeroAI>();
                ctx.heroes.Add(BattleSpawner.SpawnHero(hd, cfg, brain));
            }
            ctx.boss = BattleSpawner.SpawnBoss(bossDef, cfg);

            var outcome = new BattleManager().RunToCompletion(ctx);

            foreach (var h in ctx.heroes) if (h) Object.DestroyImmediate(h.gameObject);
            if (ctx.boss) Object.DestroyImmediate(ctx.boss.gameObject);

            Assert.That(outcome,
                Is.EqualTo(BattleManager.Outcome.Victory).Or.EqualTo(BattleManager.Outcome.Defeat),
                "Fight against " + bossName + " must resolve to a terminal outcome (no hang).");
        }
    }
}
