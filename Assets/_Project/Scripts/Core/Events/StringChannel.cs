using UnityEngine;

namespace RPGArena.Core.Events
{
    // A channel carrying a string payload (e.g. the scene name to transition to,
    // for OnSceneTransitionRequested in CLAUDE.md §4.13).
    [CreateAssetMenu(menuName = "RPGArena/Events/String Channel", fileName = "OnStringEvent")]
    public class StringChannel : EventChannel<string> { }
}
