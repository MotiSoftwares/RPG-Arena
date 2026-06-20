using UnityEngine;
using RPGArena.Core.Events;
using RPGArena.Characters;

namespace RPGArena.Combat.Events
{
    // Carries an Entity payload — turn started/ended, entity died/spawned, attack missed.
    // Lives in the Gameplay assembly because its payload (Entity) is a gameplay type, which
    // is exactly what keeps the generic EventChannel<T> base payload-agnostic in Core.
    [CreateAssetMenu(menuName = "RPGArena/Events/Entity Channel", fileName = "OnEntityEvent")]
    public class EntityChannel : EventChannel<Entity> { }
}
