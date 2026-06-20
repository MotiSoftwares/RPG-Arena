using UnityEngine;
using RPGArena.Core.Events;

namespace RPGArena.Combat.Events
{
    // Carries an Ability — raised as OnBossTelegraph so the HUD can warn "the Dragon is
    // charging Flame Breath!" a turn ahead, giving the player an informed choice (§4.13).
    [CreateAssetMenu(menuName = "RPGArena/Events/Ability Channel", fileName = "OnAbilityEvent")]
    public class AbilityChannel : EventChannel<Ability> { }
}
