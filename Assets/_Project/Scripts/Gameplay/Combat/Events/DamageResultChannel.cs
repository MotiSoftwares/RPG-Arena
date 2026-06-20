using UnityEngine;
using RPGArena.Core.Events;

namespace RPGArena.Combat.Events
{
    // Carries a DamageResult — raised as OnDamageDealt so presentation can show the number,
    // the dice/roll flourish, hit-stop and shake without ever calling into combat logic.
    [CreateAssetMenu(menuName = "RPGArena/Events/Damage Result Channel", fileName = "OnDamageDealt")]
    public class DamageResultChannel : EventChannel<DamageResult> { }
}
