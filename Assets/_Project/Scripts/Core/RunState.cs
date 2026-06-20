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

        // How far the player is in the gauntlet (0 = first boss).
        public int currentBossIndex;

        // Wipe everything for a brand-new run.
        public void Reset()
        {
            bossesCleared.Clear();
            partyClassNames.Clear();
            acquiredBoons.Clear();
            currentBossIndex = 0;
        }
    }
}
