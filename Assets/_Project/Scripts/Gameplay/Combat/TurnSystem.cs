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
        public bool TryGrantExtraTurn(Entity e, Queue<Entity> order)
        {
            if (e == null || !e.IsAlive) return false;
            extraTurnsThisRound.TryGetValue(e, out int used);
            if (used >= maxExtraPerRound) return false;
            extraTurnsThisRound[e] = used + 1;
            order.Enqueue(e);
            return true;
        }
    }
}
