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

        // --- SIDE CLAMPS ---------------------------------------------------------------
        // A TargetRule names a SIDE, but nothing downstream used to enforce it: SingleAlly returned
        // whatever entity it was handed, so a single bad target argument from anywhere — a HUD
        // picker, an AI brain, an item, a test harness — resolved "Heal -> the boss" and handed the
        // enemy a free full heal. That is the most punishing failure the damage layer can produce,
        // and it was one wrong reference away at all times.
        //
        // Both BattleManager (headless) and BattleController (live) route their single-target
        // resolution through these, so the mirrored invariant is one implementation rather than two
        // that have to be remembered together.

        // The ally slot, falling back to the caster. A DEAD ally is deliberately still legal here:
        // revive effects target the fallen, and IsAlive filtering belongs to the ability, not the side.
        public static Entity ClampToAlly(Entity caster, Entity proposed)
            => (proposed != null && caster != null && proposed.team == caster.team) ? proposed : caster;

        // The enemy slot. Returns NULL for a same-side proposal rather than substituting one, so the
        // caller's existing "no target given" path (adds-first, then first alive) chooses properly.
        public static Entity ClampToEnemy(Entity caster, Entity proposed)
            => (proposed != null && caster != null && proposed.team != caster.team) ? proposed : null;
    }
}
