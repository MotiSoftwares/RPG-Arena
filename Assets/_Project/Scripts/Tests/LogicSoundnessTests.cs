using NUnit.Framework;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.Status;
using RPGArena.Combat.Commands;

namespace RPGArena.Tests
{
    // Regressions for logic holes found by an adversarial audit of the combat systems. Each of these
    // was a real, provable defect — not a hypothetical — so each gets a test that fails if it returns.
    public class LogicSoundnessTests
    {
        private static StatusEffectDefinition Frozen(int turns = 2)
        {
            var s = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            s.displayName = "Frozen"; s.flag = StatusFlag.Frozen; s.skipsTurn = true; s.durationTurns = turns;
            return s;
        }

        // Control statuses only land when a synergy forces them, and Wet+Ice is that synergy — so the
        // target has to be soaked for any of these freezes to be legal in the first place.
        private static StatusEffectDefinition Wet(int turns = 3)
        {
            var s = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            s.displayName = "Wet"; s.flag = StatusFlag.Wet; s.durationTurns = turns;
            return s;
        }

        // THE PERMA-FREEZE LOCKOUT. Frost Touch has no cooldown, costs 8 MP against 4 MP/turn regen
        // plus an 8 MP refund on the free basic, and Frozen lasts 2 of the victim's turns — so
        // re-applying it every round was MP-positive and locked a boss out of the entire fight. With
        // the phase loop handing the player free ordering, Wet -> Freeze in one phase is guaranteed.
        [Test]
        public void A_Frozen_Target_Cannot_Be_Re_Frozen_Until_It_Thaws()
        {
            var cfg = TestUtil.Cfg();
            var ctx = TestUtil.MiniCtx(cfg);
            var caster = TestUtil.Make(cfg, new StatBlock { maxHP = 150, baseMagicAttack = 20 });
            var boss = TestUtil.Make(cfg, new StatBlock { maxHP = 99999 }, isBoss: true);
            caster.team = Team.Heroes; boss.team = Team.Enemies;

            var frozen = Frozen();
            var ability = ScriptableObject.CreateInstance<Ability>();
            ability.displayName = "Ice Lance"; ability.element = ElementType.Ice;
            ability.effectType = EffectType.Attack; ability.targetRule = TargetRule.SingleEnemy;
            ability.statusesToApply = new[] { frozen };

            // Round 1: soak, then the Wet+Ice synergy forces the freeze through.
            var wet = Wet();
            boss.Status.Apply(wet);
            var cmd = CommandFactory.Build(new ActionRequest(ability, caster, boss));
            cmd.Resolve(ctx);
            Assert.IsTrue(boss.Status.Has(StatusFlag.Frozen), "The first Wet+Ice freeze must land.");
            Assert.Greater(boss.controlLockTurns, 0, "Landing a control status must start the thaw cooldown.");

            // Boss burns its two frozen turns.
            boss.TickEndOfTurn();
            boss.TickEndOfTurn();
            Assert.IsFalse(boss.Status.Has(StatusFlag.Frozen), "Frozen expires after its duration.");
            Assert.Greater(boss.controlLockTurns, 0, "...but the thaw cooldown must OUTLAST the freeze itself.");

            // The player immediately re-soaks and tries to re-freeze — this is the exploit.
            boss.Status.Apply(wet);
            CommandFactory.Build(new ActionRequest(ability, caster, boss)).Resolve(ctx);
            Assert.IsFalse(boss.Status.Has(StatusFlag.Frozen),
                "A re-freeze inside the thaw window must be refused — otherwise the boss never acts again.");

            // Once thawed, control is available again: it is a cooldown, not an immunity.
            while (boss.controlLockTurns > 0) boss.TickEndOfTurn();
            boss.Status.Apply(wet);
            CommandFactory.Build(new ActionRequest(ability, caster, boss)).Resolve(ctx);
            Assert.IsTrue(boss.Status.Has(StatusFlag.Frozen), "After thawing, the target must be freezable again.");

            TestUtil.Destroy(caster, boss);
        }

        [Test]
        public void The_Boss_Gets_Real_Turns_Under_A_Maximal_Freeze_Lock()
        {
            var cfg = TestUtil.Cfg();
            var ctx = TestUtil.MiniCtx(cfg);
            var caster = TestUtil.Make(cfg, new StatBlock { maxHP = 150, baseMagicAttack = 20 });
            var boss = TestUtil.Make(cfg, new StatBlock { maxHP = 99999 }, isBoss: true);
            caster.team = Team.Heroes; boss.team = Team.Enemies;

            var ability = ScriptableObject.CreateInstance<Ability>();
            ability.displayName = "Ice Lance"; ability.element = ElementType.Ice;
            ability.effectType = EffectType.Attack; ability.targetRule = TargetRule.SingleEnemy;
            ability.statusesToApply = new[] { Frozen() };

            var wet = Wet();
            int acted = 0;
            for (int round = 0; round < 12; round++)
            {
                boss.Status.Apply(wet);       // re-soak every round, the maximal-pressure loop
                CommandFactory.Build(new ActionRequest(ability, caster, boss)).Resolve(ctx);
                if (boss.CanAct) acted++;
                boss.TickEndOfTurn();
            }
            // Freeze 2 turns on a 4-turn cycle => ~50% uptime. Anything near zero means the lock is back.
            Assert.GreaterOrEqual(acted, 4,
                $"A boss under constant freeze pressure still acted only {acted}/12 turns — control is a lockout again.");

            TestUtil.Destroy(caster, boss);
        }

        // DIFFICULTY. Scaling stats.baseAttack was nearly a no-op (derived Attack is dominated by
        // primary*k1) and did literally nothing for a caster whose baseAttack is 0 — the Black Mage
        // dealt identical damage on Easy and Hard. The knob now lives at the damage step.
        [Test]
        public void Difficulty_Scales_Magic_Damage_Not_Just_Physical()
        {
            var cfg = TestUtil.Cfg();
            // A caster shaped like the Black Mage: no physical attack at all, damage via MagicAttack.
            var caster = TestUtil.Make(cfg, new StatBlock { maxHP = 500, baseAttack = 0, baseMagicAttack = 24, INT = 30 });
            var tgt = TestUtil.Make(cfg, new StatBlock { maxHP = 99999 }, isBoss: true);

            var info = new DamageInfo
            {
                source = caster, target = tgt, element = ElementType.Dark,
                basePower = 1.4f, isMagic = true, forceHit = true, hitTier = HitTier.Reliable
            };

            caster.difficultyDamageMult = 1f;
            int normal = DamagePipeline.ComputePure(info, cfg, 0f, 1f, 1f).amount;
            caster.difficultyDamageMult = 1.15f;
            int hard = DamagePipeline.ComputePure(info, cfg, 0f, 1f, 1f).amount;
            caster.difficultyDamageMult = 0.70f;
            int easy = DamagePipeline.ComputePure(info, cfg, 0f, 1f, 1f).amount;

            Assert.Greater(hard, normal, "HARD must actually raise a magic caster's damage.");
            Assert.Less(easy, normal, "EASY must actually lower a magic caster's damage.");
            Assert.AreEqual(normal * 1.15f, hard, normal * 0.03f, "Hard should land near the advertised x1.15.");
            Assert.AreEqual(normal * 0.70f, easy, normal * 0.03f, "Easy should land near the advertised x0.70.");

            TestUtil.Destroy(caster, tgt);
        }

        // A boss enrage phase OVERWRITES damageOutMultiplier, so difficulty could not live there.
        [Test]
        public void An_Enrage_Phase_Does_Not_Erase_The_Difficulty_Setting()
        {
            var cfg = TestUtil.Cfg();
            var boss = TestUtil.Make(cfg, new StatBlock { maxHP = 1000, baseAttack = 30, STR = 28 }, isBoss: true);
            boss.difficultyDamageMult = 1.15f;
            boss.phases = new System.Collections.Generic.List<BossPhase>
            {
                new BossPhase { name = "Enrage", hpThresholdPercent = 0.5f, enrage = true, attackMultiplier = 1.3f }
            };

            boss.TakeDamage(600);                    // drop under the threshold
            boss.CheckPhaseTransition(null);
            Assert.AreEqual(1.3f, boss.damageOutMultiplier, 0.001f, "The phase must set the enrage multiplier.");
            Assert.AreEqual(1.15f, boss.difficultyDamageMult, 0.001f,
                "...and must NOT clobber the difficulty multiplier — they are separate knobs.");

            TestUtil.Destroy(boss);
        }

        // DEFENDING was a no-op: a 1-turn status applied on the caster's own turn is stripped by that
        // same turn's end-of-turn tick, strictly before the enemy phase it exists to survive.
        [Test]
        public void Defending_Survives_The_Casters_Own_Turn_End()
        {
            var cfg = TestUtil.Cfg();
            var hero = TestUtil.Make(cfg, new StatBlock { maxHP = 320 });

            var defending = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            defending.displayName = "Defending";
            defending.flag = StatusFlag.Defending;
            defending.durationTurns = 2;             // the shipped value, post-fix

            hero.Status.Apply(defending);
            hero.TickEndOfTurn();                    // the caster's own turn ends immediately after acting
            Assert.IsTrue(hero.Status.Has(StatusFlag.Defending),
                "Defending must still be up during the enemy phase — otherwise the x0.5 mitigation never applies.");

            hero.TickEndOfTurn();                    // next round: it should now be gone
            Assert.IsFalse(hero.Status.Has(StatusFlag.Defending), "Defending must not be permanent either.");

            TestUtil.Destroy(hero);
        }
    }
}
