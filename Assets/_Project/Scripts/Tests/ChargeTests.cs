using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;

namespace RPGArena.Tests
{
    // The party Valor / Overdrive charge system (the reliable win-path). Building a context WITH a
    // ChargeSystem also proves the back-compat: the other suites use MiniCtx (charge null) untouched.
    public class ChargeTests
    {
        private static BattleContext CtxWithCharge(BalanceConfig cfg)
        {
            var ctx = TestUtil.MiniCtx(cfg);
            ctx.charge = new ChargeSystem { max = cfg.valorMax };
            return ctx;
        }

        private static Ability Skill(int mp, EffectType type, TargetRule rule)
        {
            var a = ScriptableObject.CreateInstance<Ability>();
            a.displayName = "T"; a.mpCost = mp; a.effectType = type; a.targetRule = rule;
            return a;
        }

        // Coordination charges far more than spam: a combo-detonation hit awards the combo rate,
        // a plain hit barely moves it.
        [Test]
        public void Valor_Combo_Charges_Far_More_Than_A_Plain_Hit()
        {
            var cfg = TestUtil.Cfg();
            var ctx = CtxWithCharge(cfg);
            var atk = Skill(0, EffectType.Attack, TargetRule.SingleEnemy);

            ChargeSystem.AwardFor(atk, new List<DamageResult> { new DamageResult { hit = true, reaction = ElementReaction.Neutral } }, ctx);
            float plain = ctx.charge.valor;

            ctx.charge.valor = 0f;
            ChargeSystem.AwardFor(atk, new List<DamageResult> { new DamageResult { hit = true, comboDetonated = true } }, ctx);
            float combo = ctx.charge.valor;

            Assert.AreEqual(cfg.valorPerPlainHit, plain, 0.01f);
            Assert.AreEqual(cfg.valorPerComboHit, combo, 0.01f);
            Assert.Greater(combo, plain, "A combo detonation must out-charge a plain hit.");
        }

        // A setup status earns the setup rate; an ally buff the support rate.
        [Test]
        public void Setup_And_Support_Both_Charge_Valor()
        {
            var cfg = TestUtil.Cfg();
            var ctx = CtxWithCharge(cfg);

            ChargeSystem.AwardFor(Skill(8, EffectType.ApplyStatus, TargetRule.SingleEnemy), new List<DamageResult>(), ctx);
            Assert.AreEqual(cfg.valorPerSetup, ctx.charge.valor, 0.01f, "A setup status on the enemy charges Valor.");

            ctx.charge.valor = 0f;
            ChargeSystem.AwardFor(Skill(10, EffectType.Buff, TargetRule.AllAllies), new List<DamageResult>(), ctx);
            Assert.AreEqual(cfg.valorPerSupport, ctx.charge.valor, 0.01f, "An ally buff charges Valor.");
        }

        [Test]
        public void Overdrive_Only_When_Full_Surges_All_Heroes_And_Fades()
        {
            var cfg = TestUtil.Cfg();
            var ctx = CtxWithCharge(cfg);
            var hero = TestUtil.Make(cfg, new StatBlock { maxHP = 500, baseAttack = 20 }, primary: PrimaryStat.STR);
            ctx.heroes.Add(hero);

            // Not full -> spend is a no-op.
            ctx.charge.SpendOverdrive(ctx, hero);
            Assert.IsFalse(ctx.charge.overdriveActive, "Overdrive can't be spent below full.");
            Assert.AreEqual(1f, hero.damageOutMultiplier, 0.001f);

            // Fill + spend -> surge.
            ctx.charge.valor = cfg.valorMax;
            Assert.IsTrue(ctx.charge.IsFull);
            ctx.charge.SpendOverdrive(ctx, hero);
            Assert.AreEqual(0f, ctx.charge.valor, 0.001f, "Spending zeroes the meter.");
            Assert.IsTrue(ctx.charge.overdriveActive);
            Assert.AreEqual(cfg.overdriveDamageMult, hero.damageOutMultiplier, 0.001f, "Overdrive surges every hero's damage.");

            // No refill mid-surge.
            ctx.charge.Add(50f, ctx, "test");
            Assert.AreEqual(0f, ctx.charge.valor, 0.001f, "Valor can't refill during the surge.");

            // The window ticks down and restores normal damage.
            for (int i = 0; i < cfg.overdriveHeroTurns; i++) ctx.charge.ConsumeHeroTurn(ctx);
            Assert.IsFalse(ctx.charge.overdriveActive, "The surge ends after its turns.");
            Assert.AreEqual(1f, hero.damageOutMultiplier, 0.001f, "Damage restores when the surge fades.");

            TestUtil.Destroy(hero);
        }
    }
}
