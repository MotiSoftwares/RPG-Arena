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
        public float approachTime = 0.28f;   // legacy floor; real duration is distance/runSpeed
        public float runSpeed = 6.5f;        // world units/sec — matched to the run clip's stride
        public float strikeHold = 0.55f;     // fallback when the swing clip's length can't be read
        public float returnTime = 0.26f;     // legacy floor for the run-back

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

        // Walk to a new anchor instead of teleporting there — a row swap should read as the hero
        // JOGGING between the lines (Run animation + eased travel), not blinking across the field.
        public void SlideBase(Vector3 worldPos, float duration = 0.45f)
        {
            if (!ready) { baseWorldPos = worldPos; return; }
            StartCoroutine(SlideRoutine(worldPos, Mathf.Max(0.05f, duration)));
        }

        private System.Collections.IEnumerator SlideRoutine(Vector3 target, float duration)
        {
            var driver = GetComponentInChildren<AnimationDriver>();
            Vector3 from = baseWorldPos;
            Quaternion homeRot = body.rotation;
            Vector3 dir = target - from; dir.y = 0f;
            Quaternion runRot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : homeRot;

            driver?.SetMoving(true);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                baseWorldPos = Vector3.Lerp(from, target, k);
                body.rotation = Quaternion.Slerp(homeRot, runRot, Mathf.Clamp01(t / 0.15f));
                yield return null;
            }
            baseWorldPos = target;
            driver?.SetMoving(false);
            for (float t = 0f; t < 0.18f; t += Time.deltaTime)   // settle back into the stage pose
            {
                body.rotation = Quaternion.Slerp(runRot, homeRot, Mathf.Clamp01(t / 0.18f));
                yield return null;
            }
            body.rotation = homeRot;
        }

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
            var animator = GetComponentInChildren<Animator>();
            // Face the run fully (drop the 3/4 camera-blend pose) so the charge visibly aims at the
            // foe; restored on the way back. Rotate the visual body only — the host anchors the HUD.
            Quaternion homeRot = body.rotation;
            Quaternion runRot = Quaternion.LookRotation(target.normalized, Vector3.up);

            // Travel time follows DISTANCE at a believable sprint pace. A fixed duration meant a
            // long charge played at ~20 units/sec — the feet cycle at ~4, so the hero ice-skated.
            float dist = target.magnitude;
            float runDur = Mathf.Clamp(dist / runSpeed, 0.28f, 0.95f);

            driver?.SetMoving(true);
            float t = 0f;
            while (t < runDur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / runDur));
                offset = target * k;
                body.rotation = Quaternion.Slerp(homeRot, runRot, Mathf.Clamp01(t / 0.12f));
                yield return null;
            }
            offset = target;
            driver?.SetMoving(false);
            onArrive?.Invoke();

            // Hold for the ACTUAL swing length instead of a guess, so the retreat never starts
            // mid-attack (which read as the hero flinching away from their own strike).
            yield return null;                     // let the trigger land so we can read the state
            float hold = strikeHold;
            if (animator != null)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                if (st.length > 0.05f && st.length < 4f) hold = st.length * 0.85f;
            }
            yield return new WaitForSeconds(hold);

            driver?.SetMoving(true);                                // backpedal home, still facing the foe
            Vector3 from = offset; t = 0f;
            float backDur = Mathf.Clamp(dist / (runSpeed * 0.85f), 0.26f, 0.9f);
            while (t < backDur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / backDur));
                offset = Vector3.Lerp(from, Vector3.zero, k);
                yield return null;
            }
            offset = Vector3.zero;
            driver?.SetMoving(false);
            t = 0f;                                                 // settle back into the 3/4 stage pose
            while (t < 0.18f)
            {
                t += Time.deltaTime;
                body.rotation = Quaternion.Slerp(runRot, homeRot, Mathf.Clamp01(t / 0.18f));
                yield return null;
            }
            body.rotation = homeRot;
            dashing = false;
        }

        // Turn to face a world direction over a short beat — used by ranged/casting attackers so
        // they actually AIM at their target instead of firing from a 3/4 stage pose.
        // The 3/4 presentation pose this combatant was SPAWNED with, so a mid-fight re-aim can
        // reproduce it. Set by BattleController right after it builds the body.
        //
        // Without these, FaceTarget did a bare LookRotation straight at the foe — so the first skill
        // a hero cast snapped them square to the dragon and they showed the camera their back for the
        // rest of the fight. The spawn pose was correct; every action after it was not.
        [System.NonSerialized] public float camBlend;
        [System.NonSerialized] public float extraYawTowardCamera;

        // THE one place that turns "where the foe is" into "where the body actually points".
        // Both spawn placement (BattleController.AttachBody) and every mid-fight re-aim go through
        // it, so the two can never drift apart again.
        public static Vector3 PresentationFacing(Vector3 faceFoe, Vector3 fromPos, float camBlend, float extraYaw)
        {
            faceFoe.y = 0f;
            var cam = Camera.main;
            Vector3 faceCam = cam != null ? cam.transform.position - fromPos : new Vector3(0f, 0f, -1f);
            faceCam.y = 0f;
            if (faceFoe.sqrMagnitude < 0.0001f)
                return faceCam.sqrMagnitude > 0.0001f ? faceCam.normalized : Vector3.forward;
            if (faceCam.sqrMagnitude < 0.0001f) return faceFoe.normalized;

            Vector3 look = Vector3.Slerp(faceFoe.normalized, faceCam.normalized, camBlend);
            // Sign derived so an actor on either side of the stage still turns TOWARD the lens.
            if (Mathf.Abs(extraYaw) > 0.01f)
            {
                float sign = Vector3.Dot(Vector3.Cross(look, faceCam.normalized), Vector3.up) >= 0f ? 1f : -1f;
                look = Quaternion.AngleAxis(sign * extraYaw, Vector3.up) * look;
            }
            return look;
        }

        public void FaceTarget(Vector3 worldDir, float turnTime = 0.15f)
        {
            if (!ready || dashing) return;
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.0001f) return;
            // Re-aim through the SAME presentation blend used at spawn, not a raw look-at.
            Vector3 look = PresentationFacing(worldDir, body != null ? body.position : transform.position,
                                              camBlend, extraYawTowardCamera);
            StartCoroutine(FaceRoutine(Quaternion.LookRotation(look, Vector3.up), turnTime));
        }

        private System.Collections.IEnumerator FaceRoutine(Quaternion look, float turnTime)
        {
            Quaternion from = body.rotation;
            for (float t = 0f; t < turnTime; t += Time.deltaTime)
            {
                if (dashing) yield break;                 // a dash owns the rotation; don't fight it
                body.rotation = Quaternion.Slerp(from, look, Mathf.Clamp01(t / turnTime));
                yield return null;
            }
            if (!dashing) body.rotation = look;
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
