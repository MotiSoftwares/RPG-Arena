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

            // Frozen + Physical => SHATTER: a big bonus, completing Wet->Ice(freeze)->smash (§5.8/§7.2).
            if (targetStatus.Has(StatusFlag.Frozen) && element == ElementType.Physical)
            {
                outcome.damageMultiplier *= 1.6f;
                outcome.note = string.IsNullOrEmpty(outcome.note) ? "SHATTER!" : outcome.note + " +SHATTER!";
            }

            // Marked => more crit and more stagger on every hit against the target.
            if (targetStatus.Has(StatusFlag.Marked))
            {
                outcome.critChanceBonus += 0.25f;
                outcome.bonusStaggerBuild += 6f;
            }

            return outcome;
        }
    }
}
