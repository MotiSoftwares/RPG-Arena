using UnityEngine;
using RPGArena.Combat.Status;

namespace RPGArena.Combat
{
    // A roguelite between-boss boon (Appendix E.2): a permanent, party-wide power-up the player
    // picks from a choice of three after each boss. DATA ONLY — the BoonSystem applies the stat
    // deltas to each hero's runtime stat block and folds the RULES into the run's RunModifiers at
    // the start of the next fight.
    //
    // Prefer the RULE fields over the stat fields when authoring a new boon. A stat boon makes the
    // same fight easier; a rule boon makes it a different fight. The stat block is kept for small
    // filler cards and for topping up a party that drafted badly.
    [CreateAssetMenu(menuName = "RPGArena/Boon", fileName = "Boon")]
    public class BoonDefinition : ScriptableObject
    {
        public string displayName = "Boon";
        [TextArea] public string description;

        [Header("Rule changes (preferred — see RunModifiers)")]
        [Tooltip("FREEZE also applies this status. Wire the Marked definition here to bridge the FROST line straight into Brittle.")]
        public StatusEffectDefinition freezeAlsoApplies;
        [Tooltip("Extra boss turns a Break lasts. Also the direct counter to the Evil Warrior's stagger decay.")]
        public int bonusBrokenTurns;
        [Tooltip("Extra turns Wet/Oiled last. Double-edged: longer coatings also sit on the board for the Black Mage to eat.")]
        public int bonusCoatingTurns;
        [Tooltip("A GRAZE builds full stagger instead of the reduced amount.")]
        public bool glancesBuildFullStagger;
        [Tooltip("Extra Valor whenever an action detonates a combo.")]
        public float bonusValorPerCombo;
        [Tooltip("The first hero to fall each battle gets back up at secondWindHpPercent.")]
        public bool secondWind;
        [Range(0.1f, 1f)] public float secondWindHpPercent = 0.35f;

        [Header("Party-wide stat bonuses")]
        public int strDelta, dexDelta, intDelta, lukDelta;
        public int maxHpDelta, maxMpDelta;
        public int attackDelta, magicAttackDelta, defenseDelta, speedDelta;
        public float accuracyDelta, critChanceDelta;

        // Does this card change how the game is PLAYED (rather than how big the numbers are)?
        // RunFlow guarantees at least one of these in every choice of three, so no draft is ever
        // three interchangeable stat bumps.
        public bool IsRule => freezeAlsoApplies != null || bonusBrokenTurns != 0 || bonusCoatingTurns != 0
                           || glancesBuildFullStagger || bonusValorPerCombo != 0f || secondWind;
    }
}
