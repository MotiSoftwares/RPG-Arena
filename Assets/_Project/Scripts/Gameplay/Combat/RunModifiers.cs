using RPGArena.Combat.Status;

namespace RPGArena.Combat
{
    // The rule changes a run has accumulated from its boons.
    //
    // WHY THIS EXISTS: boons used to be flat StatBlock deltas (+4 STR, +30 maxHP). Numbers going up
    // is not a decision — every card is "yes", the pick order barely matters, and by boss three you
    // are playing the identical fight with bigger numbers. A boon that changes a RULE ("FREEZE also
    // Marks", "Break windows last a turn longer") changes which combos are worth building and which
    // class you protect, so the same party plays differently depending on what it drafted.
    //
    // Every field's default is the vanilla behaviour, and every consumer null-guards the whole
    // object, so a context that never sets `mods` (i.e. the entire headless EditMode suite) is
    // bit-identical to before this type existed.
    public class RunModifiers
    {
        // FREEZE also applies this status (wired to the Marked definition by the boon asset).
        // Non-null = the rule is on. Bridges the FROST line into Brittle without spending a turn
        // on the Mark, which is the whole point: it changes what the Thief should be doing.
        public StatusEffectDefinition freezeAlsoApplies;

        // Extra boss turns a Break lasts. Added to BattleContext.BossStaggeredTurns at setup, so it
        // is also the direct counter to the Evil Warrior's stagger decay.
        public int bonusBrokenTurns;

        // Extra turns the Wet/Oiled coatings last. Note the deliberate double edge: longer coatings
        // survive a bad turn order, but they also sit on the board longer for the Black Mage to eat.
        public int bonusCoatingTurns;

        // A GRAZE builds full stagger instead of the reduced amount. Turns the consolation outcome
        // into real progress toward a Break.
        public bool glancesBuildFullStagger;

        // Extra Valor on any action that detonates a combo — a drafted party cycles Overdrive faster.
        public float bonusValorPerCombo;

        // The first hero to fall each battle gets back up. `secondWindUsed` is the per-battle latch;
        // BattleController clears it at setup (a run-long boon, a once-per-fight effect).
        public bool secondWind;
        public float secondWindHpPercent = 0.35f;
        public bool secondWindUsed;

        // Convenience for the hooks, so callers read `mods.FreezeMarks` instead of a null-check chain.
        public bool FreezeMarks => freezeAlsoApplies != null;
    }
}
