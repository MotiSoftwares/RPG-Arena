using UnityEngine;
using RPGArena.Core;
using RPGArena.Combat.Status;

namespace RPGArena.Combat
{
    // The single source of truth for damage (§4.8). The COMPUTE half is pure and
    // deterministic given explicit rolls, so it is unit-testable without a scene (§16.1);
    // the APPLY half has the side effects (mutates HP, builds stagger, raises events).
    public class DamagePipeline
    {
        private readonly BalanceConfig cfg;
        private readonly System.Random rng;

        public DamagePipeline(BalanceConfig cfg, System.Random rng = null)
        {
            this.cfg = cfg;
            this.rng = rng ?? new System.Random();
        }

        // Live path: draws the three rolls (with the pity guard) and computes.
        public DamageResult Compute(DamageInfo info)
        {
            // Pity (Appendix E.1): after too many misses in a row, force the next hit.
            if (info.source != null && info.source.ConsecutiveMisses >= cfg.missStreakCap)
                info.forceHit = true;

            float hitRoll = (float)rng.NextDouble();
            float damageRoll = Mathf.Lerp(cfg.damageVarianceMin, cfg.damageVarianceMax, (float)rng.NextDouble());
            float critRoll = (float)rng.NextDouble();
            // Only the SPECIAL draws the risk die — gated so non-special hits consume no extra rng
            // (keeps seeded headless runs stable until a risky ability actually fires). The riskFloor
            // clamp lives in ComputePure so it stays deterministic + unit-testable.
            float riskRoll = info.rollsRiskDie ? (float)rng.NextDouble() : 1f;
            return ComputePure(info, cfg, hitRoll, damageRoll, critRoll, riskRoll);
        }

        // PURE: deterministic given explicit rolls. hitRoll/critRoll in [0,1); damageRoll is the
        // already-chosen variance multiplier. riskRoll in [0,1) drives the SPECIAL's risk die and
        // defaults to 1f (a no-op band) so every non-special call is byte-identical. §4.8 order.
        public static DamageResult ComputePure(DamageInfo info, BalanceConfig cfg,
                                               float hitRoll, float damageRoll, float critRoll, float riskRoll = 1f)
        {
            var r = new DamageResult { source = info.source, target = info.target, ability = info.ability, element = info.element, hit = true, damageRoll = damageRoll };
            var src = info.source;
            var tgt = info.target;

            // 1) HIT CHECK (Appendix E.1). Auto-hit/forceHit skips it; a Broken target can't evade.
            float tierBase = info.hitTier switch
            {
                HitTier.Reliable => cfg.reliableHitBase,
                HitTier.Standard => cfg.standardHitBase,
                HitTier.Risky => cfg.riskyHitBase,
                _ => cfg.reliableHitBase
            };
            float acc = src != null ? src.Accuracy : 0f;
            float eva = (tgt != null && !tgt.isStaggered) ? tgt.Evasion : 0f;
            float hitChance = Mathf.Clamp(tierBase + (acc - eva) * cfg.accuracyToPercent, cfg.hitFloor, cfg.hitCeiling);
            // Reliable abilities (basics, AoE, the Archer) NEVER miss (Appendix E.1), as do
            // explicit auto-hit / forced (pity) attacks.
            bool guaranteed = info.forceHit || info.hitTier == HitTier.Reliable;
            r.hitChance = guaranteed ? 1f : hitChance;
            if (!guaranteed && hitRoll > hitChance)
            {
                r.hit = false;
                // A missed committed skill still builds a little stagger (anti-feel-bad).
                r.staggerBuilt = info.isBreakSkill ? cfg.staggerBuildNormalHit : 0f;
                return r;
            }

            // 2) BASE DAMAGE: offense stat × ability power × variance roll × enrage multiplier.
            float offense = src != null ? (info.isMagic ? src.MagicAttack : src.Attack) : 0f;
            float dmg = offense * Mathf.Max(0f, info.basePower) * damageRoll
                      * (src != null ? src.damageOutMultiplier : 1f);

            // 3) ELEMENT modifier. Absorb (negative sentinel) flips the hit into a heal.
            var reaction = (tgt != null && tgt.elementProfile != null)
                ? tgt.elementProfile.GetReaction(info.element) : ElementReaction.Neutral;
            // Breaking the boss STRIPS its armour: a staggered target no longer resists, so the
            // Break window is a clear payoff even for physical-only parties (§7.2/§7.4/§19.3).
            // A FROZEN target's physical SHATTER likewise bypasses Physical resist — otherwise the
            // marquee Wet->Freeze->smash combo nets only 2.3 x 0.5 ~= 1.15x and feels like a dud.
            bool shatter = tgt != null && tgt.Status != null && tgt.Status.Has(StatusFlag.Frozen) && info.element == ElementType.Physical;
            if (tgt != null && (tgt.isStaggered || shatter) && reaction == ElementReaction.Resist)
                reaction = ElementReaction.Neutral;
            r.reaction = reaction;
            float elementMult = ElementProfile.MultiplierFor(reaction, cfg);
            if (elementMult < 0f)
            {
                r.absorbed = true; r.isHeal = true;
                r.amount = Mathf.Max(0, Mathf.RoundToInt(dmg));   // healed instead of damaged
                return r;
            }
            dmg *= elementMult;

            // 4) DEFENSE mitigation (percentage, diminishing). Soul Arrow etc. pierce some defense.
            float pierce = info.ability != null ? Mathf.Clamp01(info.ability.ignoreDefensePercent) : 0f;
            float def = tgt != null ? tgt.Defense * (1f - pierce) : 0f;
            dmg *= (1f - def / (def + cfg.defenseK));

            // 4b) POSITIONING: a BACK-ROW hero is shielded from single-target PHYSICAL (melee) blows —
            //     the front line takes the brunt. AoE and ranged/magic ignore rows (so turtling the
            //     whole party in the back still gets punished by the Dragon's Flame Breath AoE).
            if (tgt != null && tgt.backRow && !info.isMagic
                && info.ability != null && info.ability.targetRule == TargetRule.SingleEnemy)
                dmg *= cfg.backRowMeleeMult;

            // 5) STAGGER multiplier while the target is Broken — the burst window.
            if (tgt != null && tgt.isStaggered) dmg *= cfg.staggerDamageMult;

            // 6) SYNERGY (Oiled+Fire, Wet+Lightning, Marked, Shatter, ...).
            var syn = SynergyResolver.Resolve(tgt != null ? tgt.Status : null, info.element);
            dmg *= syn.damageMultiplier;
            // A PAID hit that cashes in a setup (a Shatter detonation, or any blow on a Marked target)
            // earns the press-turn bonus too — so the physical classes aren't locked out of the action
            // economy that the Mage's Ice-weakness would otherwise monopolise (§5.5).
            r.comboDetonated = (syn.note != null && syn.note.Contains("SHATTER"))
                            || (tgt != null && tgt.Status != null && tgt.Status.Has(StatusFlag.Marked));

            // 7) CRIT (Marked raises the chance; some finishers always crit).
            float critChance = (src != null ? src.CritChance : 0f) + syn.critChanceBonus;
            bool forcedCrit = info.ability != null && info.ability.guaranteedCrit;
            if (forcedCrit || critRoll <= critChance)
            {
                r.crit = true;
                dmg *= (src != null ? src.CritDamage : 1.5f);
            }

            // 7c) RISK DIE — the SPECIAL skill's visible d20 gamble. Low rolls go BAD (backfire/whiff),
            //     high rolls pay off (big/jackpot, jackpot also crits). ONLY abilities flagged
            //     rollsRiskDie draw it; for every other call this block is skipped, so the seeded
            //     CombatTests are byte-identical. SelfRecoil takes a slice of the WOULD-BE damage.
            if (info.rollsRiskDie)
            {
                r.risked = true;
                riskRoll = Mathf.Max(Mathf.Clamp01(info.riskFloor), riskRoll);   // a "safe" special floors the roll out of Backfire
                r.riskFace = Mathf.Clamp(Mathf.FloorToInt(riskRoll * 20f) + 1, 1, 20);
                if (riskRoll < cfg.riskBackfireThreshold)
                {
                    r.riskBand = RiskBand.Backfire;
                    if (info.backfireKind == BackfireKind.SelfRecoil)
                        r.selfDamage = Mathf.Max(1, Mathf.RoundToInt(dmg * cfg.riskSelfRecoilPct));
                    dmg *= cfg.riskBackfireDamageMult;
                }
                else if (riskRoll < cfg.riskWhiffThreshold) { r.riskBand = RiskBand.Whiff; dmg *= cfg.riskWhiffDamageMult; }
                else if (riskRoll >= cfg.riskJackpotThreshold)
                {
                    r.riskBand = RiskBand.Jackpot; dmg *= cfg.riskJackpotDamageMult;
                    if (!r.crit) { r.crit = true; dmg *= (src != null ? src.CritDamage : 1.5f); }
                }
                else if (riskRoll >= cfg.riskBigThreshold) { r.riskBand = RiskBand.Big; dmg *= cfg.riskBigDamageMult; }
                else r.riskBand = RiskBand.Normal;
            }

            // 7b) FINISHER payoffs (§6): bonus vs a setup flag (Marked/Weaken/Frozen) and an
            //     execute bonus on a low-HP target — reasons to set up before firing an ult.
            if (info.ability != null && tgt != null)
            {
                if (info.ability.bonusVsFlag != StatusFlag.None && tgt.Status.Has(info.ability.bonusVsFlag))
                    dmg *= info.ability.bonusVsFlagMult;
                if (info.ability.executeBelowHpPct > 0f && tgt.stats.maxHP > 0
                    && (float)tgt.currentHP / tgt.stats.maxHP <= info.ability.executeBelowHpPct)
                    dmg *= info.ability.executeMult;
            }

            // 8) DEFEND stance reduces incoming damage.
            if (tgt != null && tgt.Status.Has(StatusFlag.Defending)) dmg *= cfg.defendDamageMult;

            // 9) CLAMP & round (never below 0).
            r.amount = Mathf.Max(0, Mathf.RoundToInt(dmg));

            // 10) STAGGER BUILD — ADDITIVE so the intended line (break the boss WITH its weakness)
            //     compounds instead of being capped: normal 8, weakness 20, break-skill 30,
            //     weakness+break-skill ~42. (Was an exclusive if/else where a weakness break-skill
            //     built the SAME as a non-weakness one, muting the whole "exploit the weakness" promise.)
            float build = cfg.staggerBuildNormalHit;
            if (reaction == ElementReaction.Weak) build += (cfg.staggerBuildWeaknessHit - cfg.staggerBuildNormalHit);
            if (info.isBreakSkill) build += (cfg.staggerBuildBreakSkill - cfg.staggerBuildNormalHit);
            r.staggerBuilt = build + syn.bonusStaggerBuild;
            return r;
        }

        // Non-mutating preview for the action menu (informed gamble, §E.1): the min/max final
        // damage band (variance ends, no crit) plus the element reaction, so the HUD can show
        // "WEAK 320-410" or the all-important "ABSORB! — heals" warning before the player commits.
        public struct Preview { public int min, max; public ElementReaction reaction; public bool absorb; }
        public Preview PreviewDamage(DamageInfo info)
        {
            var lo = ComputePure(info, cfg, 0f, cfg.damageVarianceMin, 1f);   // hitRoll 0 = hits, critRoll 1 = no crit
            var hi = ComputePure(info, cfg, 0f, cfg.damageVarianceMax, 1f);
            return new Preview { min = lo.amount, max = hi.amount, reaction = hi.reaction, absorb = hi.absorbed };
        }

        // APPLY: the side-effect half. Mutates the target, updates the miss streak, builds
        // stagger on a boss target, and raises OnDamageDealt for presentation + the log.
        public void Apply(DamageResult r, BattleContext ctx)
        {
            if (r.target == null) return;

            if (!r.hit)
            {
                if (r.source != null) r.source.ConsecutiveMisses++;
            }
            else
            {
                if (r.source != null) r.source.ConsecutiveMisses = 0;
                if (r.isHeal) r.target.Heal(r.amount);
                else
                {
                    int dmg = r.amount;
                    // Magic Guard (§6.2): convert incoming damage to MP loss — each MP soaks 2 HP.
                    if (dmg > 0 && r.target.currentMP > 0 && r.target.Status.Has(StatusFlag.MagicGuard))
                    {
                        int absorbed = Mathf.Min(dmg, r.target.currentMP * 2);
                        int mpUsed = Mathf.CeilToInt(absorbed / 2f);
                        r.target.TrySpendMP(mpUsed);
                        dmg -= absorbed;
                        ctx.Log($"      Magic Guard absorbs {absorbed} damage ({mpUsed} MP).");
                    }
                    r.target.TakeDamage(dmg);
                }
            }

            if (r.staggerBuilt > 0f && r.target.isBoss)
                ctx.stagger.Build(r.target, r.staggerBuilt, ctx);

            // Risk-die SelfRecoil backfire: the caster eats a slice of their own would-be blow.
            if (r.hit && r.selfDamage > 0 && r.source != null)
            {
                r.source.TakeDamage(r.selfDamage);
                ctx.Log($"      BACKFIRE! {r.source.displayName} takes {r.selfDamage} self-damage.");
            }

            ctx.RaiseDamage(r);
        }
    }
}
