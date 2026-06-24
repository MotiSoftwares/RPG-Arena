using RPGArena.Characters;

namespace RPGArena.Combat
{
    // Everything the damage pipeline needs to compute one hit. Flags let synergies, stances,
    // and the RNG layer modify the result (CLAUDE.md §4.8 / Appendix E.1).
    public struct DamageInfo
    {
        public Entity source;
        public Entity target;
        public Ability ability;
        public ElementType element;
        public float basePower;        // ability.power (1.0 == 100%)
        public bool isMagic;           // use MagicAttack instead of Attack
        public bool forceHit;          // auto-hit abilities skip the accuracy roll
        public bool isBreakSkill;      // adds extra stagger build
        public HitTier hitTier;        // reliability tier for the hit roll
    }

    // The outcome of one hit. Presentation reads this from OnDamageDealt to drive the
    // floating number, colour, the "dice" roll flourish, hit-stop and shake.
    public struct DamageResult
    {
        public Entity source;
        public Entity target;
        public Ability ability;        // the ability used (drives per-ability animation: cast/area/attack)
        public ElementType element;    // the attack's element (drives element-coloured VFX)
        public int amount;             // final HP delta magnitude (>= 0)
        public bool hit;               // false == missed
        public bool crit;
        public bool absorbed;          // wrong element: the hit healed the target instead
        public bool isHeal;            // true when this result restores HP (absorb or a heal)
        public ElementReaction reaction;
        public float damageRoll;       // the rolled variance multiplier (for the dice UI)
        public float hitChance;        // the chance we rolled against (shown pre-commit)
        public float staggerBuilt;     // how much stagger this hit added to the target
        public bool comboDetonated;    // this paid hit cashed in a setup (Shatter / Marked) — earns a bonus turn for physical classes too
    }
}
