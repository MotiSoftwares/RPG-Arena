using UnityEngine;

namespace RPGArena.Characters
{
    // Procedural "animation" for a combatant's billboard body so the stage feels alive WITHOUT
    // rigged 3D models (§2.2/§10.5 animated-characters intent, achieved cheaply): a gentle idle
    // bob, an attack lunge toward the foe, and a hit recoil. It drives the "Body" child's world
    // position so the contact shadow (a separate child) stays planted on the ground. When real 3D
    // models arrive, an Animator-driven AnimationDriver replaces this — the call sites stay the same.
    public class CombatantMotion : MonoBehaviour
    {
        [Header("Idle bob")]
        public float bobAmplitude = 0.07f;
        public float bobSpeed = 1.8f;
        [Header("Lunge / recoil springback")]
        public float lungeDistance = 0.55f;
        public float recoilDistance = 0.3f;
        public float springback = 9f;

        private Transform body;
        private Vector3 baseWorldPos;
        private Vector3 offset;          // current lunge/recoil offset (springs back to zero)
        private float bobPhase;
        private bool ready;

        private void Start()
        {
            body = transform.Find("Body");
            if (body != null) baseWorldPos = body.position;
            bobPhase = Random.value * Mathf.PI * 2f;   // desync bobs between combatants
            ready = body != null;
        }

        private void LateUpdate()
        {
            if (!ready) return;
            bobPhase += Time.deltaTime * bobSpeed;
            float bob = Mathf.Sin(bobPhase) * bobAmplitude;
            // Ease the lunge/recoil offset back to rest (unscaled so hit-stop doesn't freeze it odd).
            offset = Vector3.Lerp(offset, Vector3.zero, Mathf.Clamp01(Time.unscaledDeltaTime * springback));
            body.position = baseWorldPos + new Vector3(0f, bob, 0f) + offset;
        }

        // Snap toward a world direction, then spring back — a strike.
        public void Lunge(Vector3 worldDir)
        {
            if (!ready) return;
            worldDir.y = 0f;
            offset = worldDir.sqrMagnitude > 0.0001f ? worldDir.normalized * lungeDistance : Vector3.zero;
        }

        // Knock back away from the attacker — a flinch.
        public void Recoil(Vector3 worldDir)
        {
            if (!ready) return;
            worldDir.y = 0f;
            offset = worldDir.sqrMagnitude > 0.0001f ? worldDir.normalized * recoilDistance : Vector3.zero;
        }
    }
}
