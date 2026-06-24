using System.Collections.Generic;
using RPGArena.Characters;

namespace RPGArena.Combat
{
    // Speed-ordered initiative (§4.7). Each round is sorted by Speed (desc). Supports a capped
    // extra-turn ("1 More") on weakness/crit (§5.5) — the action-economy reward — without ever
    // looping forever, because each entity may earn at most maxExtraPerRound bonus turns.
    public class TurnSystem
    {
        private readonly Dictionary<Entity, int> extraTurnsThisRound = new();
        private int maxExtraPerRound = 1;

        // Build this round's order. Tie-break equal speeds with a small random so order varies.
        public Queue<Entity> BuildRoundOrder(IEnumerable<Entity> combatants, System.Random rng, int maxExtra)
        {
            maxExtraPerRound = maxExtra;
            extraTurnsThisRound.Clear();

            var living = new List<Entity>();
            foreach (var e in combatants)
                if (e.IsAlive) living.Add(e);

            living.Sort((a, b) =>
            {
                int cmp = b.Speed.CompareTo(a.Speed);
                return cmp != 0 ? cmp : rng.Next(-1, 2);
            });

            return new Queue<Entity>(living);
        }

        // Grant one bonus turn this round (respecting the cap); it acts again later in the queue.
        // (Used by the headless BattleManager.)
        public bool TryGrantExtraTurn(Entity e, Queue<Entity> order)
        {
            if (e == null || !e.IsAlive) return false;
            extraTurnsThisRound.TryGetValue(e, out int used);
            if (used >= maxExtraPerRound) return false;
            extraTurnsThisRound[e] = used + 1;
            order.Enqueue(e);
            return true;
        }

        // Cap check only (the live BattleController owns the List order and inserts the bonus turn
        // RIGHT AFTER the current actor, so it acts again BEFORE the slow boss — a real press-turn
        // snowball, instead of a useless poke queued at the tail behind the enemy).
        public bool CanGrantExtra(Entity e)
        {
            if (e == null || !e.IsAlive) return false;
            extraTurnsThisRound.TryGetValue(e, out int used);
            if (used >= maxExtraPerRound) return false;
            extraTurnsThisRound[e] = used + 1;
            return true;
        }

        // THE single rule for earning a "1 More" bonus turn (§5.5), shared by the headless
        // (BattleManager) and live (BattleController) loops so they can never drift apart:
        // a hero earns it when a PAID action (mpCost > 0) either lands a WEAKNESS hit (the Mage's
        // Ice line) OR cashes in a setup — a Shatter detonation or a blow on a Marked target — so
        // the physical classes (Warrior/Archer/Thief) have their OWN reachable tempo engine instead
        // of the reward being Mage-exclusive. The free 0-MP basic still can't farm it (mpCost > 0),
        // and random crits never snowball — tempo is always a deliberate, paid, set-up reward.
        public static bool EarnsExtraTurn(Ability used, List<DamageResult> results)
        {
            if (used == null || used.mpCost <= 0 || results == null) return false;
            foreach (var r in results)
                if (r.hit && (r.reaction == ElementReaction.Weak || r.comboDetonated)) return true;
            return false;
        }
    }
}
