namespace RPGArena.Combat.Status
{
    // What a flag+element synergy contributes to a hit. Consulted by the damage pipeline
    // (step 7) so one class's setup (Oiled/Wet/Marked) pays off for another (§4.12).
    public struct SynergyOutcome
    {
        public float damageMultiplier;     // multiplies the element-adjusted damage
        public float bonusStaggerBuild;    // extra stagger this hit adds
        public float critChanceBonus;      // extra crit chance this hit
        public bool forceStatusApply;      // guarantees the ability's status lands (e.g. Freeze)
        public string note;                // short label for the combat log

        public static SynergyOutcome None => new SynergyOutcome { damageMultiplier = 1f };
    }

    // The cross-class combo brain. TRIMMED per Appendix E.3 (Bleed combo removed; Stun folded
    // into Frozen). Values are starting constants here; they can later move to a data table
    // SO (§4.12 "data-described") without touching the pipeline.
    public static class SynergyResolver
    {
        public static SynergyOutcome Resolve(StatusEffectContainer targetStatus, ElementType element)
        {
            var outcome = SynergyOutcome.None;
            if (targetStatus == null) return outcome;

            // Oiled + Fire => +50% fire damage (and the Fire skill's Burn spreads).
            if (targetStatus.Has(StatusFlag.Oiled) && element == ElementType.Fire)
            {
                outcome.damageMultiplier *= 1.5f;
                outcome.note = "Oiled+Fire";
            }

            // NOTE ON PHYSICAL LINES: they are resolved as an EXCLUSIVE LADDER further down, not as
            // independent bonuses. Stacking them multiplied out to ~5.8x (Oiled x soaked x SHATTER x
            // QUARRY), which blew past the intended SHATTER ceiling and collapsed fights to under
            // four rounds. Exactly one physical line pays, and it is always the best one available.

            // Wet + Lightning => +50% damage and a guaranteed control proc.
            if (targetStatus.Has(StatusFlag.Wet) && element == ElementType.Lightning)
            {
                outcome.damageMultiplier *= 1.5f;
                outcome.forceStatusApply = true;
                outcome.note = "Wet+Lightning";
            }

            // Wet + Ice => reliably Freeze — the intended Dragon combo.
            if (targetStatus.Has(StatusFlag.Wet) && element == ElementType.Ice)
            {
                outcome.forceStatusApply = true;
                outcome.note = "Wet+Ice (Freeze)";
            }

            // THE PHYSICAL LADDER — exactly ONE rung pays, always the highest available. Physical
            // is the element every class can throw, so if these stacked, a party that piled on every
            // setup got a multiplicative jackpot (measured ~5.8x) that dwarfed the intended SHATTER
            // ceiling and ended fights in under four rounds. As a ladder the ordering guarantees the
            // design intent BY CONSTRUCTION rather than by tuning: coordinating a full cross-school
            // freeze always beats a same-school setup, which always beats a single flag.
            if (element == ElementType.Physical)
            {
                bool frozen = targetStatus.Has(StatusFlag.Frozen);
                bool marked = targetStatus.Has(StatusFlag.Marked);
                bool oiled = targetStatus.Has(StatusFlag.Oiled);
                bool wet = targetStatus.Has(StatusFlag.Wet);

                if (frozen)
                {
                    // SHATTER — the marquee line: Wet -> Ice(freeze) -> smash, three heroes deep.
                    outcome.damageMultiplier *= 2.3f;
                    outcome.bonusStaggerBuild += 25f;
                    outcome.note = Append(outcome.note, "SHATTER!");
                }
                else if (marked && oiled)
                {
                    // QUARRY — the all-physical party's own ceiling. Lower than SHATTER (1.9 vs 2.3)
                    // because it needs no cross-school coordination, but it is a REAL payoff for a
                    // trio that has no Ice at all and previously could only ever chip.
                    outcome.damageMultiplier *= 1.9f;
                    outcome.bonusStaggerBuild += 18f;
                    outcome.note = Append(outcome.note, "QUARRY!");
                }
                else if (oiled)
                {
                    // Slick: a single-flag consolation that keeps the setup worth casting alone.
                    outcome.damageMultiplier *= 1.15f;
                    outcome.bonusStaggerBuild += 12f;
                    outcome.note = Append(outcome.note, "Oiled+Physical");
                }
                else if (wet)
                {
                    // Soaked: the same consolation for the other coating.
                    outcome.damageMultiplier *= 1.15f;
                    outcome.bonusStaggerBuild += 12f;
                    outcome.note = Append(outcome.note, "Wet+Physical");
                }
            }

            // Marked => more crit and more stagger on every hit against the target. This rides on
            // TOP of the ladder deliberately: Mark is an accuracy/crit tool, not a damage line, so
            // it sharpens whatever rung you landed on instead of competing with it.
            if (targetStatus.Has(StatusFlag.Marked))
            {
                outcome.critChanceBonus += 0.25f;
                outcome.bonusStaggerBuild += 6f;
            }

            // Marked + Frozen => BRITTLE: a marked, frozen target is glass — a big extra crit-chance
            // spike (on top of Mark's) so the Shatter that follows almost always crits. Rewards
            // spending a Mark BEFORE the freeze->smash, deepening the marquee combo line.
            if (targetStatus.Has(StatusFlag.Marked) && targetStatus.Has(StatusFlag.Frozen))
            {
                outcome.critChanceBonus += 0.4f;
                outcome.note = Append(outcome.note, "Brittle");
            }

            return outcome;
        }

        private static string Append(string note, string add)
            => string.IsNullOrEmpty(note) ? add : note + " +" + add;
    }
}

