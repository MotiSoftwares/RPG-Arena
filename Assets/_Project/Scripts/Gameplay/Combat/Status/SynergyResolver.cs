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

            // Oiled + Physical => the slick coating leaves the target exposed: a little extra damage
            // and a solid stagger bump. This gives Thief's Oil Bomb STANDALONE value against the
            // Fire-ABSORBING Dragon, where its Oiled+Fire payoff would only heal the boss (§4.12).
            if (targetStatus.Has(StatusFlag.Oiled) && element == ElementType.Physical)
            {
                outcome.damageMultiplier *= 1.15f;
                outcome.bonusStaggerBuild += 12f;
                outcome.note = string.IsNullOrEmpty(outcome.note) ? "Oiled+Physical" : outcome.note + " +slick";
            }

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

            // Wet + Physical => the soaked target is heavier and exposed: a little extra damage and a
            // solid stagger bump. This gives Thief's Water Bomb STANDALONE value for the NO-MAGE
            // physical trio (which can't freeze): soak, then pound to drive the Break that vents the
            // dragon's Searing Fury. Mirrors the Oiled+Physical line for the actually-applied flag.
            if (targetStatus.Has(StatusFlag.Wet) && element == ElementType.Physical)
            {
                outcome.damageMultiplier *= 1.15f;
                outcome.bonusStaggerBuild += 12f;
                outcome.note = string.IsNullOrEmpty(outcome.note) ? "Wet+Physical" : outcome.note + " +soaked";
            }

            // Frozen + Physical => SHATTER: the party's highest-ceiling line, completing the
            // 3-action Wet -> Ice(freeze) -> smash combo (§5.8/§7.2). It must out-damage simply
            // casting Ice three times (each ×1.5 + a bonus turn), so it both BURSTS hard (×2.3) and
            // slams the Break meter (+stagger) — the payoff for coordinating three heroes.
            if (targetStatus.Has(StatusFlag.Frozen) && element == ElementType.Physical)
            {
                outcome.damageMultiplier *= 2.3f;
                outcome.bonusStaggerBuild += 25f;
                outcome.note = string.IsNullOrEmpty(outcome.note) ? "SHATTER!" : outcome.note + " +SHATTER!";
            }

            // Marked => more crit and more stagger on every hit against the target.
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
                outcome.note = string.IsNullOrEmpty(outcome.note) ? "Brittle!" : outcome.note + " +Brittle";
            }

            return outcome;
        }
    }
}
