using NUnit.Framework;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.Status;

namespace RPGArena.Tests
{
    // Small helpers shared by the edit-mode combat tests.
    internal static class TestUtil
    {
        public static BalanceConfig Cfg() => ScriptableObject.CreateInstance<BalanceConfig>();

        public static ElementProfile Profile(ElementType[] weak = null, ElementType[] resist = null,
            ElementType[] immune = null, ElementType[] absorb = null)
        {
            var p = ScriptableObject.CreateInstance<ElementProfile>();
            p.weakTo = weak; p.resistTo = resist; p.immuneTo = immune; p.absorbs = absorb;
            return p;
        }

        public static Entity Make(BalanceConfig cfg, StatBlock stats, ElementProfile profile = null,
            bool isBoss = false, PrimaryStat primary = PrimaryStat.INT)
        {
            var e = new GameObject("TestEntity").AddComponent<Entity>();
            e.isBoss = isBoss; e.primaryStat = primary;
            e.Initialize(stats, cfg, profile, null, null);
            return e;
        }

        public static void Destroy(params Entity[] es) { foreach (var e in es) if (e) Object.DestroyImmediate(e.gameObject); }

        public static BattleContext MiniCtx(BalanceConfig cfg) => new BattleContext
        {
            balance = cfg, rng = new System.Random(1), echoToConsole = false,
            stagger = new StaggerSystem(), turns = new TurnSystem(),
            damage = new DamagePipeline(cfg, new System.Random(2))
        };
    }

    public class CombatTests
    {
        // --- Elemental matrix (§5.4) -------------------------------------------------
        [Test]
        public void Matrix_Multipliers_Match_Config()
        {
            var cfg = TestUtil.Cfg();
            Assert.AreEqual(cfg.weakMult, ElementProfile.MultiplierFor(ElementReaction.Weak, cfg), 1e-4f);
            Assert.AreEqual(cfg.resistMult, ElementProfile.MultiplierFor(ElementReaction.Resist, cfg), 1e-4f);
            Assert.AreEqual(0f, ElementProfile.MultiplierFor(ElementReaction.Immune, cfg), 1e-4f);
            Assert.AreEqual(1f, ElementProfile.MultiplierFor(ElementReaction.Neutral, cfg), 1e-4f);
            Assert.Less(ElementProfile.MultiplierFor(ElementReaction.Absorb, cfg), 0f);   // negative heal sentinel
        }

        [Test]
        public void Profile_Priority_Absorb_Beats_Weak()
        {
            var p = TestUtil.Profile(weak: new[] { ElementType.Fire, ElementType.Ice }, absorb: new[] { ElementType.Fire });
            Assert.AreEqual(ElementReaction.Absorb, p.GetReaction(ElementType.Fire));   // absorb wins over a stray weakness
            Assert.AreEqual(ElementReaction.Weak, p.GetReaction(ElementType.Ice));
            Assert.AreEqual(ElementReaction.Neutral, p.GetReaction(ElementType.Holy));
        }

        // --- Damage pipeline, pure (§4.8 / Appendix E.1) -----------------------------
        [Test]
        public void Weakness_Hits_Harder_Than_Neutral()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { INT = 20, baseMagicAttack = 50 });
            var weakT = TestUtil.Make(cfg, new StatBlock { maxHP = 9999 }, TestUtil.Profile(weak: new[] { ElementType.Ice }), isBoss: true);
            var neutT = TestUtil.Make(cfg, new StatBlock { maxHP = 9999 }, TestUtil.Profile(), isBoss: true);

            var w = DamagePipeline.ComputePure(Info(src, weakT, ElementType.Ice), cfg, 0f, 1f, 1f);
            var n = DamagePipeline.ComputePure(Info(src, neutT, ElementType.Ice), cfg, 0f, 1f, 1f);
            Assert.AreEqual(ElementReaction.Weak, w.reaction);
            Assert.Greater(w.amount, n.amount);
            TestUtil.Destroy(src, weakT, neutT);
        }

        [Test]
        public void Absorb_Turns_Damage_Into_Heal()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { baseMagicAttack = 50 });
            var dragon = TestUtil.Make(cfg, new StatBlock { maxHP = 9999 }, TestUtil.Profile(absorb: new[] { ElementType.Fire }), isBoss: true);
            var r = DamagePipeline.ComputePure(Info(src, dragon, ElementType.Fire), cfg, 0f, 1f, 1f);
            Assert.IsTrue(r.absorbed);
            Assert.IsTrue(r.isHeal);
            Assert.Greater(r.amount, 0);
            TestUtil.Destroy(src, dragon);
        }

        [Test]
        public void Crit_Increases_Damage()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { baseMagicAttack = 50, critDamage = 2f, baseCritChance = 0.5f });
            var tgt = TestUtil.Make(cfg, new StatBlock { maxHP = 9999 }, TestUtil.Profile(), isBoss: true);
            var info = Info(src, tgt, ElementType.Physical);
            var noCrit = DamagePipeline.ComputePure(info, cfg, 0f, 1f, 1f);   // critRoll 1 > 0.5 -> no crit
            var crit = DamagePipeline.ComputePure(info, cfg, 0f, 1f, 0f);     // critRoll 0 <= 0.5 -> crit
            Assert.IsFalse(noCrit.crit);
            Assert.IsTrue(crit.crit);
            Assert.Greater(crit.amount, noCrit.amount);
            TestUtil.Destroy(src, tgt);
        }

        [Test]
        public void AutoHit_Always_Lands_But_Tiers_Roll()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { baseMagicAttack = 50, baseAccuracy = 0 });
            var tgt = TestUtil.Make(cfg, new StatBlock { maxHP = 9999, baseEvasion = 0 }, TestUtil.Profile(), isBoss: true);
            var auto     = new DamageInfo { source = src, target = tgt, element = ElementType.Physical, basePower = 1f, isMagic = true, hitTier = HitTier.Reliable, forceHit = true };
            var reliable = new DamageInfo { source = src, target = tgt, element = ElementType.Physical, basePower = 1f, isMagic = true, hitTier = HitTier.Reliable };
            var risky    = new DamageInfo { source = src, target = tgt, element = ElementType.Physical, basePower = 1f, isMagic = true, hitTier = HitTier.Risky };
            Assert.IsTrue(DamagePipeline.ComputePure(auto, cfg, 0.999f, 1f, 1f).hit, "autoHit always lands.");
            Assert.IsTrue(DamagePipeline.ComputePure(reliable, cfg, 0.1f, 1f, 1f).hit, "a reliable shot lands on a good roll.");
            Assert.IsFalse(DamagePipeline.ComputePure(reliable, cfg, 0.999f, 1f, 1f).hit, "even a reliable shot can miss on a bad roll now (real RNG).");
            Assert.IsFalse(DamagePipeline.ComputePure(risky, cfg, 0.999f, 1f, 1f).hit, "a risky shot can miss.");
            TestUtil.Destroy(src, tgt);
        }

        [Test]
        public void Higher_Defense_Reduces_Damage()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { baseMagicAttack = 50 });
            var soft = TestUtil.Make(cfg, new StatBlock { maxHP = 9999, baseDefense = 0 }, TestUtil.Profile(), isBoss: true);
            var hard = TestUtil.Make(cfg, new StatBlock { maxHP = 9999, baseDefense = 300 }, TestUtil.Profile(), isBoss: true);
            int s = DamagePipeline.ComputePure(Info(src, soft, ElementType.Physical), cfg, 0f, 1f, 1f).amount;
            int h = DamagePipeline.ComputePure(Info(src, hard, ElementType.Physical), cfg, 0f, 1f, 1f).amount;
            Assert.Greater(s, h);
            TestUtil.Destroy(src, soft, hard);
        }

        // --- Stagger & telegraph (§4.9 / §7.2) ---------------------------------------
        [Test]
        public void Stagger_Builds_And_Breaks_At_Threshold()
        {
            var cfg = TestUtil.Cfg();
            var ctx = TestUtil.MiniCtx(cfg);
            var boss = TestUtil.Make(cfg, new StatBlock { maxHP = 9999 }, TestUtil.Profile(), isBoss: true);
            boss.staggerThreshold = 50f; ctx.boss = boss; ctx.BossStaggeredTurns = 1;

            ctx.stagger.Build(boss, 30f, ctx);
            Assert.IsFalse(boss.isStaggered);
            ctx.stagger.Build(boss, 30f, ctx);              // crosses 50
            Assert.IsTrue(boss.isStaggered);
            TestUtil.Destroy(boss);
        }

        [Test]
        public void Break_Cancels_The_Telegraphed_Attack()
        {
            var cfg = TestUtil.Cfg();
            var ctx = TestUtil.MiniCtx(cfg);
            var boss = TestUtil.Make(cfg, new StatBlock { maxHP = 9999 }, TestUtil.Profile(), isBoss: true);
            boss.staggerThreshold = 10f; ctx.boss = boss;
            var flame = ScriptableObject.CreateInstance<Ability>(); flame.displayName = "Flame Breath";
            boss.telegraphedAbility = flame;                // the Dragon is mid-charge

            ctx.stagger.Build(boss, 999f, ctx);             // BREAK during the charge
            Assert.IsTrue(boss.isStaggered);
            Assert.IsNull(boss.telegraphedAbility, "Breaking the boss mid-charge must cancel its telegraphed attack (§7.2).");
            TestUtil.Destroy(boss);
        }

        // --- Cross-class synergy (§4.12 / Appendix E.3) ------------------------------
        [Test]
        public void WetIce_Forces_Freeze_And_OiledFire_Boosts()
        {
            var wet = new StatusEffectContainer();
            wet.Apply(Flag(StatusFlag.Wet));
            Assert.IsTrue(SynergyResolver.Resolve(wet, ElementType.Ice).forceStatusApply, "Wet + Ice must reliably Freeze.");

            var oiled = new StatusEffectContainer();
            oiled.Apply(Flag(StatusFlag.Oiled));
            Assert.Greater(SynergyResolver.Resolve(oiled, ElementType.Fire).damageMultiplier, 1f, "Oiled + Fire must boost fire damage.");
        }

        // Oil Bomb must have standalone value vs the Fire-ABSORBING Dragon (its Oiled+Fire combo
        // would only heal it), so Oiled+Physical builds extra stagger.
        [Test]
        public void OiledPhysical_Builds_Stagger()
        {
            var oiled = new StatusEffectContainer();
            oiled.Apply(Flag(StatusFlag.Oiled));
            Assert.Greater(SynergyResolver.Resolve(oiled, ElementType.Physical).bonusStaggerBuild, 0f,
                "Oiled + Physical must build extra stagger so Oil Bomb isn't a trap on a fire-absorbing boss.");
        }

        // The marquee Wet->Freeze->smash combo: a Frozen target's physical Shatter must bypass the
        // Dragon's Physical resist, else it nets only ~1.15x and feels like a dud (audit tier-2 #10).
        [Test]
        public void Frozen_Physical_Shatter_Bypasses_Physical_Resist()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { STR = 24, baseAttack = 30 }, primary: PrimaryStat.STR);
            var tgt = TestUtil.Make(cfg, new StatBlock { maxHP = 9999, baseDefense = 0 }, TestUtil.Profile(resist: new[] { ElementType.Physical }), isBoss: true);
            int resisted = DamagePipeline.ComputePure(Phys(src, tgt), cfg, 0f, 1f, 1f).amount;
            tgt.Status.Apply(Flag(StatusFlag.Frozen));
            var shatter = DamagePipeline.ComputePure(Phys(src, tgt), cfg, 0f, 1f, 1f);
            Assert.AreEqual(ElementReaction.Neutral, shatter.reaction, "Frozen+Physical must shatter the Physical resist.");
            Assert.Greater(shatter.amount, resisted, "A Shatter hit must out-damage a plain resisted physical hit.");
            TestUtil.Destroy(src, tgt);
        }

        // Physical classes get their OWN tempo engine: a PAID hit that detonates a setup (Shatter /
        // Marked) earns the "1 More", but a free 0-MP basic never does (audit tier-2 #4).
        [Test]
        public void Physical_Combo_Detonation_Earns_Extra_Turn_But_Free_Basic_Does_Not()
        {
            var cfg = TestUtil.Cfg();
            var src = TestUtil.Make(cfg, new StatBlock { STR = 24, baseAttack = 30 }, primary: PrimaryStat.STR);
            var tgt = TestUtil.Make(cfg, new StatBlock { maxHP = 9999 }, TestUtil.Profile(), isBoss: true);
            tgt.Status.Apply(Flag(StatusFlag.Frozen));
            var shatter = DamagePipeline.ComputePure(Phys(src, tgt), cfg, 0f, 1f, 1f);
            Assert.IsTrue(shatter.comboDetonated, "A Frozen+Physical Shatter must flag comboDetonated.");
            var results = new System.Collections.Generic.List<DamageResult> { shatter };
            var paid = ScriptableObject.CreateInstance<Ability>(); paid.mpCost = 10;
            var free = ScriptableObject.CreateInstance<Ability>(); free.mpCost = 0;
            Assert.IsTrue(TurnSystem.EarnsExtraTurn(paid, results), "A PAID physical Shatter earns the press-turn bonus.");
            Assert.IsFalse(TurnSystem.EarnsExtraTurn(free, results), "A 0-MP basic must never farm a bonus turn.");
            TestUtil.Destroy(src, tgt);
        }

        // --- Searing Fury (boss escalation vented by Break) --------------------------
        [Test]
        public void Boss_Searing_Fury_Escalates_Outgoing_Damage()
        {
            var cfg = TestUtil.Cfg();
            var dragon = TestUtil.Make(cfg, new StatBlock { STR = 24, baseAttack = 30, maxHP = 9999 }, isBoss: true, primary: PrimaryStat.STR);
            var hero = TestUtil.Make(cfg, new StatBlock { maxHP = 9999 });

            dragon.rageStacks = 0;
            var calm = DamagePipeline.ComputePure(Phys(dragon, hero), cfg, 0f, 1f, 1f);
            dragon.rageStacks = 5;
            var furious = DamagePipeline.ComputePure(Phys(dragon, hero), cfg, 0f, 1f, 1f);

            Assert.Greater(furious.amount, calm.amount, "Fury must raise the boss's outgoing damage.");
            Assert.AreEqual(calm.amount * (1f + 5 * cfg.rageDamagePerStack), furious.amount, calm.amount * 0.03f,
                "5 Fury stacks should scale damage by ~1.30x.");
            TestUtil.Destroy(dragon, hero);
        }

        [Test]
        public void Break_Vents_The_Boss_Fury_To_Zero()
        {
            var cfg = TestUtil.Cfg();
            var ctx = TestUtil.MiniCtx(cfg);
            var dragon = TestUtil.Make(cfg, new StatBlock { maxHP = 9999 }, isBoss: true);
            dragon.staggerThreshold = 50f;
            ctx.boss = dragon;
            dragon.rageStacks = 7;

            ctx.stagger.Build(dragon, 50f, ctx);   // crosses the threshold -> Break
            Assert.IsTrue(dragon.isStaggered, "Building past the threshold must Break the boss.");
            Assert.AreEqual(0, dragon.rageStacks, "A Break must VENT the boss's Searing Fury back to zero.");
            TestUtil.Destroy(dragon);
        }

        // --- Synergy web (combos the live kits can actually trigger) ------------------
        [Test]
        public void Wet_Physical_Gives_The_Physical_Trio_A_Setup_Payoff()
        {
            // Thief's Water Bomb (Wet) should make the no-Mage physical trio's blows hit harder AND
            // build extra stagger (so they can Break/vent Fury without an Ice freeze).
            var dry = new StatusEffectContainer();
            var wet = new StatusEffectContainer(); wet.Apply(Flag(StatusFlag.Wet));
            var sDry = SynergyResolver.Resolve(dry, ElementType.Physical);
            var sWet = SynergyResolver.Resolve(wet, ElementType.Physical);
            Assert.Greater(sWet.damageMultiplier, sDry.damageMultiplier, "Wet must raise physical damage.");
            Assert.Greater(sWet.bonusStaggerBuild, sDry.bonusStaggerBuild, "Wet must add stagger build for physical hits.");
        }

        [Test]
        public void Marked_Plus_Frozen_Is_Brittle_Extra_Crit()
        {
            var marked = new StatusEffectContainer(); marked.Apply(Flag(StatusFlag.Marked));
            var brittle = new StatusEffectContainer(); brittle.Apply(Flag(StatusFlag.Marked)); brittle.Apply(Flag(StatusFlag.Frozen));
            var sM = SynergyResolver.Resolve(marked, ElementType.Physical);
            var sB = SynergyResolver.Resolve(brittle, ElementType.Physical);
            Assert.Greater(sB.critChanceBonus, sM.critChanceBonus,
                "A Marked + Frozen (brittle) target must crit harder than Marked alone — rewards Mark before the freeze->shatter.");
        }

        // --- helpers -----------------------------------------------------------------
        private static DamageInfo Info(Entity src, Entity tgt, ElementType e) => new DamageInfo
        {
            source = src, target = tgt, element = e, basePower = 1f, isMagic = true, forceHit = true, hitTier = HitTier.Standard
        };

        private static DamageInfo Phys(Entity src, Entity tgt) => new DamageInfo
        {
            source = src, target = tgt, element = ElementType.Physical, basePower = 1f, isMagic = false, forceHit = true, hitTier = HitTier.Standard
        };

        private static StatusEffectDefinition Flag(StatusFlag f)
        {
            var s = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            s.flag = f; s.durationTurns = 3; s.kind = StatusKind.Flag;
            return s;
        }
    }
}
