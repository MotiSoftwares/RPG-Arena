using UnityEngine;

namespace RPGArena.Core.Events
{
    // A channel for "something happened" signals that carry no data — battle
    // started/won/lost, round started, stagger broken, etc. We close the generic over
    // bool only so the asset is concrete; the bool value itself is ignored.
    [CreateAssetMenu(menuName = "RPGArena/Events/Void Channel", fileName = "OnEvent")]
    public class VoidChannel : EventChannel<bool>
    {
        // Convenience so callers can write channel.Raise() instead of Raise(true).
        public void Raise() => Raise(true);
    }
}
