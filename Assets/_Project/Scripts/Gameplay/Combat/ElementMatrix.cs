using RPGArena.Core;

namespace RPGArena.Combat
{
    // Thin facade over per-entity ElementProfiles. It answers the only question the damage
    // pipeline ever asks — "how does THIS defender react to THIS incoming element, and what
    // multiplier does that give?" — by delegating to the defender's profile plus the global
    // BalanceConfig. The per-entity profile is the single source of truth, so there is no
    // separate, duplicate 36-cell global table to keep in sync (CLAUDE.md §4.11).
    public static class ElementMatrix
    {
        // How the defender reacts (Neutral if it has no profile).
        public static ElementReaction Resolve(ElementProfile defender, ElementType incoming)
            => defender != null ? defender.GetReaction(incoming) : ElementReaction.Neutral;

        // The damage multiplier for that reaction (Absorb returns the negative heal sentinel).
        public static float Multiplier(ElementProfile defender, ElementType incoming, BalanceConfig cfg)
            => ElementProfile.MultiplierFor(Resolve(defender, incoming), cfg);
    }
}
