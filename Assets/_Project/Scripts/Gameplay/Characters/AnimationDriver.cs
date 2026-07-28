using System.Collections.Generic;
using UnityEngine;

namespace RPGArena.Characters
{
    // A thin facade over a combatant's Animator (on its rigged 3D model). Presentation drives it
    // from the combat event channels (via JuiceController) so the engine stays decoupled from
    // animation (§4.2). Idle is the controller's default; the rest are one-shot triggers that
    // return to Idle. Every call is capability-checked: a combatant whose controller lacks a
    // parameter (or has no Animator at all — e.g. the 2D Dragon) safely falls back or no-ops.
    public class AnimationDriver : MonoBehaviour
    {
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int CastHash = Animator.StringToHash("Cast");
        private static readonly int AreaHash = Animator.StringToHash("AreaAttack");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int VictoryHash = Animator.StringToHash("Victory");
        private static readonly int VariantHash = Animator.StringToHash("AttackVariant");
        private static readonly int IdleBlendHash = Animator.StringToHash("IdleBlend");
        private static readonly int MovingHash = Animator.StringToHash("Moving");
        private static readonly int RoarHash = Animator.StringToHash("Roar");

        private Animator animator;
        private readonly HashSet<int> paramHashes = new();

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            foreach (var p in animator.parameters) paramHashes.Add(p.nameHash);
            // Desync idles so combatants don't all breathe in lockstep (cheap extra "life").
            if (paramHashes.Contains(IdleBlendHash)) animator.SetFloat(IdleBlendHash, Random.value);
        }

        private bool Has(int hash) => animator != null && paramHashes.Contains(hash);

        public void PlayAttack()
        {
            if (animator == null) return;
            if (Has(VariantHash)) animator.SetFloat(VariantHash, Random.Range(0, 3));   // 0/1/2 -> exact blend motion
            if (Has(AttackHash)) animator.SetTrigger(AttackHash);
        }

        // Locomotion: drives the Idle<->Run blend while CombatantMotion dashes the body across the
        // arena, so melee approaches read as RUNNING, not gliding. No-ops on controllers without it.
        public void SetMoving(bool moving) { if (Has(MovingHash)) animator.SetBool(MovingHash, moving); }

        public void PlayCast() { if (Has(CastHash)) animator.SetTrigger(CastHash); else PlayAttack(); }
        public void PlayAreaAttack() { if (Has(AreaHash)) animator.SetTrigger(AreaHash); else PlayAttack(); }
        // The boss's Scream — telegraphs + the intro cinematic. Falls back gracefully on rigs without it.
        public void PlayRoar() { if (Has(RoarHash)) animator.SetTrigger(RoarHash); else PlayAreaAttack(); }
        public void PlayVictory() { if (Has(VictoryHash)) animator.SetTrigger(VictoryHash); }
        public void PlayHit() { if (Has(HitHash)) animator.SetTrigger(HitHash); }

        public void PlayDie()
        {
            // Release the hip clamp first — a death clip is SUPPOSED to put the body on the ground.
            var lockComp = GetComponentInParent<HipHeightLock>();
            if (lockComp != null) lockComp.suspended = true;
            if (Has(DieHash)) animator.SetTrigger(DieHash);
        }
    }
}
