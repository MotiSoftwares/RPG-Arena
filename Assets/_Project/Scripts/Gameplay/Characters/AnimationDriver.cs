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

        // WHERE IN AN ATTACK CLIP THE BLOW ACTUALLY LANDS, as a fraction of the clip.
        //
        // Presentation used to assume every swing connects a flat 0.35s in ("mixamo one-handers
        // connect ~0.35s"). The clips the game actually plays run from 0.67s to 3.60s, so that
        // constant put the damage number, hit-stop and VFX 15% into the Evil Warrior's 2.43s swipe
        // and past the end of the Thief's jab. A FRACTION scales with whatever clip is playing —
        // including the attack-variant blend trees, where the clip is picked at random each swing.
        //
        // Per-rig and serialized: a lumbering brute and a duellist genuinely connect at different
        // points, so this is tunable per prefab rather than one global number.
        [Range(0.1f, 0.9f)] public float contactFraction = 0.42f;

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
        // A flinch must never chop the character's OWN swing in half — that reads as a stutter and
        // was one of the "animations look broken" cases (multi-hit trades interrupt constantly).
        // If we're mid-attack, skip the flinch; the recoil motion still sells the impact.
        public void PlayHit()
        {
            if (!Has(HitHash)) return;
            if (animator != null)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                bool midAttack = (st.IsName("Attack") || st.IsName("Cast") || st.IsName("AreaAttack")) && st.normalizedTime % 1f < 0.85f;
                if (midAttack) return;
            }
            animator.SetTrigger(HitHash);
        }

        // Seconds from now until the action clip CURRENTLY PLAYING reaches its contact frame.
        //
        // Returns a negative number when the animator is not (yet) in an action state, which the
        // caller uses to keep polling: a trigger set this frame takes a frame or two to transition,
        // and on a rig with no Animator at all there is nothing to wait for. Callers must always
        // keep their own timeout — an interrupted state must never stall the battle.
        public float TimeToContact()
        {
            if (animator == null) return -1f;
            var st = animator.GetCurrentAnimatorStateInfo(0);
            if (!st.IsName("Attack") && !st.IsName("Cast") && !st.IsName("AreaAttack")) return -1f;

            var info = animator.GetCurrentAnimatorClipInfo(0);
            if (info == null || info.Length == 0 || info[0].clip == null) return -1f;
            float len = info[0].clip.length;
            if (len <= 0.01f) return -1f;

            float played = (st.normalizedTime % 1f) * len;
            return Mathf.Max(0f, len * contactFraction - played);
        }

        public void PlayDie()
        {
            // Release the hip clamp first — a death clip is SUPPOSED to put the body on the ground.
            var lockComp = GetComponentInParent<HipHeightLock>();
            if (lockComp != null) lockComp.suspended = true;
            if (Has(DieHash)) animator.SetTrigger(DieHash);
        }
    }
}
