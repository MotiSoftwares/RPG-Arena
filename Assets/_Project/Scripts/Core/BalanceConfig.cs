using UnityEngine;

namespace RPGArena.Core
{
    // One ScriptableObject holding every global tuning constant, so the whole game can
    // be balanced from the Inspector instead of by editing code. No magic numbers live
    // in logic — they all surface here (CLAUDE.md §3.4 "no magic numbers", §A.2).
    [CreateAssetMenu(menuName = "RPGArena/Balance Config", fileName = "BalanceConfig")]
    public class BalanceConfig : ScriptableObject
    {
        [Header("Element multipliers")]
        // How far a weakness or resistance bends incoming damage.
        // RESIST was 0.5, and that single number was most of the "the Mage does ten times what
        // everyone else does" problem: the Dragon and the Evil Warrior both RESIST Physical, which
        // is the only element three of the four classes can throw, while the Mage's Ice is their
        // WEAKNESS. Halving three classes while boosting the fourth is a 3x swing before a single
        // stat is compared. 0.65 keeps resistance meaningful (you still want the right element, and
        // Break still strips it) without making the physical trio's whole kit feel broken.
        public float weakMult = 1.5f;
        public float resistMult = 0.65f;

        [Header("Party output")]
        [Tooltip("Flat multiplier on ALL hero damage. The party-wide difficulty dial: raise it when the fight reads as a slog, lower it when combos trivialise bosses. Enemies are unaffected (they scale via Entity.difficultyDamageMult).")]
        public float heroDamageMult = 1.5f;

        [Header("Defense mitigation")]
        // Percentage mitigation = Defense / (Defense + K): soft, diminishing returns
        // so stacking defense never reaches full immunity.
        public float defenseK = 100f;

        [Header("Stagger / Break")]
        // Damage multiplier applied while a target is Broken — the burst window.
        public float staggerDamageMult = 1.85f;
        // Different hit kinds add different amounts to the stagger meter.
        public float staggerBuildNormalHit = 8f;
        public float staggerBuildWeaknessHit = 20f;
        public float staggerBuildBreakSkill = 30f;

        [Header("Action economy")]
        // Hard cap so weakness/crit "1 More" turns can never loop forever.
        public int maxExtraTurnsPerEntityPerRound = 1;

        [Header("Stat derivation constants (k1..k8)")]
        // Convert primary stats (STR/DEX/INT/LUK) into derived combat stats (§5.2).
        // NOTE: k8 (crit-per-LUK) was 0.4 — that made Thief crit ~1060% (always crit + free bonus
        // turn every action). Crit chance is a probability in [0,1], so per-LUK must be a few ‰.
        public float k1 = 2f, k2 = 2.2f, k3 = 0.5f, k4 = 1f, k5 = 0.5f, k6 = 0.4f, k7 = 0.25f, k8 = 0.005f;

        [Header("RNG — informed-gamble layer (CLAUDE.md Appendix E.1)")]
        // Final damage is multiplied by a random value in this band, so every hit
        // visibly "rolls" weak..strong; a crit pushes the roll to the top of the band.
        public float damageVarianceMin = 0.85f;
        public float damageVarianceMax = 1.15f;

        // Base hit chance per ability reliability tier, before accuracy/evasion are added. Tuned so a
        // hit is a REAL gamble (~60-85%), not a near-certainty — landing your attack is part of the RNG.
        public float reliableHitBase = 0.85f;
        public float standardHitBase = 0.72f;
        public float riskyHitBase = 0.58f;

        // Hit chance is always clamped to this window so non-reliable skills are never a
        // guaranteed miss, and even a buffed shot is never a guaranteed hit (caps the certainty).
        public float hitFloor = 0.45f;
        public float hitCeiling = 0.90f;

        // Anti-feel-bad: after this many misses in a row, the next attack is forced to hit.
        public int missStreakCap = 2;

        [Tooltip("Overdrive RALLY: percent of max HP healed to the party (a fallen hero is revived at 25%).")]
        public int overdriveRallyHealPercent = 45;

        [Header("Glancing blows (dead-turn elimination)")]
        [Tooltip("Damage a GLANCING blow deals (a failed accuracy roll that still grazes). One action per hero per round means a flat whiff is a wasted turn — measured at ~24% of all attacks.")]
        [Range(0.1f, 0.6f)] public float glanceDamageMult = 0.35f;
        [Tooltip("How badly the accuracy roll must fail (0..1 past the hit chance) before it becomes a CLEAN MISS instead of a glance. Higher = more glances, fewer dead turns.")]
        [Range(0f, 1f)] public float cleanMissOvershoot = 0.55f;
        [Tooltip("Each point of (Accuracy - Evasion) shifts hit chance by this. Lowered so high-accuracy heroes don't just pin every shot to the ceiling — accuracy helps, but the gamble stays.")]
        public float accuracyToPercent = 0.005f;
        [Tooltip("Incoming damage multiplier while the target is Defending.")]
        public float defendDamageMult = 0.5f;
        [Tooltip("Positioning: damage a BACK-ROW hero takes from a single-target physical (melee) blow.")]
        public float backRowMeleeMult = 0.55f;

        [Header("MP economy")]
        [Tooltip("Flat MP regenerated at the start of every turn, so refueling is a baseline — not something you must farm by spamming free basics. Decouples the MP faucet from damage.")]
        public int mpRegenPerTurn = 5;

        [Header("Risk die (the SPECIAL skill's d20 gamble)")]
        // The risk roll is a uniform [0,1) draw mapped to a d20 face + an outcome band. Low = the
        // gamble goes bad (backfire/whiff), high = it pays off (big/jackpot, jackpot also crits).
        public float riskBackfireThreshold = 0.15f;   // < this => Backfire (the bad beat)
        public float riskWhiffThreshold   = 0.30f;     // < this => Whiff (lands soft)
        public float riskBigThreshold      = 0.75f;     // >= this => Big
        public float riskJackpotThreshold  = 0.95f;     // >= this => Jackpot (+ forced crit)
        public float riskBackfireDamageMult = 0.5f;
        public float riskWhiffDamageMult    = 0.7f;
        public float riskBigDamageMult      = 1.5f;
        public float riskJackpotDamageMult  = 2.0f;
        [Tooltip("SelfRecoil backfire: caster takes this fraction of the would-be damage as self-damage.")]
        public float riskSelfRecoilPct = 0.12f;

        [Header("Control diminishing returns")]
        [Tooltip("After a turn-skipping control status (Frozen) lands, the target cannot be controlled again for this many of ITS OWN turns. Frozen lasts 2, so 4 means the boss acts 2 turns out of every 4 — a burst window you spend and re-earn. Without this, Frost Touch (no cooldown, MP-positive) locks a boss out of the entire fight.")]
        public int controlLockTurns = 4;

        // THE STAKE (push-your-luck). A SPECIAL used to be a slot-machine pull: press the button,
        // watch the d20, accept whatever came out. The stake turns it into a read on the board —
        // finish a nearly-dead boss with ALL IN, or take the sure thing when a backfire would wipe
        // the party. One symmetric scale drives it: band = 1 + (band - 1) * scale, so a stake that
        // widens the jackpot widens the backfire by exactly as much and can never be free.
        [Tooltip("STEADY floors the risk roll here — at/above the whiff threshold, so a steady special can never backfire or whiff.")]
        public float stakeSteadyFloor = 0.30f;
        [Tooltip("STEADY: how far the bands stay stretched. <1 pulls Big/Jackpot back toward normal — the price of safety.")]
        public float stakeSteadyScale = 0.5f;
        [Tooltip("STEADY: flat damage penalty. Removing the bad faces is itself an EV gain, so without this the 'safe' option is the highest-EV one AND the lowest-variance one, which makes PRESS strictly dominated. Safety has to cost something.")]
        public float stakeSteadyDamageMult = 0.85f;
        [Tooltip("ALL IN: bands stretched this far. Pushes Jackpot AND Backfire further out, and ignores the ability's authored safety floor.")]
        public float stakeAllInScale = 1.5f;

        [Header("Boss Searing Fury (escalation vented by Break)")]
        // The boss gains one Fury stack each of its turns; every stack raises its OUTGOING damage by
        // rageDamagePerStack. Breaking the boss vents ALL stacks back to zero. So a party that never
        // builds the Break meter watches the dragon's damage run away — the anti-spam pressure valve.
        public float rageDamagePerStack = 0.06f;   // +6% boss damage per Fury stack
        public int rageMaxStacks = 12;             // cap (+72% at full) so it ramps, not one-shots

        [Header("Party Valor / Overdrive (the reliable charge win-path)")]
        public float valorMax = 100f;
        // Valor accrues from COORDINATION (combos/setups/support) far faster than from spam, so the
        // meter is the antidote to button-mashing — see ChargeSystem.AwardFor.
        public float valorPerPlainHit = 2f;     // a free/basic hit barely charges it
        public float valorPerComboHit = 18f;    // a weakness or Shatter/Mark detonation charges it hard
        public float valorPerSetup    = 10f;    // applying Wet/Oiled/Marked to the boss
        public float valorPerSupport  = 8f;     // an ally buff / heal
        public int overdriveHeroTurns = 3;       // how many hero turns the Overdrive surge lasts
        public float overdriveDamageMult = 1.35f;// party-wide damage multiplier during Overdrive
    }
}
