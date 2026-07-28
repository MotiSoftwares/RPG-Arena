using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.AI;

namespace RPGArena.Tests
{
    // Plays many complete battles with a RANDOM brain and asserts the things that must hold no
    // matter what anyone does.
    //
    // Why this exists: the "heal the boss" bug survived three passes of careful code reading and was
    // caught in about ninety seconds by driving an actual fight and reading the log. The targeted
    // suites all check that the RIGHT play produces the right outcome; nothing was checking that a
    // WRONG play stays inside the rules. ChaoticAI picks a random legal action every turn, so it
    // aims supports and elements in ways a careful player (and every other test) never would.
    //
    // Keep the assertions to true invariants — properties that hold for every seed, every party and
    // every boss. Anything seed-dependent belongs in PartyTrioTests, which pins one seed on purpose.
    public class InvariantFuzzTests
    {
        private const string P = "Assets/_Project/ScriptableObjects/";
        private static T Load<T>(string rel) where T : Object => AssetDatabase.LoadAssetAtPath<T>(P + rel);

        private const int SeedsPerBoss = 12;

        [Test]
        public void Random_Play_Never_Breaks_The_Rules_Or_Hangs()
        {
            var cfg = Load<BalanceConfig>("Config/BalanceConfig.asset");
            var chaos = Load<AIBehavior>("AI/ChaoticAI.asset");
            var classes = new[]
            {
                Load<CharacterDefinition>("Characters/Warrior.asset"),
                Load<CharacterDefinition>("Characters/Mage.asset"),
                Load<CharacterDefinition>("Characters/Thief.asset"),
                Load<CharacterDefinition>("Characters/Archer.asset"),
            };
            Assert.IsTrue(cfg && chaos, "Authored content missing — run menu 'RPGArena/Build All Content' first.");
            foreach (var c in classes) Assert.IsNotNull(c);

            var problems = new List<string>();
            int battles = 0;

            foreach (var bossName in RunState.BossOrder)
            {
                var def = Load<BossDefinition>($"Bosses/{bossName}.asset");
                Assert.IsNotNull(def, $"Boss asset missing: {bossName}");

                for (int seed = 0; seed < SeedsPerBoss; seed++)
                {
                    var ctx = new BattleContext
                    {
                        balance = cfg, rng = new System.Random(seed * 7 + 1), echoToConsole = false,
                        BossStaggeredTurns = def.staggeredTurns,
                        damage = new DamagePipeline(cfg, new System.Random(seed * 13 + 5)),
                        stagger = new StaggerSystem(), turns = new TurnSystem(),
                        charge = new ChargeSystem { max = cfg.valorMax },
                    };
                    for (int k = 0; k < 3; k++)
                        ctx.heroes.Add(BattleSpawner.SpawnHero(classes[(seed + k) % classes.Length], cfg, chaos));
                    ctx.boss = BattleSpawner.SpawnBoss(def, cfg);

                    var outcome = new BattleManager().RunToCompletion(ctx);
                    battles++;
                    Check(ctx, outcome, $"{bossName}#{seed}", problems);

                    foreach (var h in ctx.heroes) if (h) Object.DestroyImmediate(h.gameObject);
                    if (ctx.boss) Object.DestroyImmediate(ctx.boss.gameObject);
                }
            }

            Assert.AreEqual(RunState.BossOrder.Length * SeedsPerBoss, battles, "Every boss/seed pair must have run.");
            Assert.IsEmpty(problems, "Invariant violations:\n  " + string.Join("\n  ", problems));
        }

        private static void Check(BattleContext ctx, BattleManager.Outcome outcome, string tag, List<string> problems)
        {
            var all = new List<Entity>(ctx.heroes) { ctx.boss };
            foreach (var e in all)
            {
                if (e == null) { problems.Add($"{tag} a combatant went null mid-fight"); continue; }
                if (e.currentHP > e.stats.maxHP) problems.Add($"{tag} {e.displayName} HP over max ({e.currentHP}/{e.stats.maxHP})");
                if (e.currentHP < 0) problems.Add($"{tag} {e.displayName} HP negative ({e.currentHP})");
                if (e.currentMP < 0) problems.Add($"{tag} {e.displayName} MP negative ({e.currentMP})");
                if (e.currentMP > e.stats.maxMP) problems.Add($"{tag} {e.displayName} MP over max ({e.currentMP}/{e.stats.maxMP})");
                if (e.staggerMeter < 0f) problems.Add($"{tag} {e.displayName} stagger meter negative");
            }

            if (ctx.charge.valor < 0f) problems.Add($"{tag} Valor negative");
            if (ctx.charge.valor > ctx.charge.max + 0.01f) problems.Add($"{tag} Valor over max ({ctx.charge.valor})");

            if (outcome == BattleManager.Outcome.Victory && ctx.boss.currentHP > 0)
                problems.Add($"{tag} declared Victory with the boss still alive");
            if (outcome == BattleManager.Outcome.Defeat && !ctx.AllHeroesDead && !HitSafetyCap(ctx))
                problems.Add($"{tag} declared Defeat with heroes still standing");

            foreach (var line in ctx.log)
            {
                // The 60-round cap is a backstop against a hang, not an expected ending. Random play
                // is bad, but it should still resolve one way or the other.
                if (line.Contains("safety cap")) problems.Add($"{tag} hit the 60-round safety cap");
                // The regression that started all this: a hero-cast support landing on the boss.
                if (line.Contains("healed") && line.Contains(ctx.boss.displayName))
                    problems.Add($"{tag} the boss was healed by a combatant action: {line.Trim()}");
            }
        }

        private static bool HitSafetyCap(BattleContext ctx)
        {
            foreach (var l in ctx.log) if (l.Contains("safety cap")) return true;
            return false;
        }

        // The design contract from the other direction: SpamLosesTests proves one bad STRATEGY loses;
        // this proves that playing with no strategy at all loses too, across every boss and party.
        [Test]
        public void Playing_At_Random_Never_Wins()
        {
            var cfg = Load<BalanceConfig>("Config/BalanceConfig.asset");
            var chaos = Load<AIBehavior>("AI/ChaoticAI.asset");
            var classes = new[]
            {
                Load<CharacterDefinition>("Characters/Warrior.asset"),
                Load<CharacterDefinition>("Characters/Mage.asset"),
                Load<CharacterDefinition>("Characters/Thief.asset"),
                Load<CharacterDefinition>("Characters/Archer.asset"),
            };
            Assert.IsTrue(cfg && chaos, "Authored content missing.");

            int wins = 0, battles = 0;
            foreach (var bossName in RunState.BossOrder)
            {
                var def = Load<BossDefinition>($"Bosses/{bossName}.asset");
                for (int seed = 0; seed < SeedsPerBoss; seed++)
                {
                    var ctx = new BattleContext
                    {
                        balance = cfg, rng = new System.Random(seed * 7 + 1), echoToConsole = false,
                        BossStaggeredTurns = def.staggeredTurns,
                        damage = new DamagePipeline(cfg, new System.Random(seed * 13 + 5)),
                        stagger = new StaggerSystem(), turns = new TurnSystem(),
                    };
                    for (int k = 0; k < 3; k++)
                        ctx.heroes.Add(BattleSpawner.SpawnHero(classes[(seed + k) % classes.Length], cfg, chaos));
                    ctx.boss = BattleSpawner.SpawnBoss(def, cfg);

                    if (new BattleManager().RunToCompletion(ctx) == BattleManager.Outcome.Victory) wins++;
                    battles++;

                    foreach (var h in ctx.heroes) if (h) Object.DestroyImmediate(h.gameObject);
                    if (ctx.boss) Object.DestroyImmediate(ctx.boss.gameObject);
                }
            }

            Assert.AreEqual(0, wins,
                $"Random play won {wins}/{battles} — the game is no longer demanding coordination (§ 'spamming must LOSE').");
        }
    }
}
