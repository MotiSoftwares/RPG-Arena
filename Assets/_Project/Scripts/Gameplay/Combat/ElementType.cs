namespace RPGArena.Combat
{
    // The damage/resistance elements. A plain enum keeps lookups fast and switch-free;
    // which elements an entity is weak/strong to lives in data (ElementProfile), not here.
    public enum ElementType { Physical, Fire, Ice, Lightning, Holy, Dark }

    // How a defender reacts to an incoming element — this is what drives the damage
    // multiplier in the pipeline (CLAUDE.md §4.11 / §5.4).
    public enum ElementReaction { Neutral, Weak, Resist, Immune, Absorb }
}
