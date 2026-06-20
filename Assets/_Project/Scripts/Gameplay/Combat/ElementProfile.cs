using UnityEngine;
using RPGArena.Core;

namespace RPGArena.Combat
{
    // A ScriptableObject describing how ONE entity reacts to each element — the puzzle of
    // every boss (CLAUDE.md §4.11). Designers tick a few arrays in the Inspector; anything
    // not listed is Neutral. The Dragon, for example, absorbs Fire and is weak to Ice.
    [CreateAssetMenu(menuName = "RPGArena/Element Profile", fileName = "ElementProfile")]
    public class ElementProfile : ScriptableObject
    {
        public ElementType[] weakTo;
        public ElementType[] resistTo;
        public ElementType[] immuneTo;
        public ElementType[] absorbs;   // absorbing an element HEALS the target instead of hurting it

        // Returns how this profile reacts to an incoming element. Priority is load-bearing:
        // absorb and immune are checked BEFORE weak/resist, so even a mis-authored profile
        // (e.g. Fire listed in two arrays) resolves the stronger reaction — the Dragon
        // absorbing Fire always wins over a stray Fire weakness.
        public ElementReaction GetReaction(ElementType incoming)
        {
            if (Contains(absorbs, incoming)) return ElementReaction.Absorb;
            if (Contains(immuneTo, incoming)) return ElementReaction.Immune;
            if (Contains(weakTo, incoming)) return ElementReaction.Weak;
            if (Contains(resistTo, incoming)) return ElementReaction.Resist;
            return ElementReaction.Neutral;
        }

        // Converts a reaction into a damage multiplier using the global tuning values.
        // Absorb returns a negative sentinel: the damage pipeline reads that as "heal the
        // target instead of damaging it" rather than applying it as a multiplier.
        public static float MultiplierFor(ElementReaction reaction, BalanceConfig cfg)
        {
            switch (reaction)
            {
                case ElementReaction.Weak: return cfg.weakMult;
                case ElementReaction.Resist: return cfg.resistMult;
                case ElementReaction.Immune: return 0f;
                case ElementReaction.Absorb: return -1f;   // sentinel: caller heals
                default: return 1f;                        // Neutral
            }
        }

        // Small null-safe helper: is 'value' present in 'array'?
        private static bool Contains(ElementType[] array, ElementType value)
        {
            if (array == null) return false;
            for (int i = 0; i < array.Length; i++)
                if (array[i] == value) return true;
            return false;
        }
    }
}
