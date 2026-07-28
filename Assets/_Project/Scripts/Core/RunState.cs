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

        // --- attrition ----------------------------------------------------------------
        // How much HP/MP each hero carries into the NEXT fight, as a fraction of max, keyed by
        // display name. Written after a victory, read at the next fight's setup.
        //
        // The floors are the safety rail: a run must never become mathematically unwinnable because
        // of one bad fight. MP floors higher than HP because a Mage with no MP has no game at all,
        // whereas a Warrior at half HP still has every button.
        public const float CarryHpFloor = 0.50f;
        public const float CarryMpFloor = 0.60f;

        public readonly Dictionary<string, float> carryHp = new();
        public readonly Dictionary<string, float> carryMp = new();

        // Valor the next fight starts with, seeded by the battle grade so a clean fast clear
        // compounds into the run.
        public float startValor;

        // Gold spent resting; each rest costs more so the Supply Camp can't become an infinite
        // healing fountain that erases attrition entirely.
        public int restsPurchased;
        public int RestCost => 120 + restsPurchased * 60;

        public bool TryGetCarry(string heroName, out float hpFrac, out float mpFrac)
        {
            hpFrac = mpFrac = 1f;
            if (string.IsNullOrEmpty(heroName) || !carryHp.TryGetValue(heroName, out hpFrac)) return false;
            carryMp.TryGetValue(heroName, out mpFrac);
            return true;
        }

        public void SetCarry(string heroName, float hpFrac, float mpFrac)
        {
            if (string.IsNullOrEmpty(heroName)) return;
            carryHp[heroName] = hpFrac;
            carryMp[heroName] = mpFrac;
        }

        // Restore everyone to `fraction` of max, but never DOWNGRADE a hero who is already healthier
        // (resting must always be an improvement, or buying one would be a trap).
        public void RestParty(float fraction)
        {
            var names = new List<string>(carryHp.Keys);
            foreach (var n in names)
            {
                carryHp[n] = System.Math.Max(carryHp[n], fraction);
                if (carryMp.TryGetValue(n, out float mp)) carryMp[n] = System.Math.Max(mp, fraction);
            }
        }

        // --- retry snapshot -----------------------------------------------------------
        // The run exactly as it was when the player walked into the current boss. Attrition plus a
        // naive retry is a death spiral (each attempt starts weaker than the failed one before it),
        // so a retry rewinds to this instead of to the corpse: same HP, same gold, same unspent
        // potions. Taken by BattleController at setup, consumed by RunFlow's Retry button.
        private Dictionary<string, float> snapHp, snapMp;
        private List<string> snapInventory;
        private int snapGold, snapRests;
        private float snapValor;
        private bool hasSnapshot;

        public void SnapshotForRetry()
        {
            snapHp = new Dictionary<string, float>(carryHp);
            snapMp = new Dictionary<string, float>(carryMp);
            snapInventory = new List<string>(inventory);
            snapGold = gold;
            snapRests = restsPurchased;
            snapValor = startValor;
            hasSnapshot = true;
        }

        public void RestoreRetrySnapshot()
        {
            if (!hasSnapshot) return;
            carryHp.Clear(); foreach (var kv in snapHp) carryHp[kv.Key] = kv.Value;
            carryMp.Clear(); foreach (var kv in snapMp) carryMp[kv.Key] = kv.Value;
            inventory.Clear(); inventory.AddRange(snapInventory);
            gold = snapGold;
            restsPurchased = snapRests;
            startValor = snapValor;
        }

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
            carryHp.Clear();
            carryMp.Clear();
            startValor = 0f;
            restsPurchased = 0;
            hasSnapshot = false;
        }
    }
}
