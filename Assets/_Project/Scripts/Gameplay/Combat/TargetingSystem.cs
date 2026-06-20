using System.Collections.Generic;
using RPGArena.Characters;
using RPGArena.Combat.Status;

namespace RPGArena.Combat
{
    // Small stateless helpers for picking targets, used by AI and by ability resolution (§4.6).
    // A Stealthed combatant (Thief Dark Sight) is UNTARGETABLE — excluded from single-target,
    // random, lowest-HP, and AoE selection alike, so it genuinely dodges a telegraphed nuke (§6.3).
    public static class TargetingSystem
    {
        public static bool IsTargetable(Entity e) =>
            e != null && e.IsAlive && !e.Status.Has(StatusFlag.Stealthed);

        public static Entity LowestHP(IReadOnlyList<Entity> entities)
        {
            Entity best = null;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (IsTargetable(e) && (best == null || e.currentHP < best.currentHP)) best = e;
            }
            return best;
        }

        public static Entity FirstAlive(IReadOnlyList<Entity> entities)
        {
            for (int i = 0; i < entities.Count; i++)
                if (IsTargetable(entities[i])) return entities[i];
            return null;
        }

        public static List<Entity> AllAlive(IReadOnlyList<Entity> entities)
        {
            var list = new List<Entity>();
            for (int i = 0; i < entities.Count; i++)
                if (IsTargetable(entities[i])) list.Add(entities[i]);
            return list;
        }

        public static Entity RandomAlive(IReadOnlyList<Entity> entities, System.Random rng)
        {
            var alive = AllAlive(entities);
            return alive.Count == 0 ? null : alive[rng.Next(alive.Count)];
        }
    }
}
