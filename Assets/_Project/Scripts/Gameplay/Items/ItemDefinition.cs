using UnityEngine;

namespace RPGArena.Combat
{
    // A purchasable, consumable battle item: an Ability in a bottle. Using one casts its wrapped
    // ability through the exact same command pipeline as a skill — so items get targeting, damage/
    // heal/status resolution, VFX, SFX, and log lines for free — and consumes one copy from the
    // RunState inventory. Item abilities cost 0 MP and are autoHit; they never enter the headless
    // balance tests (no AI uses them).
    [CreateAssetMenu(menuName = "RPGArena/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        [Tooltip("Shop price in gold.")]
        public int goldCost = 50;

        [Tooltip("The ability this item casts when used (mpCost 0 + autoHit recommended).")]
        public Ability ability;
    }
}
