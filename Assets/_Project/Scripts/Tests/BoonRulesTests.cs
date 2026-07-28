using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.Status;

namespace RPGArena.Tests
{
    // The rule-changing boons (RunModifiers) and the run-attrition layer.
    //
    // These carry a contract the rest of the suite depends on: a context that never sets `mods` must
    // behave EXACTLY as it did before RunModifiers existed. The whole 29-test suite is that
    // regression check, so the job here is to prove each rule actually fires when it IS set, and
    // that the vanilla path is genuinely untouched rather than accidentally always-on.
    public class BoonRulesTests
    {
        private static StatusEffectDefinition Status(string name, StatusFlag flag, int turns)
        {
            var s = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            s.displayName = name; s.flag = flag; s.durationTurns = turns;
            return s;
        }

        private static BoonDefinition Boon() => ScriptableObject.CreateInstance<BoonDefinition>();

        // --- the safety contract -------------------------------------------------------
        [Test]
        public void A_Context_Without_Mods_Is_Vanilla()
        {
            var cfg = TestUtil.Cfg();
            var ctx = TestUtil.MiniCtx(cfg);
            Assert.IsNull(ctx.mods, "MiniCtx must never opt into run modifiers — the whole EditMode suite relies on it.");

            // The pipeline's optional mods parameter defaults to null, and a null mods must not
            // change a single branch. A graze still builds REDUCED stagger.
            var src = TestUtil.Make(cfg, new StatBlock { maxHP = 100, baseAttack = 20 });
            var boss = TestUtil.Make(cfg, new StatBlock { maxHP = 5000 }, isBoss: true);
            var info = new DamageInfo { source = src, target = boss, element = ElementType.Physical, basePower = 1f };

            var graze = ForceGraze(info, cfg, null);
            Assert.IsTrue(graze.glanced, "Test setup: the roll must produce a graze, not a clean miss.");
            Assert.Less(graze.staggerBuilt, cfg.staggerBuildNormalHit,
                "With no boons a graze must still build REDUCED stagger.");

            TestUtil.Destroy(src, boss);
        }

        // Pick a hit roll that lands in the graze band: above the hit chance, but within
        // cleanMissOvershoot of it so it grazes instead of whiffing outright.
        private static DamageResult ForceGraze(DamageInfo info, BalanceConfig cfg, RunModifiers mods)
        {
            var clean = DamagePipeline.ComputePure(info, cfg, 0f, 1f, 1f);
            float justOver = clean.hitChance + (1f - clean.hitChance) * (cfg.cleanMissOvershoot * 0.5f);
            return DamagePipeline.ComputePure(info, cfg, justOver, 1f, 1f, 1f, mods);
        }

        // --- Duelist's Eye -------------------------------------------------------------
        [Test]
        public void DuelistsEye_Makes_A_Graze_Build_Full_Stagger()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { maxHP = 100, baseAttack = 20 });
            var boss = TestUtil.Make(cfg, new StatBlock { maxHP = 5000 }, isBoss: true);
            var info = new DamageInfo { source = src, target = boss, element = ElementType.Physical, basePower = 1f };

            var without = ForceGraze(info, cfg, null);
            var with = ForceGraze(info, cfg, new RunModifiers { glancesBuildFullStagger = true });

            Assert.IsTrue(without.glanced && with.glanced, "Both runs must be grazes for the comparison to mean anything.");
            Assert.Greater(with.staggerBuilt, without.staggerBuilt, "Duelist's Eye must raise a graze's stagger build.");
            Assert.AreEqual(cfg.staggerBuildNormalHit, with.staggerBuilt, 0.01f, "...specifically to the FULL amount.");
            // The damage is still reduced — the boon buys progress toward a Break, not damage.
            Assert.AreEqual(without.amount, with.amount, "Duelist's Eye must not change graze DAMAGE.");

            TestUtil.Destroy(src, boss);
        }

        // --- Tarpits -------------------------------------------------------------------
        [Test]
        public void Tarpits_Extends_A_Coating_Instead_Of_Merely_Refreshing_It()
        {
            var wet = Status("Wet", StatusFlag.Wet, 3);
            var bag = new StatusEffectContainer();
            bag.Apply(wet);

            // Re-applying only refreshes to the base duration — this is exactly why the boon extends.
            bag.Apply(wet);
            Assert.AreEqual(3, bag.Effects[0].remaining, "Re-applying a status only refreshes it to its base duration.");

            bag.ExtendByFlag(StatusFlag.Wet, 2);
            Assert.AreEqual(5, bag.Effects[0].remaining, "Tarpits must ADD turns on top of the base duration.");

            // Extending a flag nobody carries is a no-op, not an exception.
            bag.ExtendByFlag(StatusFlag.Oiled, 2);
            Assert.AreEqual(1, bag.Effects.Count);
        }

        // --- Warcry --------------------------------------------------------------------
        [Test]
        public void Warcry_Boosts_Combo_Valor_But_Not_The_Plain_Hit()
        {
            var cfg = TestUtil.Cfg();
            var atk = ScriptableObject.CreateInstance<Ability>();
            atk.displayName = "T"; atk.effectType = EffectType.Attack; atk.targetRule = TargetRule.SingleEnemy;

            var ctx = TestUtil.MiniCtx(cfg);
            ctx.charge = new ChargeSystem { max = cfg.valorMax };
            ctx.mods = new RunModifiers { bonusValorPerCombo = 7f };

            ChargeSystem.AwardFor(atk, new List<DamageResult> { new DamageResult { hit = true, comboDetonated = true } }, ctx);
            Assert.AreEqual(cfg.valorPerComboHit + 7f, ctx.charge.valor, 0.01f, "Warcry must add to the COMBO tier.");

            // The anti-spam accrual rule must survive the boon: a plain hit is unaffected.
            ctx.charge.valor = 0f;
            ChargeSystem.AwardFor(atk, new List<DamageResult> { new DamageResult { hit = true, reaction = ElementReaction.Neutral } }, ctx);
            Assert.AreEqual(cfg.valorPerPlainHit, ctx.charge.valor, 0.01f,
                "Warcry must NOT speed up the free basic — that would undo the anti-spam accrual rule.");
        }

        // --- Second Wind ---------------------------------------------------------------
        [Test]
        public void SecondWind_Saves_A_Hero_Exactly_Once_Per_Battle()
        {
            var cfg = TestUtil.Cfg();
            var ctx = TestUtil.MiniCtx(cfg);
            ctx.mods = new RunModifiers { secondWind = true, secondWindHpPercent = 0.35f };

            var a = TestUtil.Make(cfg, new StatBlock { maxHP = 400 });
            var b = TestUtil.Make(cfg, new StatBlock { maxHP = 400 });
            a.team = Team.Heroes; b.team = Team.Heroes;

            a.TakeDamage(9999);
            Assert.IsFalse(a.IsAlive, "Test setup: the hero must actually be down.");
            Assert.IsTrue(BoonSystem.TrySecondWind(a, ctx), "The FIRST hero to fall is saved.");
            Assert.IsTrue(a.IsAlive);
            Assert.AreEqual(140, a.currentHP, "...and comes back at 35% of max.");

            b.TakeDamage(9999);
            Assert.IsFalse(BoonSystem.TrySecondWind(b, ctx), "The SECOND hero to fall is not saved.");
            Assert.IsFalse(b.IsAlive);

            TestUtil.Destroy(a, b);
        }

        [Test]
        public void SecondWind_Never_Saves_An_Enemy()
        {
            var cfg = TestUtil.Cfg();
            var ctx = TestUtil.MiniCtx(cfg);
            ctx.mods = new RunModifiers { secondWind = true };

            var boss = TestUtil.Make(cfg, new StatBlock { maxHP = 400 }, isBoss: true);
            boss.team = Team.Enemies;
            boss.TakeDamage(9999);

            Assert.IsFalse(BoonSystem.TrySecondWind(boss, ctx), "A boon must never resurrect the boss.");
            Assert.IsFalse(boss.IsAlive);
            TestUtil.Destroy(boss);
        }

        // --- accumulation --------------------------------------------------------------
        [Test]
        public void Rules_Accumulate_Across_Boons_But_SecondWind_Does_Not_Stack()
        {
            var mods = new RunModifiers();

            var a = Boon(); a.bonusBrokenTurns = 1; a.bonusCoatingTurns = 2; a.bonusValorPerCombo = 7f;
            var b = Boon(); b.bonusBrokenTurns = 1; b.glancesBuildFullStagger = true;
            var c = Boon(); c.secondWind = true; c.secondWindHpPercent = 0.35f;
            var d = Boon(); d.secondWind = true; d.secondWindHpPercent = 0.50f;

            foreach (var boon in new[] { a, b, c, d }) BoonSystem.ApplyRules(mods, boon);

            Assert.AreEqual(2, mods.bonusBrokenTurns, "Numeric rules add up across cards.");
            Assert.AreEqual(2, mods.bonusCoatingTurns);
            Assert.AreEqual(7f, mods.bonusValorPerCombo, 0.01f);
            Assert.IsTrue(mods.glancesBuildFullStagger);
            Assert.IsTrue(mods.secondWind);
            Assert.AreEqual(0.50f, mods.secondWindHpPercent, 0.01f,
                "Two Second Winds are one revive at the BETTER percentage, not two revives.");
        }

        [Test]
        public void IsRule_Separates_Rule_Cards_From_Stat_Cards()
        {
            var stat = Boon(); stat.attackDelta = 12; stat.maxHpDelta = 90;
            Assert.IsFalse(stat.IsRule, "A pure stat card is not a rule card.");

            var rule = Boon(); rule.bonusBrokenTurns = 1;
            Assert.IsTrue(rule.IsRule, "RunFlow guarantees one of these per draft — IsRule must detect it.");
        }

        // --- attrition -----------------------------------------------------------------
        [Test]
        public void Retry_Snapshot_Rewinds_Wounds_Gold_And_Spent_Items()
        {
            var run = new RunState();
            run.gold = 500;
            run.SetCarry("Warrior", 0.62f, 0.80f);
            run.AddItem("Item_Bomb");

            run.SnapshotForRetry();   // the moment the player walks into the boss

            // ...the fight goes badly: potions burned, gold spent, everyone wrecked.
            run.gold = 20;
            run.RemoveItem("Item_Bomb");
            run.SetCarry("Warrior", 0f, 0f);

            run.RestoreRetrySnapshot();

            Assert.AreEqual(500, run.gold, "A retry restores the gold the player entered with.");
            Assert.AreEqual(1, run.CountItem("Item_Bomb"), "...and the consumables they had not yet used.");
            Assert.IsTrue(run.TryGetCarry("Warrior", out float hp, out float mp));
            Assert.AreEqual(0.62f, hp, 0.001f, "...and their wounds as of entering, not as of dying.");
            Assert.AreEqual(0.80f, mp, 0.001f);
        }

        [Test]
        public void Resting_Heals_The_Wounded_And_Never_Downgrades_The_Healthy()
        {
            var run = new RunState();
            run.SetCarry("Warrior", 0.30f, 0.40f);
            run.SetCarry("Mage", 0.95f, 1.00f);

            run.RestParty(0.85f);

            run.TryGetCarry("Warrior", out float wHp, out float wMp);
            run.TryGetCarry("Mage", out float mHp, out float mMp);

            Assert.AreEqual(0.85f, wHp, 0.001f, "The wounded hero is brought up to the rest fraction.");
            Assert.AreEqual(0.85f, wMp, 0.001f);
            Assert.AreEqual(0.95f, mHp, 0.001f, "A healthier hero must never be knocked DOWN by resting.");
            Assert.AreEqual(1.00f, mMp, 0.001f);
        }

        [Test]
        public void Rest_Price_Climbs_So_The_Camp_Is_Not_An_Infinite_Fountain()
        {
            var run = new RunState();
            int first = run.RestCost;
            run.restsPurchased++;
            int second = run.RestCost;

            Assert.Greater(second, first, "Each rest must cost more than the last.");
        }

        // --- target side clamps --------------------------------------------------------
        // Found by driving a LIVE battle and watching the log say "Mage uses Heal -> The Dragon
        // healed 70". SingleAlly used to return whatever target it was handed, so one bad reference
        // anywhere healed the boss. Both battle loops now clamp through TargetingSystem.
        [Test]
        public void A_Support_Ability_Can_Never_Resolve_Onto_The_Enemy()
        {
            var cfg = TestUtil.Cfg();
            var mage = TestUtil.Make(cfg, new StatBlock { maxHP = 150 });
            var ally = TestUtil.Make(cfg, new StatBlock { maxHP = 320 });
            var boss = TestUtil.Make(cfg, new StatBlock { maxHP = 2500 }, isBoss: true);
            mage.team = Team.Heroes; ally.team = Team.Heroes; boss.team = Team.Enemies;

            Assert.AreSame(mage, TargetingSystem.ClampToAlly(mage, boss),
                "A heal aimed at the boss must fall back to the caster, never touch the boss.");
            Assert.AreSame(ally, TargetingSystem.ClampToAlly(mage, ally), "A real ally is still targetable.");
            Assert.AreSame(mage, TargetingSystem.ClampToAlly(mage, null), "No target given falls back to the caster.");

            TestUtil.Destroy(mage, ally, boss);
        }

        [Test]
        public void An_Attack_Can_Never_Resolve_Onto_Your_Own_Side()
        {
            var cfg = TestUtil.Cfg();
            var hero = TestUtil.Make(cfg, new StatBlock { maxHP = 320 });
            var ally = TestUtil.Make(cfg, new StatBlock { maxHP = 150 });
            var boss = TestUtil.Make(cfg, new StatBlock { maxHP = 2500 }, isBoss: true);
            hero.team = Team.Heroes; ally.team = Team.Heroes; boss.team = Team.Enemies;

            Assert.IsNull(TargetingSystem.ClampToEnemy(hero, ally),
                "A same-side proposal must return null so the caller re-picks a legal target.");
            Assert.AreSame(boss, TargetingSystem.ClampToEnemy(hero, boss), "A real enemy passes through.");
            // ...and from the enemy's side of the fence, symmetrically.
            Assert.AreSame(hero, TargetingSystem.ClampToEnemy(boss, hero));
            Assert.IsNull(TargetingSystem.ClampToEnemy(boss, boss));

            TestUtil.Destroy(hero, ally, boss);
        }

        [Test]
        public void A_Revive_Can_Still_Target_A_Fallen_Ally()
        {
            var cfg = TestUtil.Cfg();
            var healer = TestUtil.Make(cfg, new StatBlock { maxHP = 150 });
            var downed = TestUtil.Make(cfg, new StatBlock { maxHP = 320 });
            healer.team = Team.Heroes; downed.team = Team.Heroes;
            downed.TakeDamage(9999);

            Assert.AreSame(downed, TargetingSystem.ClampToAlly(healer, downed),
                "The side clamp must not filter out the dead — revives target the fallen.");

            TestUtil.Destroy(healer, downed);
        }

        [Test]
        public void A_New_Run_Carries_Nothing_Forward()
        {
            var run = new RunState();
            run.SetCarry("Warrior", 0.3f, 0.3f);
            run.gold = 900; run.startValor = 30f; run.restsPurchased = 4;
            run.SnapshotForRetry();

            run.Reset();

            Assert.AreEqual(0, run.carryHp.Count, "A fresh run starts undamaged.");
            Assert.AreEqual(0, run.gold);
            Assert.AreEqual(0f, run.startValor, 0.001f);
            Assert.AreEqual(0, run.restsPurchased);

            // A stale snapshot from the previous run must not be able to resurrect its gold.
            run.RestoreRetrySnapshot();
            Assert.AreEqual(0, run.gold, "Reset must invalidate the previous run's retry snapshot.");
        }
    }
}
