using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat.AI;
using RPGArena.Combat.Status;

namespace RPGArena.Combat.Demo
{
    // A self-contained, code-built Mage-vs-Dragon fight used to verify the M1 combat engine
    // headlessly (it runs synchronously, so it works in the Editor without play mode). It
    // doubles as a live "the engine works" demo for the video. The real authored ScriptableObject
    // content (all four classes + three bosses) is created in later milestones; this just proves
    // the systems. Call DragonFightDemo.Run() and read the returned log.
    public static class DragonFightDemo
    {
        public static string Run(int seed = 12345, bool echoToConsole = false)
        {
            var cfg = ScriptableObject.CreateInstance<BalanceConfig>();   // default tuning values

            // --- Status effects -------------------------------------------------------
            var frozen = MakeStatus("Frozen", StatusKind.Control, StatusFlag.Frozen, 1, skipsTurn: true);
            var burn = MakeStatus("Burn", StatusKind.DoT, StatusFlag.None, 3, pctHp: 0.05f);
            var guard = MakeStatus("Tail Guard", StatusKind.Buff, StatusFlag.None, 2, defMod: 30);

            // --- Mage kit -------------------------------------------------------------
            var magicBolt = MakeAbility("Magic Bolt", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Ice, 1.2f, magic: true, mp: 0, regen: 8, tier: HitTier.Standard);
            var iceLance = MakeAbility("Ice Lance", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Ice, 1.6f, magic: true, mp: 12, tier: HitTier.Standard, tags: new[] { "BreakSkill" }, statuses: new[] { frozen });
            var fireball = MakeAbility("Fireball", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Fire, 1.6f, magic: true, mp: 12, tier: HitTier.Standard, statuses: new[] { burn });
            var heal = MakeAbility("Heal", EffectType.Heal, TargetRule.Self, ElementType.Holy, 1.4f, magic: true, mp: 14);
            var blizzard = MakeAbility("Blizzard", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Ice, 2.4f, magic: true, mp: 30, tier: HitTier.Risky, tags: new[] { "BreakSkill", "Finisher" });

            var mageProfile = ScriptableObject.CreateInstance<ElementProfile>();   // neutral
            var mageDef = ScriptableObject.CreateInstance<CharacterDefinition>();
            mageDef.className = "Mage";
            mageDef.primaryStat = PrimaryStat.INT;
            mageDef.baseStats = new StatBlock { INT = 30, LUK = 8, DEX = 10, maxHP = 220, maxMP = 200, baseMagicAttack = 12, baseDefense = 4, baseSpeed = 14, baseAccuracy = 12, baseCritChance = 0.08f, critDamage = 1.6f };
            mageDef.abilities = new List<Ability> { magicBolt, iceLance, fireball, heal, blizzard };
            mageDef.elementProfile = mageProfile;

            // --- Dragon kit -----------------------------------------------------------
            var claw = MakeAbility("Claw Swipe", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Physical, 1.3f, tier: HitTier.Standard);
            var tailSweep = MakeAbility("Tail Sweep", EffectType.Attack, TargetRule.AllEnemies, ElementType.Physical, 0.8f, autoHit: true);
            var tailGuard = MakeAbility("Tail Guard", EffectType.Buff, TargetRule.Self, ElementType.Physical, 0f, statuses: new[] { guard });
            var flameBreath = MakeAbility("Flame Breath", EffectType.Attack, TargetRule.AllEnemies, ElementType.Fire, 1.6f, autoHit: true);
            var chargingBreath = MakeAbility("Charging Breath", EffectType.BossMove, TargetRule.Self, ElementType.Fire, 0f);
            chargingBreath.telegraphsAbility = flameBreath;

            var dragonProfile = ScriptableObject.CreateInstance<ElementProfile>();
            dragonProfile.weakTo = new[] { ElementType.Ice };
            dragonProfile.absorbs = new[] { ElementType.Fire };       // burning the fire dragon HEALS it
            dragonProfile.resistTo = new[] { ElementType.Physical };

            var ai = ScriptableObject.CreateInstance<DragonCycleAI>();
            ai.clawSwipe = claw; ai.tailSweep = tailSweep; ai.tailGuard = tailGuard;
            ai.chargingBreath = chargingBreath; ai.flameBreath = flameBreath; ai.tailSweepChance = 0.2f;

            var dragonDef = ScriptableObject.CreateInstance<BossDefinition>();
            dragonDef.bossName = "The Dragon";
            dragonDef.primaryStat = PrimaryStat.STR;
            dragonDef.baseStats = new StatBlock { STR = 24, maxHP = 900, maxMP = 999, baseAttack = 30, baseDefense = 14, baseSpeed = 8, baseAccuracy = 8 };
            dragonDef.abilities = new List<Ability> { claw, tailSweep, tailGuard, chargingBreath, flameBreath };
            dragonDef.aiBehavior = ai;
            dragonDef.elementProfile = dragonProfile;
            dragonDef.staggerThreshold = 90f;
            dragonDef.staggeredTurns = 1;

            // --- Build the battle -----------------------------------------------------
            var ctx = new BattleContext
            {
                balance = cfg,
                rng = new System.Random(seed),
                echoToConsole = echoToConsole,
                BossStaggeredTurns = dragonDef.staggeredTurns,
                damage = new DamagePipeline(cfg, new System.Random(seed + 1)),
                stagger = new StaggerSystem(),
                turns = new TurnSystem()
            };

            var heroBrain = ScriptableObject.CreateInstance<SimpleHeroAI>();
            var mage = BattleSpawner.SpawnHero(mageDef, cfg, heroBrain);
            ctx.heroes.Add(mage);
            ctx.boss = BattleSpawner.SpawnBoss(dragonDef, cfg);

            var outcome = new BattleManager().RunToCompletion(ctx);

            // Clean up the throwaway combatant GameObjects so the scene stays clean.
            foreach (var h in ctx.heroes) if (h) Object.DestroyImmediate(h.gameObject);
            if (ctx.boss) Object.DestroyImmediate(ctx.boss.gameObject);

            // Build the report.
            var sb = new StringBuilder();
            int cancels = ctx.log.FindAll(l => l.Contains("CANCELLED by the Break")).Count;
            int breaks = ctx.log.FindAll(l => l.Contains("BREAK!")).Count;
            int absorbs = ctx.log.FindAll(l => l.Contains("ABSORBED")).Count;
            sb.AppendLine($"OUTCOME={outcome}  breaks={breaks}  flameBreathCancels={cancels}  fireAbsorbs={absorbs}  logLines={ctx.log.Count}");
            foreach (var line in ctx.log) sb.AppendLine(line);
            return sb.ToString();
        }

        // --- tiny content builders ----------------------------------------------------
        private static StatusEffectDefinition MakeStatus(string name, StatusKind kind, StatusFlag flag,
            int duration, bool skipsTurn = false, float pctHp = 0f, int defMod = 0)
        {
            var s = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            s.displayName = name; s.kind = kind; s.flag = flag; s.durationTurns = duration;
            s.skipsTurn = skipsTurn; s.perTurnPercentMaxHP = pctHp; s.defenseMod = defMod;
            return s;
        }

        private static Ability MakeAbility(string name, EffectType type, TargetRule rule, ElementType element,
            float power, bool magic = false, int mp = 0, int regen = 0, int hits = 1,
            bool autoHit = false, HitTier tier = HitTier.Reliable, string[] tags = null,
            StatusEffectDefinition[] statuses = null)
        {
            var a = ScriptableObject.CreateInstance<Ability>();
            a.displayName = name; a.effectType = type; a.targetRule = rule; a.element = element;
            a.power = power; a.isMagic = magic; a.mpCost = mp; a.mpRegenOnUse = regen; a.hits = hits;
            a.autoHit = autoHit; a.hitTier = tier; a.tags = tags; a.statusesToApply = statuses;
            return a;
        }
    }
}
