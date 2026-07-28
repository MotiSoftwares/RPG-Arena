using UnityEngine;

namespace RPGArena.Characters
{
    // Several of the imported Mixamo battle clips (e.g. the Warrior's sword Attack, the Mage's
    // Cast/Victory) encode big VERTICAL root motion on the hip bone. Our rigs are Generic with
    // applyRootMotion = false, and on a Generic avatar that vertical root component is reconstructed
    // straight onto the hip bone — so the whole skeleton SINKS into the floor for the duration of
    // the clip (the "magician goes downwards when attacking" bug). Neither the clip's position
    // curves nor the Root-Transform-Position-Y import bake reaches it on a Generic rig.
    //
    // This component is the rig-agnostic safety net: after the Animator has posed the skeleton each
    // frame (LateUpdate), it clamps the hip bone's LOCAL height so it can never drop more than a
    // small, natural amount below its bind height. Legit weight-shifts/crouches (a few cm) pass
    // through untouched; only the catastrophic ~0.9m sinks get caught. Horizontal motion and every
    // bone rotation (the actual swing/cast pose) are left alone.
    [DefaultExecutionOrder(50)]   // after the Animator's pose, before nothing that depends on it
    public class HipHeightLock : MonoBehaviour
    {
        [Tooltip("How far below bind height the hips may still travel (preserves natural crouches; clamps full sinks).")]
        public float maxDip = 0.25f;

        // Set while a clip is SUPPOSED to put the hips on the floor (death, knockdown). Without this
        // the clamp holds the corpse upright — fighters died standing at half-height.
        public bool suspended;

        private Transform hips;
        private float bindLocalY;
        private bool ready;
        private Animator animator;
        private static readonly int DieState = Animator.StringToHash("Die");

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            hips = FindHipBone(transform);
            if (hips != null)
            {
                bindLocalY = hips.localPosition.y;   // captured at instantiation = the bind/idle pose
                ready = true;
            }
        }

        private void LateUpdate()
        {
            if (!ready || suspended) return;
            // A death/knockdown clip legitimately drops the whole skeleton — let it.
            if (animator != null)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                if (st.shortNameHash == DieState || st.IsName("Die") || st.IsName("DieRecovery")) return;
            }
            var lp = hips.localPosition;
            float minY = bindLocalY - maxDip;
            if (lp.y < minY) { lp.y = minY; hips.localPosition = lp; }
        }

        // Heuristic root-bone finder: the pelvis/hips drives the whole skeleton's height. Matches the
        // common rig conventions (Mixamo "Hips", generic "Pelvis"/"Root"/"Bip01"). No-ops if none found.
        private static Transform FindHipBone(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.EndsWith("hips") || n.EndsWith(":hips") || n.EndsWith("pelvis") || n == "hips" || n == "bip01 pelvis")
                    return t;
            }
            return null;
        }
    }
}
