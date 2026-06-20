using System.Collections.Generic;
using RPGArena.Characters;

namespace RPGArena.Combat
{
    // Small stateless helpers for picking targets, used by AI and by ability resolution (§4.6).
    public static class TargetingSystem
    {
        public static Entity LowestHP(IReadOnlyList<Entity> entities)
        {
            Entity best = null;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e.IsAlive && (best == null || e.currentHP < best.currentHP)) best = e;
            }
            return best;
        }

        public static Entity FirstAlive(IReadOnlyList<Entity> entities)
        {
            for (int i = 0; i < entities.Count; i++)
                if (entities[i].IsAlive) return entities[i];
            return null;
        }

        public static List<Entity> AllAlive(IReadOnlyList<Entity> entities)
        {
            var list = new List<Entity>();
            for (int i = 0; i < entities.Count; i++)
                if (entities[i].IsAlive) list.Add(entities[i]);
            return list;
        }

        public static Entity RandomAlive(IReadOnlyList<Entity> entities, System.Random rng)
        {
            var alive = AllAlive(entities);
            return alive.Count == 0 ? null : alive[rng.Next(alive.Count)];
        }
    }
}
