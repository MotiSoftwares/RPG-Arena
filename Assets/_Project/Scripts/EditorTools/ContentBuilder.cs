#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.AI;
using RPGArena.Combat.Status;

namespace RPGArena.EditorTools
{
    // Authors ALL gameplay content as ScriptableObject assets in one editor run (the
    // data-driven approach, §4.1). Re-runnable: it overwrites the existing assets. The four
    // class kits and the Dragon are trimmed/tuned per CLAUDE.md §6/§7.2 + Appendix E.3.
    public static class ContentBuilder
    {
        private const string Root = "Assets/_Project/ScriptableObjects";

        [MenuItem("RPGArena/Build All Content")]
        public static void BuildAll()
        {
            EnsureFolders();

            // --- Status effects (trimmed set, Appendix E.3) ---------------------------
            var burn     = Status("Burn", StatusKind.DoT, StatusFlag.None, 3, pctHp: 0.05f, el: ElementType.Fire);
            var poison   = Status("Poison", StatusKind.DoT, StatusFlag.None, 4, flat: 30, stacks: true, maxStacks: 5);
            var oiled    = Status("Oiled", StatusKind.Flag, StatusFlag.Oiled, 3);
            var wet      = Status("Wet", StatusKind.Flag, StatusFlag.Wet, 3);
            var marked   = Status("Marked", StatusKind.Flag, StatusFlag.Marked, 3);
            var frozen   = Status("Frozen", StatusKind.Control, StatusFlag.Frozen, 1, skip: true);
            var defending= Status("Defending", StatusKind.Buff, StatusFlag.Defending, 1);
            var stealth  = Status("Stealth", StatusKind.Buff, StatusFlag.Stealthed, 1, eva: 200f);
            var rage     = Status("Rage", StatusKind.Buff, StatusFlag.None, 3, atk: 15);
            var bless    = Status("Bless", StatusKind.Buff, StatusFlag.None, 3, acc: 20f, def: 8);
            var weaken   = Status("Weaken", StatusKind.Debuff, StatusFlag.Weaken, 3, def: -12);
            var magicGuard = Status("MagicGuard", StatusKind.Buff, StatusFlag.MagicGuard, 2);
            var blind    = Status("Blind", StatusKind.Debuff, StatusFlag.None, 3, acc: -25f);
            var berserk  = Status("BerserkStance", StatusKind.Buff, StatusFlag.None, 99, atk: 20, def: -10);
            var guardian = Status("GuardianStance", StatusKind.Buff, StatusFlag.None, 99, def: 25, atk: -8);
            var tailGuard= Status("TailGuardBuff", StatusKind.Buff, StatusFlag.None, 2, def: 30);

            // --- Element profiles -----------------------------------------------------
            var neutral = Profile("Hero_Neutral");
            var dragonProfile = Profile("Dragon_Profile", weak: new[] { ElementType.Ice }, resist: new[] { ElementType.Physical }, absorb: new[] { ElementType.Fire });

            // --- WARRIOR (STR) --------------------------------------------------------
            var warrior = Character("Warrior", "Warrior", PrimaryStat.STR, neutral,
                new StatBlock { STR = 28, maxHP = 320, maxMP = 60, baseAttack = 14, baseDefense = 18, baseSpeed = 8, baseAccuracy = 6, critDamage = 1.5f },
                new[]
                {
                    Ab("Warrior_PowerStrike", "Power Strike", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Physical, 1.3f, mp: 0, regen: 3, tier: HitTier.Standard),
                    Ab("Warrior_SlashBlast", "Slash Blast", EffectType.MultiHit, TargetRule.SingleEnemy, ElementType.Physical, 0.6f, hits: 2, mp: 8, tier: HitTier.Standard, tags: Brk),
                    Ab("Warrior_Rage", "Rage", EffectType.Buff, TargetRule.AllAllies, ElementType.Physical, 0f, mp: 12, cd: 2, statuses: One(rage)),
                    Ab("Warrior_GuardianTaunt", "Guardian Taunt", EffectType.Defend, TargetRule.Self, ElementType.Physical, 0f, mp: 10, cd: 2, statuses: One(defending)),
                    Ab("Warrior_BerserkStance", "Berserk Stance", EffectType.Stance, TargetRule.Self, ElementType.Physical, 0f, mp: 0, stance: StanceAction.ToggleStatus, stanceStatuses: new[] { berserk, guardian }),
                    Ab("Warrior_CrushingBlow", "Crushing Blow", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Physical, 2.5f, mp: 25, cd: 4, tier: HitTier.Risky, tags: Brk, bonusFlag: StatusFlag.Weaken, bonusMult: 1.5f),
                });

            // --- MAGE (INT) -----------------------------------------------------------
            var mage = Character("Mage", "Mage", PrimaryStat.INT, neutral,
                new StatBlock { INT = 30, LUK = 8, maxHP = 150, maxMP = 130, baseMagicAttack = 12, baseDefense = 5, baseSpeed = 13, baseAccuracy = 12, baseCritChance = 0.08f, critDamage = 1.6f },
                new[]
                {
                    Ab("Mage_MagicBolt", "Magic Bolt", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Ice, 0.85f, magic: true, mp: 0, regen: 3, tier: HitTier.Standard, followsAttune: true),
                    Ab("Mage_Attunement", "Element Attunement", EffectType.Stance, TargetRule.Self, ElementType.Ice, 0f, mp: 0, stance: StanceAction.CycleAttunement, attuneOpts: new[] { ElementType.Fire, ElementType.Ice, ElementType.Lightning, ElementType.Holy }),
                    Ab("Mage_Fireball", "Fireball", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Fire, 1.9f, magic: true, mp: 12, tier: HitTier.Standard, statuses: One(burn)),
                    Ab("Mage_IceLance", "Ice Lance", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Ice, 1.9f, magic: true, mp: 12, tier: HitTier.Standard, tags: Brk, statuses: One(frozen)),
                    Ab("Mage_Spark", "Spark", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Lightning, 1.8f, magic: true, mp: 12, tier: HitTier.Standard, statuses: One(frozen)),
                    Ab("Mage_Heal", "Heal", EffectType.Heal, TargetRule.SingleAlly, ElementType.Holy, 0.95f, magic: true, mp: 20, cd: 2),
                    Ab("Mage_Bless", "Bless", EffectType.Buff, TargetRule.AllAllies, ElementType.Holy, 0f, mp: 12, cd: 2, statuses: One(bless)),
                    Ab("Mage_MagicGuard", "Magic Guard", EffectType.Buff, TargetRule.Self, ElementType.Holy, 0f, mp: 10, cd: 3, statuses: One(magicGuard)),
                    Ab("Mage_Blizzard", "Blizzard", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Ice, 2.5f, magic: true, mp: 30, cd: 4, tier: HitTier.Risky, tags: BrkFin),
                });

            // --- THIEF (LUK) ----------------------------------------------------------
            var thief = Character("Thief", "Thief", PrimaryStat.LUK, neutral,
                new StatBlock { LUK = 26, DEX = 14, maxHP = 190, maxMP = 100, baseAttack = 12, baseDefense = 8, baseSpeed = 16, baseAccuracy = 10, baseEvasion = 12, baseCritChance = 0.2f, critDamage = 1.7f },
                new[]
                {
                    Ab("Thief_LuckySeven", "Lucky Seven", EffectType.MultiHit, TargetRule.SingleEnemy, ElementType.Physical, 0.5f, hits: 2, mp: 0, regen: 3, tier: HitTier.Standard),
                    Ab("Thief_OilBomb", "Oil Bomb", EffectType.ApplyStatus, TargetRule.SingleEnemy, ElementType.Physical, 0f, mp: 8, cd: 1, statuses: One(oiled)),
                    Ab("Thief_WaterBomb", "Water Bomb", EffectType.ApplyStatus, TargetRule.SingleEnemy, ElementType.Physical, 0f, mp: 8, cd: 1, statuses: One(wet)),
                    Ab("Thief_ShadowMark", "Shadow Mark", EffectType.ApplyStatus, TargetRule.SingleEnemy, ElementType.Physical, 0f, mp: 6, cd: 1, statuses: One(marked)),
                    Ab("Thief_DarkSight", "Dark Sight", EffectType.Buff, TargetRule.Self, ElementType.Physical, 0f, mp: 6, cd: 2, statuses: One(stealth)),
                    Ab("Thief_SmokeBomb", "Smoke Bomb", EffectType.Debuff, TargetRule.SingleEnemy, ElementType.Physical, 0f, mp: 10, cd: 2, statuses: One(blind)),
                    Ab("Thief_Assassinate", "Assassinate", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Physical, 2.3f, mp: 24, cd: 4, tier: HitTier.Risky, tags: Fin, gCrit: true, bonusFlag: StatusFlag.Marked, bonusMult: 1.6f),
                });

            // --- ARCHER (DEX) ---------------------------------------------------------
            var archer = Character("Archer", "Archer", PrimaryStat.DEX, neutral,
                new StatBlock { DEX = 26, maxHP = 210, maxMP = 110, baseAttack = 14, baseDefense = 9, baseSpeed = 14, baseAccuracy = 22, baseCritChance = 0.12f, critDamage = 1.6f },
                new[]
                {
                    Ab("Archer_DoubleShot", "Double Shot", EffectType.MultiHit, TargetRule.SingleEnemy, ElementType.Physical, 0.7f, hits: 2, mp: 0, regen: 3, tier: HitTier.Reliable),
                    Ab("Archer_SoulArrow", "Soul Arrow", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Physical, 1.75f, mp: 10, tier: HitTier.Reliable, ignoreDef: 0.5f),
                    Ab("Archer_MarkTarget", "Mark Target", EffectType.ApplyStatus, TargetRule.SingleEnemy, ElementType.Physical, 0f, mp: 6, cd: 1, statuses: One(marked)),
                    Ab("Archer_Puppet", "Puppet", EffectType.Buff, TargetRule.Self, ElementType.Physical, 0f, mp: 12, cd: 3, statuses: One(defending)),
                    Ab("Archer_EyeOfAmazon", "Eye of Amazon", EffectType.Buff, TargetRule.AllAllies, ElementType.Physical, 0f, mp: 8, cd: 2, statuses: One(bless)),
                    Ab("Archer_ArrowRain", "Arrow Rain", EffectType.Attack, TargetRule.AllEnemies, ElementType.Physical, 2.4f, mp: 26, cd: 4, tier: HitTier.Reliable, tags: BrkFin),
                });

            // --- THE DRAGON -----------------------------------------------------------
            var dFlame = Ab("Dragon_FlameBreath", "Flame Breath", EffectType.Attack, TargetRule.AllEnemies, ElementType.Fire, 1.4f, auto: true);
            var dClaw = Ab("Dragon_ClawSwipe", "Claw Swipe", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Physical, 1.3f, tier: HitTier.Standard);
            var dSweep = Ab("Dragon_TailSweep", "Tail Sweep", EffectType.Attack, TargetRule.AllEnemies, ElementType.Physical, 0.8f, auto: true);
            var dGuard = Ab("Dragon_TailGuard", "Tail Guard", EffectType.Buff, TargetRule.Self, ElementType.Physical, 0f, statuses: One(tailGuard));
            var dCharge = Ab("Dragon_ChargingBreath", "Charging Breath", EffectType.BossMove, TargetRule.Self, ElementType.Fire, 0f, telegraphs: dFlame);

            var dragonAI = ScriptableObject.CreateInstance<DragonCycleAI>();
            dragonAI.clawSwipe = dClaw; dragonAI.tailSweep = dSweep; dragonAI.tailGuard = dGuard;
            dragonAI.chargingBreath = dCharge; dragonAI.flameBreath = dFlame; dragonAI.tailSweepChance = 0.2f;
            Save(dragonAI, $"{Root}/AI/DragonCycleAI.asset");

            var dragon = ScriptableObject.CreateInstance<BossDefinition>();
            dragon.bossName = "The Dragon"; dragon.primaryStat = PrimaryStat.STR;
            dragon.elementProfile = dragonProfile; dragon.aiBehavior = dragonAI;
            dragon.baseStats = new StatBlock { STR = 24, maxHP = 980, maxMP = 999, baseAttack = 26, baseDefense = 14, baseSpeed = 8, baseAccuracy = 6 };
            dragon.abilities = new List<Ability> { dClaw, dSweep, dGuard, dCharge, dFlame };
            dragon.staggerThreshold = 100f; dragon.staggeredTurns = 2;
            dragon.phases = new List<BossPhase> { new BossPhase { name = "Enrage", hpThresholdPercent = 0.4f, enrage = true, attackMultiplier = 1.3f } };
            Save(dragon, $"{Root}/Bosses/Dragon.asset");

            // --- THE BLACK MAGE (§7.3) ------------------------------------------------
            // Weak to Holy/Light (the Cleric school punishes it), resists Dark. A chaotic caster.
            var blackMageProfile = Profile("BlackMage_Profile", weak: new[] { ElementType.Holy }, resist: new[] { ElementType.Dark });
            var bmDarkBolt = Ab("BlackMage_DarkBolt", "Dark Bolt", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Dark, 1.4f, magic: true, tier: HitTier.Standard);
            var bmDarkNova = Ab("BlackMage_DarkNova", "Dark Nova", EffectType.Attack, TargetRule.AllEnemies, ElementType.Dark, 0.85f, magic: true, auto: true);
            var bmCurse = Ab("BlackMage_Curse", "Curse", EffectType.Debuff, TargetRule.AllEnemies, ElementType.Dark, 0f, statuses: One(blind));
            var bmHex = Ab("BlackMage_Hex", "Weakening Hex", EffectType.Debuff, TargetRule.SingleEnemy, ElementType.Dark, 0f, statuses: One(weaken));
            var bmOblivion = Ab("BlackMage_Oblivion", "Oblivion", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Dark, 2.2f, magic: true, tier: HitTier.Risky, tags: Fin);
            var bmChannel = Ab("BlackMage_Channel", "Channel Oblivion", EffectType.BossMove, TargetRule.Self, ElementType.Dark, 0f, telegraphs: bmOblivion);

            var blackMageAI = ScriptableObject.CreateInstance<ChaoticAI>();
            blackMageAI.darkBolt = bmDarkBolt; blackMageAI.darkNova = bmDarkNova; blackMageAI.curse = bmCurse;
            blackMageAI.weakenHex = bmHex; blackMageAI.oblivion = bmOblivion; blackMageAI.channelMove = bmChannel;
            Save(blackMageAI, $"{Root}/AI/ChaoticAI.asset");

            var blackMage = ScriptableObject.CreateInstance<BossDefinition>();
            blackMage.bossName = "The Black Mage"; blackMage.primaryStat = PrimaryStat.INT;
            blackMage.elementProfile = blackMageProfile; blackMage.aiBehavior = blackMageAI;
            blackMage.baseStats = new StatBlock { INT = 30, maxHP = 850, maxMP = 999, baseMagicAttack = 24, baseDefense = 10, baseSpeed = 12, baseAccuracy = 14 };
            blackMage.abilities = new List<Ability> { bmDarkBolt, bmDarkNova, bmCurse, bmHex, bmChannel, bmOblivion };
            blackMage.staggerThreshold = 110f; blackMage.staggeredTurns = 1;
            blackMage.phases = new List<BossPhase> { new BossPhase { name = "Reality Warp", hpThresholdPercent = 0.5f, enrage = true, attackMultiplier = 1.25f } };
            Save(blackMage, $"{Root}/Bosses/BlackMage.asset");

            // --- THE EVIL WARRIOR (§7.4) ----------------------------------------------
            // Heavily armoured: resists Physical, weak to Lightning (armour conducts). A focused
            // aggressor that executes the weakest hero.
            var evilProfile = Profile("EvilWarrior_Profile", weak: new[] { ElementType.Lightning }, resist: new[] { ElementType.Physical });
            var ewCleave = Ab("EvilWarrior_Cleave", "Dark Cleave", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Physical, 1.4f, tier: HitTier.Standard);
            var ewExecute = Ab("EvilWarrior_Execute", "Execute", EffectType.Attack, TargetRule.SingleEnemy, ElementType.Physical, 2.4f, tier: HitTier.Risky, tags: Fin);
            var ewRage = Ab("EvilWarrior_DarkRage", "Dark Rage", EffectType.Buff, TargetRule.Self, ElementType.Physical, 0f, statuses: One(rage));
            var ewFlurry = Ab("EvilWarrior_Flurry", "Blade Flurry", EffectType.MultiHit, TargetRule.SingleEnemy, ElementType.Physical, 0.65f, hits: 3, auto: true);
            var ewWindUp = Ab("EvilWarrior_WindUp", "Dark Wind-Up", EffectType.BossMove, TargetRule.Self, ElementType.Physical, 0f, telegraphs: ewFlurry);

            var evilAI = ScriptableObject.CreateInstance<AggressiveAI>();
            evilAI.strike = ewCleave; evilAI.execute = ewExecute; evilAI.selfRage = ewRage; evilAI.flurry = ewFlurry; evilAI.windUpMove = ewWindUp;
            Save(evilAI, $"{Root}/AI/AggressiveAI.asset");

            var evilWarrior = ScriptableObject.CreateInstance<BossDefinition>();
            evilWarrior.bossName = "The Evil Warrior"; evilWarrior.primaryStat = PrimaryStat.STR;
            evilWarrior.elementProfile = evilProfile; evilWarrior.aiBehavior = evilAI;
            evilWarrior.baseStats = new StatBlock { STR = 28, maxHP = 950, maxMP = 200, baseAttack = 30, baseDefense = 16, baseSpeed = 13, baseAccuracy = 12 };
            evilWarrior.abilities = new List<Ability> { ewCleave, ewExecute, ewRage, ewWindUp, ewFlurry };
            evilWarrior.staggerThreshold = 110f; evilWarrior.staggeredTurns = 1;
            evilWarrior.phases = new List<BossPhase> { new BossPhase { name = "Last Stand", hpThresholdPercent = 0.4f, enrage = true, attackMultiplier = 1.35f } };
            Save(evilWarrior, $"{Root}/Bosses/EvilWarrior.asset");

            // --- ROGUELITE BOONS (Appendix E.2) ---------------------------------------
            // Party-wide power-ups; the player picks 1 of 3 after each boss.
            Boon("Boon_GiantsBlood", "Giant's Blood", "Every hero gains +90 max HP.", hp: 90);
            Boon("Boon_ArcaneFont", "Arcane Font", "Every hero gains +70 max MP.", mp: 70);
            Boon("Boon_Whetstone", "Whetstone", "Sharper strikes: +7 Attack and +7 Magic Attack.", atk: 7, matk: 7);
            Boon("Boon_IronSkin", "Iron Skin", "Hardened hide: +9 Defense for all heroes.", def: 9);
            Boon("Boon_EagleEye", "Eagle Eye", "Truer aim: +10 Accuracy (fewer misses).", acc: 10f);
            Boon("Boon_KillerInstinct", "Killer Instinct", "+12% critical hit chance for the party.", crit: 0.12f);
            Boon("Boon_FleetFooted", "Fleet-Footed", "Quicker reflexes: +5 Speed (act sooner).", spd: 5);
            Boon("Boon_HeroicMight", "Heroic Might", "+4 to every primary stat (STR/DEX/INT/LUK).", str: 4, dex: 4, intel: 4, luk: 4);
            Boon("Boon_Bloodlust", "Bloodlust", "Overwhelming force: +12 Attack and +12 Magic Attack.", atk: 12, matk: 12);

            // Suppress "unused" warnings for statuses authored for later bosses/heroes.
            _ = new Object[] { poison, warrior, mage, thief, archer };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ContentBuilder] Built all content: 4 classes + 3 bosses (Dragon/Black Mage/Evil Warrior) + AI + statuses/profiles.");
        }

        // --- builders -----------------------------------------------------------------
        private static readonly string[] Brk = { "BreakSkill" };
        private static readonly string[] Fin = { "Finisher" };
        private static readonly string[] BrkFin = { "BreakSkill", "Finisher" };
        private static StatusEffectDefinition[] One(StatusEffectDefinition s) => new[] { s };

        private static StatusEffectDefinition Status(string name, StatusKind kind, StatusFlag flag, int dur,
            bool skip = false, float pctHp = 0f, int flat = 0, bool stacks = false, int maxStacks = 1,
            int atk = 0, int def = 0, float acc = 0f, float eva = 0f, ElementType el = ElementType.Physical)
        {
            var s = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            s.displayName = name; s.kind = kind; s.flag = flag; s.durationTurns = dur; s.skipsTurn = skip;
            s.perTurnPercentMaxHP = pctHp; s.perTurnFlatDamage = flat; s.stacks = stacks; s.maxStacks = maxStacks;
            s.attackMod = atk; s.defenseMod = def; s.accuracyMod = acc; s.evasionMod = eva; s.dotElement = el;
            Save(s, $"{Root}/Status/{name}.asset");
            return s;
        }

        private static ElementProfile Profile(string name, ElementType[] weak = null, ElementType[] resist = null,
            ElementType[] immune = null, ElementType[] absorb = null)
        {
            var p = ScriptableObject.CreateInstance<ElementProfile>();
            p.weakTo = weak; p.resistTo = resist; p.immuneTo = immune; p.absorbs = absorb;
            Save(p, $"{Root}/Elements/{name}.asset");
            return p;
        }

        private static Ability Ab(string file, string name, EffectType type, TargetRule rule, ElementType el, float power,
            bool magic = false, int mp = 0, int regen = 0, int hits = 1, int cd = 0, bool auto = false,
            HitTier tier = HitTier.Reliable, string[] tags = null, StatusEffectDefinition[] statuses = null,
            bool followsAttune = false, StanceAction stance = StanceAction.None, ElementType[] attuneOpts = null,
            StatusEffectDefinition[] stanceStatuses = null, Ability telegraphs = null,
            float ignoreDef = 0f, bool gCrit = false, StatusFlag bonusFlag = StatusFlag.None, float bonusMult = 1.5f,
            float execPct = 0f, float execMult = 1.6f)
        {
            var a = ScriptableObject.CreateInstance<Ability>();
            a.displayName = name; a.effectType = type; a.targetRule = rule; a.element = el; a.power = power; a.isMagic = magic;
            a.mpCost = mp; a.mpRegenOnUse = regen; a.hits = hits; a.cooldown = cd; a.autoHit = auto; a.hitTier = tier;
            a.tags = tags; a.statusesToApply = statuses; a.followsAttunement = followsAttune;
            a.stanceAction = stance; a.attunementOptions = attuneOpts; a.stanceStatuses = stanceStatuses; a.telegraphsAbility = telegraphs;
            a.ignoreDefensePercent = ignoreDef; a.guaranteedCrit = gCrit; a.bonusVsFlag = bonusFlag; a.bonusVsFlagMult = bonusMult;
            a.executeBelowHpPct = execPct; a.executeMult = execMult;
            Save(a, $"{Root}/Abilities/{file}.asset");
            return a;
        }

        private static BoonDefinition Boon(string file, string name, string desc,
            int str = 0, int dex = 0, int intel = 0, int luk = 0, int hp = 0, int mp = 0,
            int atk = 0, int matk = 0, int def = 0, int spd = 0, float acc = 0f, float crit = 0f)
        {
            var b = ScriptableObject.CreateInstance<BoonDefinition>();
            b.displayName = name; b.description = desc;
            b.strDelta = str; b.dexDelta = dex; b.intDelta = intel; b.lukDelta = luk;
            b.maxHpDelta = hp; b.maxMpDelta = mp; b.attackDelta = atk; b.magicAttackDelta = matk;
            b.defenseDelta = def; b.speedDelta = spd; b.accuracyDelta = acc; b.critChanceDelta = crit;
            Save(b, $"{Root}/Boons/{file}.asset");
            return b;
        }

        private static CharacterDefinition Character(string file, string name, PrimaryStat primary,
            ElementProfile profile, StatBlock stats, Ability[] abilities)
        {
            var c = ScriptableObject.CreateInstance<CharacterDefinition>();
            c.className = name; c.primaryStat = primary; c.elementProfile = profile; c.baseStats = stats;
            c.abilities = new List<Ability>(abilities);
            Save(c, $"{Root}/Characters/{file}.asset");
            return c;
        }

        private static void Save(Object obj, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
        }

        private static void EnsureFolders()
        {
            foreach (var sub in new[] { "Status", "Elements", "Abilities", "Characters", "Bosses", "AI", "Boons" })
                if (!AssetDatabase.IsValidFolder($"{Root}/{sub}"))
                    AssetDatabase.CreateFolder(Root, sub);
        }
    }
}
#endif
