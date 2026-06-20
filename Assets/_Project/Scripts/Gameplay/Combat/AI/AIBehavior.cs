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
    }
}
