using System.Collections.Generic;
using UnityEngine;
using RPGArena.Characters;

namespace RPGArena.Combat
{
    // The party's shared "Valor" meter — the reliable CHARGE win-path (the deterministic
    // counterweight to the swingy risk-die SPECIAL). It fills through COORDINATION: a weakness /
    // combo-detonation hit, a setup status on the boss, or an ally buff each charge it FAR faster
    // than spamming a free basic, so the meter is the antidote to button-mashing. When full the
    // HUD shows an OVERDRIVE button that spends it on a party-wide damage surge.
    //
    // A context SERVICE like StaggerSystem / TurnSystem — headless test contexts leave ctx.charge
    // null and every hook null-guards, so the edit-mode suite is invisible to it. The surge reuses
    // the existing Entity.damageOutMultiplier (a hero is never a boss, so no enrage conflict).
    public class ChargeSystem
    {
        public float valor;
        public float max = 100f;
        public bool overdriveActive;
        public int overdriveTurnsLeft;

        // Full and ready to unleash (can't re-charge mid-surge).
        public bool IsFull => valor >= max && !overdriveActive;

        public void Add(float amount, BattleContext ctx, string reason)
        {
            if (overdriveActive || amount <= 0f) return;     // no refill mid-surge
            valor = Mathf.Min(max, valor + amount);
            ctx?.Log($"    Valor +{Mathf.RoundToInt(amount)} ({reason}) — {Mathf.RoundToInt(valor)}/{Mathf.RoundToInt(max)}");
        }

        // What a full meter can be spent on. A single "press to win" button is not a decision —
        // three answers to three different problems is. Which one is right depends on the board:
        // are you ahead and want to close (SURGE), is the boss charging something lethal
        // (SUNDER), or did that AoE just leave the party one hit from a wipe (RALLY)?
        public enum OverdriveMode { Surge, Sunder, Rally }

        public void SpendOverdrive(BattleContext ctx, Entity caller) => SpendOverdrive(ctx, caller, OverdriveMode.Surge);

        public void SpendOverdrive(BattleContext ctx, Entity caller, OverdriveMode mode)
        {
            if (!IsFull || ctx == null) return;

            // VALIDATE BEFORE SPENDING. Sunder is the only mode that can be illegal (nothing left to
            // break), and silently converting it into a Surge would spend a full meter on something
            // the player did not ask for. Reject instead, before a single point of Valor is touched.
            if (mode == OverdriveMode.Sunder && (ctx.boss == null || !ctx.boss.IsAlive || ctx.boss.isStaggered))
                return;

            valor = 0f;
            switch (mode)
            {
                case OverdriveMode.Sunder:
                {
                    // INSTANT BREAK: skip the stagger meter entirely. The answer to a telegraphed
                    // wipe you cannot out-damage — it cancels the charge, vents Fury, and opens the
                    // burst window on your terms instead of the boss's.
                    ctx.Log("=== OVERDRIVE — SUNDER! The party breaks the boss open. ===");
                    ctx.stagger?.Break(ctx.boss, ctx);
                    break;
                }
                case OverdriveMode.Rally:
                {
                    // SECOND WIND: a big party heal that also revives one fallen hero at low HP.
                    // The comeback button — losing a hero stops being an instant spiral.
                    int healPct = ctx.balance != null ? ctx.balance.overdriveRallyHealPercent : 45;
                    ctx.Log($"=== OVERDRIVE — RALLY! The party rallies (+{healPct}% HP). ===");
                    foreach (var h in ctx.heroes)
                    {
                        if (h == null) continue;
                        if (!h.IsAlive)
                        {
                            h.currentHP = Mathf.Max(1, Mathf.RoundToInt(h.stats.maxHP * 0.25f));
                            ctx.Log($"    {h.displayName} is back on their feet ({h.currentHP} HP)!");
                        }
                        else h.Heal(Mathf.RoundToInt(h.stats.maxHP * (healPct / 100f)));
                    }
                    break;
                }
                default:
                {
                    // SURGE: the damage window. Multiplies on top of Break (x1.85) / Shatter (x2.3) /
                    // crit, so "charge up, then Shatter inside a Break" is the reliable ceiling.
                    overdriveActive = true;
                    overdriveTurnsLeft = ctx.balance != null ? ctx.balance.overdriveHeroTurns : 3;
                    float mult = ctx.balance != null ? ctx.balance.overdriveDamageMult : 1.35f;
                    foreach (var h in ctx.heroes) if (h != null && h.IsAlive) h.damageOutMultiplier = mult;
                    ctx.Log($"=== OVERDRIVE — SURGE! The party surges (x{mult:0.00} damage for {overdriveTurnsLeft} hero turns). ===");
                    break;
                }
            }
        }

        // Call after each hero's turn during a surge; closes the window (restores damage) at zero.
        public void ConsumeHeroTurn(BattleContext ctx)
        {
            if (!overdriveActive) return;
            if (--overdriveTurnsLeft <= 0)
            {
                overdriveActive = false;
                if (ctx != null)
                {
                    foreach (var h in ctx.heroes) if (h != null) h.damageOutMultiplier = 1f;
                    ctx.Log("    Overdrive fades.");
                }
            }
        }

        // THE accrual rule (mirrors TurnSystem.EarnsExtraTurn so headless + live can't drift): a paid
        // weakness/combo hit charges hard, a setup status or ally buff charges moderately, and a plain
        // hit barely moves it — so spamming the free basic fills the meter ~5-8x slower than rotating
        // setup -> detonate. Award is per ACTION (the single highest applicable category).
        public static void AwardFor(Ability used, List<DamageResult> results, BattleContext ctx)
        {
            if (ctx == null || ctx.charge == null || used == null) return;
            var cfg = ctx.balance; if (cfg == null) return;

            float gain = 0f;
            bool anyHit = false, combo = false;
            if (results != null)
                foreach (var r in results)
                    if (r.hit && !r.isHeal) { anyHit = true; if (r.reaction == ElementReaction.Weak || r.comboDetonated) combo = true; }

            // WARCRY boon adds to the combo tier only, so it rewards the coordinated line rather
            // than making the free basic charge faster (which would undo the whole accrual rule).
            if (combo) gain = Mathf.Max(gain, cfg.valorPerComboHit + (ctx.mods != null ? ctx.mods.bonusValorPerCombo : 0f));
            else if (anyHit) gain = Mathf.Max(gain, cfg.valorPerPlainHit);

            bool enemyTarget = used.targetRule == TargetRule.SingleEnemy || used.targetRule == TargetRule.AllEnemies;
            bool allyTarget = used.targetRule == TargetRule.SingleAlly || used.targetRule == TargetRule.AllAllies || used.targetRule == TargetRule.Self;
            if ((used.effectType == EffectType.ApplyStatus || used.effectType == EffectType.Debuff) && enemyTarget)
                gain = Mathf.Max(gain, cfg.valorPerSetup);
            if ((used.effectType == EffectType.Buff || used.effectType == EffectType.Heal) && allyTarget)
                gain = Mathf.Max(gain, cfg.valorPerSupport);

            if (gain > 0f) ctx.charge.Add(gain, ctx, used.displayName);
        }
    }
}
