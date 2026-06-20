using UnityEngine;

namespace RPGArena.Core.Events
{
    // A channel carrying a single integer payload (e.g. MP spent, heal amount).
    [CreateAssetMenu(menuName = "RPGArena/Events/Int Channel", fileName = "OnIntEvent")]
    public class IntChannel : EventChannel<int> { }
}
