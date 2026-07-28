using System.Collections.Generic;

namespace RPGArena.Core
{
    // Lightweight, in-memory state for a single run of the boss gauntlet. Created once by
    // GameBootstrap and kept for the whole session — this is a boss-rush, not a save-heavy
    // RPG (CLAUDE.md §4.15). Roguelite boons (Appendix E.2) accumulate here across bosses.
    [System.Serializable]
    public class RunState
    {
        // Bosses cleared this run, in order (by id/name).
        public readonly List<string> bossesCleared = new();

        // The class names the player chose for their party of three (set at character select).
        public readonly List<string> partyClassNames = new();

        // Boons picked between bosses; applied by the BoonSystem (Appendix E.2).
        // Placeholder list in M0 — the boon system itself arrives in M2.
        public readonly List<string> acquiredBoons = new();

        // Gold earned from victories (grade-scaled) and minion drops; spent in the Supply Camp
        // shop between bosses.
        public int gold;

        // Owned consumables by ItemDefinition asset name — one entry per copy. A new run starts
        // with one free Healing Potion so the item button teaches itself in fight one.
        public readonly List<string> inventory = new() { "Item_HealingPotion" };

        public int CountItem(string itemName)
        {
            int n = 0;
            foreach (var s in inventory) if (s == itemName) n++;
            return n;
        }

        public void AddItem(string itemName) => inventory.Add(itemName);
        public bool RemoveItem(string itemName) => inventory.Remove(itemName);

        // How far the player is in the gauntlet (0 = first boss).
        public int currentBossIndex;

        // The boss-rush order (asset names). The BattleController picks the current boss by name.
        public static readonly string[] BossOrder = { "Dragon", "BlackMage", "EvilWarrior" };

        // The boss currently being faced (clamped so it is always valid).
        public string CurrentBoss => BossOrder[currentBossIndex < 0 ? 0 :
            (currentBossIndex >= BossOrder.Length ? BossOrder.Length - 1 : currentBossIndex)];

        // True while there is still a boss after the current one.
        public bool HasNextBoss => currentBossIndex < BossOrder.Length - 1;

        // The boss after the current one (for "Next: ..." UI); the current one if none remains.
        public string NextBoss => HasNextBoss ? BossOrder[currentBossIndex + 1] : CurrentBoss;

        // Advance to the next boss (called after a win + boon pick).
        public void AdvanceBoss()
        {
            if (currentBossIndex >= 0 && currentBossIndex < BossOrder.Length) bossesCleared.Add(CurrentBoss);
            if (HasNextBoss) currentBossIndex++;
        }

        // Wipe everything for a brand-new run.
        public void Reset()
        {
            bossesCleared.Clear();
            partyClassNames.Clear();
            acquiredBoons.Clear();
            currentBossIndex = 0;
            gold = 0;
            inventory.Clear();
            inventory.Add("Item_HealingPotion");   // the starter freebie
        }
    }
}
