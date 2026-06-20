using UnityEngine;

namespace RPGArena.Combat
{
    // A roguelite between-boss boon (Appendix E.2): a permanent, party-wide power-up the player
    // picks from a choice of three after each boss. DATA ONLY — the BoonSystem applies the deltas
    // to each hero's runtime stat block at the start of the next fight.
    [CreateAssetMenu(menuName = "RPGArena/Boon", fileName = "Boon")]
    public class BoonDefinition : ScriptableObject
    {
        public string displayName = "Boon";
        [TextArea] public string description;

        [Header("Party-wide stat bonuses")]
        public int strDelta, dexDelta, intDelta, lukDelta;
        public int maxHpDelta, maxMpDelta;
        public int attackDelta, magicAttackDelta, defenseDelta, speedDelta;
        public float accuracyDelta, critChanceDelta;
    }
}
