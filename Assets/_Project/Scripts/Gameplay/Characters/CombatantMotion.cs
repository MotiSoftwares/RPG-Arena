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
        [Header("Melee dash (approach -> strike -> return)")]
        public float approachTime = 0.28f;   // eased run-in duration (JuiceController schedules the impact off this)
        public float strikeHold = 0.55f;     // time held at the foe while the swing plays + connects
        public float returnTime = 0.26f;     // eased run-back duration

        private Transform body;
        private Vector3 baseWorldPos;
        private Vector3 offset;          // current lunge/recoil offset (springs back to zero)
        private float bobPhase;
        private bool ready;
        private bool dashing;            // a scripted run-in is driving the offset (skip auto-springback)
        private bool recoiling;          // a scripted flinch is driving the offset (skip auto-springback)

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
            // Ease the lunge offset back to rest. While a scripted run-in (DashStrike) or flinch
            // (Recoil) owns the offset, don't fight it — those run on SCALED time so the body holds
            // still during the hit-stop freeze (which is what makes the freeze read as a hard impact).
            if (!dashing && !recoiling) offset = Vector3.Lerp(offset, Vector3.zero, Mathf.Clamp01(Time.unscaledDeltaTime * springback));
            body.position = baseWorldPos + new Vector3(0f, bob, 0f) + offset;
        }

        // Re-anchor the procedural motion to a new world spot (used when a hero repositions rows).
        public void MoveBase(Vector3 worldPos) { baseWorldPos = worldPos; }

        // Snap toward a world direction, then spring back — a strike.
        public void Lunge(Vector3 worldDir) => Lunge(worldDir, lungeDistance);

        // Snap a chosen distance toward a world direction, then spring back. A large distance turns
        // the small in-place lunge into a melee "dash in, strike, dash back" toward the foe.
        public void Lunge(Vector3 worldDir, float distance)
        {
            if (!ready) return;
            worldDir.y = 0f;
            offset = worldDir.sqrMagnitude > 0.0001f ? worldDir.normalized * distance : Vector3.zero;
        }

        // A melee approach: run a chosen distance toward the foe, HOLD briefly for the strike, then
        // run back. Reads as "charge in and hit" (vs the in-place lunge). Ignored if a run is already
        // in progress so a multi-hit flurry doesn't restart it every frame.
        public void DashStrike(Vector3 worldDir, float distance) => DashStrike(worldDir, distance, null);

        // onArrive fires the moment the runner reaches the foe — the presentation layer triggers the
        // swing there so the attack animation starts AT the target instead of gliding in mid-swing.
        public void DashStrike(Vector3 worldDir, float distance, System.Action onArrive)
        {
            if (!ready || dashing) { onArrive?.Invoke(); return; }
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.0001f) { Lunge(worldDir, distance); onArrive?.Invoke(); return; }
            StartCoroutine(DashRoutine(worldDir.normalized * distance, onArrive));
        }

        private System.Collections.IEnumerator DashRoutine(Vector3 target, System.Action onArrive)
        {
            dashing = true;
            var driver = GetComponentInChildren<AnimationDriver>();
            // Face the run fully (drop the 3/4 camera-blend pose) so the charge visibly aims at the
            // foe; restored on the way back. Rotate the visual body only — the host anchors the HUD.
            Quaternion homeRot = body.rotation;
            Quaternion runRot = Quaternion.LookRotation(target.normalized, Vector3.up);

            driver?.SetMoving(true);
            float t = 0f;
            while (t < approachTime)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / approachTime));
                offset = target * k;
                body.rotation = Quaternion.Slerp(homeRot, runRot, Mathf.Clamp01(t / 0.12f));
                yield return null;
            }
            offset = target;
            driver?.SetMoving(false);
            onArrive?.Invoke();

            yield return new WaitForSeconds(strikeHold);            // the swing plays + connects here

            driver?.SetMoving(true);                                // backpedal home, still facing the foe
            Vector3 from = offset; t = 0f;
            while (t < returnTime)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / returnTime));
                offset = Vector3.Lerp(from, Vector3.zero, k);
                yield return null;
            }
            offset = Vector3.zero;
            driver?.SetMoving(false);
            t = 0f;                                                 // settle back into the 3/4 stage pose
            while (t < 0.16f)
            {
                t += Time.deltaTime;
                body.rotation = Quaternion.Slerp(runRot, homeRot, Mathf.Clamp01(t / 0.16f));
                yield return null;
            }
            body.rotation = homeRot;
            dashing = false;
        }

        // Knock back away from the attacker — a flinch. Default magnitude.
        public void Recoil(Vector3 worldDir) => Recoil(worldDir, 1f);

        // Knock back scaled to the hit's weight (the call site passes more for a crit / weakness), HOLD
        // the flinch briefly (reads through the hit-stop freeze since it runs on scaled time), then
        // spring back with a small overshoot/settle so the impact has follow-through, not a snap home.
        public void Recoil(Vector3 worldDir, float scale)
        {
            if (!ready || dashing) return;
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.0001f) return;
            StartCoroutine(RecoilRoutine(worldDir.normalized * recoilDistance * Mathf.Max(0.1f, scale)));
        }

        private System.Collections.IEnumerator RecoilRoutine(Vector3 knock)
        {
            recoiling = true;
            offset = knock;                                         // snap to the flinch
            yield return new WaitForSeconds(0.07f);                 // HOLD (scaled → extends through hit-stop)
            Vector3 from = offset; float t = 0f;
            while (t < 0.18f) { t += Time.deltaTime; offset = Vector3.Lerp(from, knock * -0.1f, t / 0.18f); yield return null; }  // spring past rest (overshoot)
            from = offset; t = 0f;
            while (t < 0.10f) { t += Time.deltaTime; offset = Vector3.Lerp(from, Vector3.zero, t / 0.10f); yield return null; }   // settle to rest
            offset = Vector3.zero;
            recoiling = false;
        }
    }
}
