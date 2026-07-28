using UnityEngine;
using RPGArena.Characters;

namespace RPGArena.Combat
{
    // Applies acquired boons to a hero's RUNTIME stat block (never the shared definition) and folds
    // their rule changes into the run's RunModifiers. Called by the BattleController at fight setup
    // for every boon the run has collected, so the party grows across the gauntlet (Appendix E.2).
    public static class BoonSystem
    {
        public static void Apply(Entity hero, BoonDefinition boon)
        {
            if (hero == null || boon == null || hero.stats == null) return;
            var s = hero.stats;
            s.STR += boon.strDelta; s.DEX += boon.dexDelta; s.INT += boon.intDelta; s.LUK += boon.lukDelta;
            s.maxHP += boon.maxHpDelta; s.maxMP += boon.maxMpDelta;
            s.baseAttack += boon.attackDelta; s.baseMagicAttack += boon.magicAttackDelta;
            s.baseDefense += boon.defenseDelta; s.baseSpeed += boon.speedDelta;
            s.baseAccuracy += boon.accuracyDelta; s.baseCritChance += boon.critChanceDelta;
        }

        // Fold a boon's RULE changes into the run's modifier set. Separate from Apply because stat
        // deltas are PER HERO while rules are per RUN — calling Apply once per hero must not add the
        // same +1 Break turn three times.
        public static void ApplyRules(RunModifiers mods, BoonDefinition boon)
        {
            if (mods == null || boon == null) return;
            if (boon.freezeAlsoApplies != null) mods.freezeAlsoApplies = boon.freezeAlsoApplies;
            mods.bonusBrokenTurns += boon.bonusBrokenTurns;
            mods.bonusCoatingTurns += boon.bonusCoatingTurns;
            mods.glancesBuildFullStagger |= boon.glancesBuildFullStagger;
            mods.bonusValorPerCombo += boon.bonusValorPerCombo;
            // Two Second Wind cards don't stack into two revives — the better HP% wins.
            if (boon.secondWind)
            {
                mods.secondWindHpPercent = mods.secondWind
                    ? Mathf.Max(mods.secondWindHpPercent, boon.secondWindHpPercent)
                    : boon.secondWindHpPercent;
                mods.secondWind = true;
            }
        }

        // SECOND WIND: the first hero to fall each battle gets straight back up. Call this the moment
        // a hero is found dead and BEFORE the death is announced — a hero who stands back up never
        // "fell", so no death log line, no OnEntityDied, no death animation to un-play.
        //
        // MIRRORED INVARIANT — called from both BattleManager.CheckDeaths (headless) and
        // BattleController.CheckDeaths (live). Returns true if the hero was saved.
        public static bool TrySecondWind(Entity hero, BattleContext ctx)
        {
            var mods = ctx?.mods;
            if (mods == null || !mods.secondWind || mods.secondWindUsed) return false;
            if (hero == null || hero.IsAlive || hero.team != Team.Heroes) return false;

            mods.secondWindUsed = true;
            hero.currentHP = Mathf.Max(1, Mathf.RoundToInt(hero.stats.maxHP * mods.secondWindHpPercent));
            ctx.Log($"  {hero.displayName} refuses to fall — SECOND WIND! ({hero.currentHP} HP)");
            return true;
        }
    }
}
