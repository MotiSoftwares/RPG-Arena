using System.Collections.Generic;
using UnityEngine;
using RPGArena.Characters;

namespace RPGArena.Combat.AI
{
    // Strategy base (§4.14): each boss's brain is a swappable ScriptableObject asset assigned
    // on the BossDefinition. A concrete brain reads the battle state and returns its chosen
    // ability + target. It MUST always return a valid action (or an explicit pass via a null
    // ability) so the turn loop can never deadlock.
    public abstract class AIBehavior : ScriptableObject
    {
        public abstract Ability DecideAction(BattleContext ctx, Entity self,
                                             IReadOnlyList<Entity> opponents, out Entity target);

        // TELEGRAPHED INTENT (§ the planning layer). Returns what this brain WOULD do on its next
        // turn WITHOUT mutating any state, so the HUD can show the player what is coming and turn
        // every turn from a reaction into a plan. Brains whose choice is deterministic (a rotation,
        // or an already-committed telegraph) can answer honestly; genuinely random brains return
        // null and the HUD shows "???" — which is itself information, since unpredictability is
        // that enemy's identity.
        //
        // MUST be side-effect free: no aiCycleIndex writes, no rng draws (a draw would desync the
        // seeded headless runs the balance gate depends on).
        public virtual Ability PreviewIntent(BattleContext ctx, Entity self,
                                             IReadOnlyList<Entity> opponents) => null;
    }
}
