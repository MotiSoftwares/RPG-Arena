using NUnit.Framework;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;

namespace RPGArena.Tests
{
    // The SPECIAL skill's d20 risk-die (the visible gamble). Pure-pipeline tests on ComputePure.
    public class RiskDiceTests
    {
        private static DamageInfo Risky(Entity src, Entity tgt, float power, BackfireKind bk) => new DamageInfo
        {
            source = src, target = tgt, element = ElementType.Physical, basePower = power,
            isMagic = false, forceHit = true, hitTier = HitTier.Risky, rollsRiskDie = true, backfireKind = bk
        };

        // A non-risky ability ignores the risk roll entirely — the regression guard that keeps the
        // original seeded CombatTests byte-identical (they never pass a risk roll).
        [Test]
        public void RiskDie_Off_Is_Unchanged_By_RiskRoll()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { STR = 24, baseAttack = 30 }, primary: PrimaryStat.STR);
            var tgt = TestUtil.Make(cfg, new StatBlock { maxHP = 99999 }, TestUtil.Profile(), isBoss: true);
            var info = new DamageInfo { source = src, target = tgt, element = ElementType.Physical, basePower = 2f, forceHit = true, hitTier = HitTier.Standard };
            int low = DamagePipeline.ComputePure(info, cfg, 0f, 1f, 1f, 0.05f).amount;
            int high = DamagePipeline.ComputePure(info, cfg, 0f, 1f, 1f, 0.99f).amount;
            Assert.AreEqual(low, high, "rollsRiskDie=false => the risk roll must never change damage.");
            TestUtil.Destroy(src, tgt);
        }

        // Low rolls go BAD (backfire/whiff + self-damage), high rolls pay off (big/jackpot+crit),
        // and the bands are strictly ordered by payoff.
        [Test]
        public void RiskDie_Bands_Are_Ordered_And_Backfire_SelfDamages()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { STR = 24, baseAttack = 30 }, primary: PrimaryStat.STR);
            var tgt = TestUtil.Make(cfg, new StatBlock { maxHP = 99999 }, TestUtil.Profile(), isBoss: true);
            var back  = DamagePipeline.ComputePure(Risky(src, tgt, 2f, BackfireKind.SelfRecoil), cfg, 0f, 1f, 1f, 0.05f);
            var whiff = DamagePipeline.ComputePure(Risky(src, tgt, 2f, BackfireKind.SelfRecoil), cfg, 0f, 1f, 1f, 0.20f);
            var norm  = DamagePipeline.ComputePure(Risky(src, tgt, 2f, BackfireKind.SelfRecoil), cfg, 0f, 1f, 1f, 0.50f);
            var big   = DamagePipeline.ComputePure(Risky(src, tgt, 2f, BackfireKind.SelfRecoil), cfg, 0f, 1f, 1f, 0.80f);
            var jack  = DamagePipeline.ComputePure(Risky(src, tgt, 2f, BackfireKind.SelfRecoil), cfg, 0f, 1f, 1f, 0.99f);

            Assert.AreEqual(RiskBand.Backfire, back.riskBand);
            Assert.AreEqual(RiskBand.Whiff, whiff.riskBand);
            Assert.AreEqual(RiskBand.Normal, norm.riskBand);
            Assert.AreEqual(RiskBand.Big, big.riskBand);
            Assert.AreEqual(RiskBand.Jackpot, jack.riskBand);

            Assert.Less(back.amount, whiff.amount);
            Assert.Less(whiff.amount, norm.amount);
            Assert.Less(norm.amount, big.amount);
            Assert.Less(big.amount, jack.amount);

            Assert.Greater(back.selfDamage, 0, "SelfRecoil backfire must deal self-damage.");
            Assert.AreEqual(0, whiff.selfDamage, "Only a Backfire self-damages.");
            Assert.IsTrue(jack.crit, "A Jackpot forces a crit.");
            Assert.IsTrue(back.risked && back.riskFace >= 1 && back.riskFace <= 20, "The d20 face is stamped 1..20.");
            TestUtil.Destroy(src, tgt);
        }

        // riskFloor clamps the roll up — a "safe" special (the Archer's Arrow Rain) never backfires.
        [Test]
        public void RiskFloor_Prevents_A_Backfire()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { DEX = 26, baseAttack = 30 }, primary: PrimaryStat.DEX);
            var tgt = TestUtil.Make(cfg, new StatBlock { maxHP = 99999 }, TestUtil.Profile(), isBoss: true);
            var info = Risky(src, tgt, 2f, BackfireKind.EmboldenBoss); info.riskFloor = 0.5f;
            var r = DamagePipeline.ComputePure(info, cfg, 0f, 1f, 1f, 0.01f);   // rolled low, but floored to >=0.5
            Assert.AreNotEqual(RiskBand.Backfire, r.riskBand, "riskFloor must keep a safe special out of Backfire.");
            Assert.AreNotEqual(RiskBand.Whiff, r.riskBand);
            TestUtil.Destroy(src, tgt);
        }

        // --- THE STAKE (push-your-luck) ------------------------------------------------
        // Sweep the whole d20 for one stake and report what the player actually experiences.
        private struct Spread { public float avg; public int floor, ceiling, recoil; }

        private static Spread Sweep(BalanceConfig cfg, Entity src, Entity tgt, float riskFloor, float scale, float powerMult)
        {
            long total = 0; int best = 0, worst = int.MaxValue, recoil = 0;
            for (int face = 0; face < 20; face++)
            {
                var info = Risky(src, tgt, 2f * powerMult, BackfireKind.SelfRecoil);
                info.riskFloor = riskFloor;
                info.riskStakeScale = scale;
                var r = DamagePipeline.ComputePure(info, cfg, 0f, 1f, 1f, (face + 0.5f) / 20f);
                total += r.amount; recoil += r.selfDamage;
                if (r.amount > best) best = r.amount;
                if (r.amount < worst) worst = r.amount;
            }
            return new Spread { avg = total / 20f, floor = worst, ceiling = best, recoil = recoil };
        }

        // The property that makes a stake a DECISION rather than a correct answer: every measure has
        // to move monotonically with risk, so each option wins on something and loses on something.
        [Test]
        public void No_Stake_Dominates_Another()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { STR = 24, baseAttack = 30 }, primary: PrimaryStat.STR);
            var tgt = TestUtil.Make(cfg, new StatBlock { maxHP = 99999 }, TestUtil.Profile(), isBoss: true);

            var steady = Sweep(cfg, src, tgt, cfg.stakeSteadyFloor, cfg.stakeSteadyScale, cfg.stakeSteadyDamageMult);
            var press = Sweep(cfg, src, tgt, 0f, 1f, 1f);
            var allIn = Sweep(cfg, src, tgt, 0f, cfg.stakeAllInScale, 1f);

            // You are PAID for variance...
            Assert.Less(steady.avg, press.avg, "STEADY must cost expected damage — safety cannot be free, or PRESS is strictly dominated.");
            Assert.LessOrEqual(press.avg, allIn.avg + 0.01f, "ALL IN must not be worse on average than PRESS as well as riskier.");
            // ...and you pay for it at the bottom...
            Assert.Greater(steady.floor, press.floor, "STEADY must guarantee a better worst case.");
            Assert.Greater(press.floor, allIn.floor, "ALL IN must have the worst worst-case.");
            // ...and rewarded at the top.
            Assert.Less(steady.ceiling, press.ceiling, "STEADY must give up the ceiling.");
            Assert.Less(press.ceiling, allIn.ceiling, "ALL IN must buy a higher ceiling.");
            // The backfire bites harder the further you push.
            Assert.AreEqual(0, steady.recoil, "A STEADY special can never recoil.");
            Assert.Greater(allIn.recoil, press.recoil, "ALL IN must deepen the self-damage, not only the payoff.");

            TestUtil.Destroy(src, tgt);
        }

        [Test]
        public void Steady_Can_Never_Backfire_Or_Whiff()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { STR = 24, baseAttack = 30 }, primary: PrimaryStat.STR);
            var tgt = TestUtil.Make(cfg, new StatBlock { maxHP = 99999 }, TestUtil.Profile(), isBoss: true);

            for (int face = 0; face < 20; face++)
            {
                var info = Risky(src, tgt, 2f, BackfireKind.SelfRecoil);
                info.riskFloor = cfg.stakeSteadyFloor;
                info.riskStakeScale = cfg.stakeSteadyScale;
                var r = DamagePipeline.ComputePure(info, cfg, 0f, 1f, 1f, (face + 0.5f) / 20f);
                Assert.AreNotEqual(RiskBand.Backfire, r.riskBand, $"face {face + 1} backfired on STEADY.");
                Assert.AreNotEqual(RiskBand.Whiff, r.riskBand, $"face {face + 1} whiffed on STEADY.");
            }
            TestUtil.Destroy(src, tgt);
        }

        // The safety contract: an unset riskStakeScale (0) must read as 1, so every AI, every
        // headless battle and every pre-existing seeded test is byte-identical to before stakes existed.
        [Test]
        public void An_Unset_Stake_Is_Exactly_The_Authored_Die()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { STR = 24, baseAttack = 30 }, primary: PrimaryStat.STR);
            var tgt = TestUtil.Make(cfg, new StatBlock { maxHP = 99999 }, TestUtil.Profile(), isBoss: true);

            for (int face = 0; face < 20; face++)
            {
                float roll = (face + 0.5f) / 20f;
                var unset = Risky(src, tgt, 2f, BackfireKind.SelfRecoil);              // riskStakeScale left at 0
                var explicitOne = Risky(src, tgt, 2f, BackfireKind.SelfRecoil);
                explicitOne.riskStakeScale = 1f;

                var a = DamagePipeline.ComputePure(unset, cfg, 0f, 1f, 1f, roll);
                var b = DamagePipeline.ComputePure(explicitOne, cfg, 0f, 1f, 1f, roll);
                Assert.AreEqual(b.amount, a.amount, $"face {face + 1}: an unset stake must equal scale 1.");
                Assert.AreEqual(b.selfDamage, a.selfDamage, $"face {face + 1}: recoil must match too.");
            }
            TestUtil.Destroy(src, tgt);
        }
    }
}
