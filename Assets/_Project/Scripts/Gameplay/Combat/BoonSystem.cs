using RPGArena.Characters;

namespace RPGArena.Combat
{
    // Applies acquired boons to a hero's RUNTIME stat block (never the shared definition). Called
    // by the BattleController at fight setup for every boon the run has collected, so the party
    // grows stronger across the gauntlet (Appendix E.2).
    public static class BoonSystem
    {
        public static void Apply(Entity hero, BoonDefinition boon)
        {
            if (hero == null || boon == null || hero.stats == null) return;
            var s = hero.stats;
            s.STR += boon.strDelta; s.DEX += boon.dexDelta; s.INT += boon.intDelta; s.LUK += boon.lukDelta;
            s.maxHP += boon.maxHpDelta; s.maxMP += boon.maxMpDelta;
            s.baseAttack += boon.attackDelta; s.baseMagicAttack += boon.magicAttackDelta;
            s.baseDefense += boon.defenseDelta; s.baseSpeed += boon.speedDelta;
            s.baseAccuracy += boon.accuracyDelta; s.baseCritChance += boon.critChanceDelta;
        }
    }
}
