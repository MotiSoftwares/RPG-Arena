using RPGArena.Characters;

namespace RPGArena.Combat
{
    // The SPECIAL skill's risk-die outcome band (low = bad gamble, high = it pays off).
    public enum RiskBand { Backfire, Whiff, Normal, Big, Jackpot }

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
        public bool rollsRiskDie;      // this is a SPECIAL: draw the risk die
        public float riskFloor;        // clamp the risk roll to >= this (a safer special)
        public BackfireKind backfireKind;  // what a backfire does to the caster
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
        public string synergyNote;     // the combo that fired ("SHATTER!", "Brittle!", "Wet+Physical"...) — presentation shows it as a callout
        public bool risked;            // a risk die was rolled for this hit (the SPECIAL)
        public int riskFace;           // the d20 face (1..20) for the flourish
        public RiskBand riskBand;      // Backfire / Whiff / Normal / Big / Jackpot
        public int selfDamage;         // SelfRecoil backfire: HP the caster loses (applied in Apply)
    }
}
