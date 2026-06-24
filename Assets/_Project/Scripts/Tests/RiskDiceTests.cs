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
    }
}
