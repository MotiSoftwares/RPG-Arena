using UnityEngine;

namespace RPGArena.Characters
{
    // A thin facade over a combatant's Animator (on its rigged 3D model). Presentation drives it
    // from the combat event channels (via JuiceController) so the engine stays decoupled from
    // animation (§4.2). Idle is the controller's default state; Attack/Hit/Die are one-shot
    // triggers that play once and return to Idle. No-op (safe) when there is no Animator — i.e.
    // for the Dragon and any combatant still using a 2D billboard.
    public class AnimationDriver : MonoBehaviour
    {
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DieHash = Animator.StringToHash("Die");

        private Animator animator;

        private void Awake() => animator = GetComponentInChildren<Animator>();

        public void PlayAttack() { if (animator != null) animator.SetTrigger(AttackHash); }
        public void PlayHit() { if (animator != null) animator.SetTrigger(HitHash); }
        public void PlayDie() { if (animator != null) animator.SetTrigger(DieHash); }
    }
}
