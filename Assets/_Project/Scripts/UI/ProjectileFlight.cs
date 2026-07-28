using UnityEngine;

namespace RPGArena.UI
{
    // Aims and paces a projectile VFX from the caster to its target. The Erb-style projectile
    // prefabs are self-propelled: the ROOT ParticleSystem fires a world-space particle at its
    // authored startSpeed, and sub-emitters explode when that particle DIES. So the trick is not
    // to move the transform — it's to (a) spawn aimed at the target and (b) set the particle's
    // lifetime to distance/speed so it detonates exactly on the target. The root transform (which
    // carries the prefab's Light) is still flown along the same path so the glow tracks the shot.
    // Pure presentation: combat logic has already resolved by the time one launches.
    public class ProjectileFlight : MonoBehaviour
    {
        private Vector3 from, to;
        private float duration, t;
        private bool done;

        // Aim a freshly instantiated fx (already positioned at `from` by the caller) and return the
        // flight time so the caller can schedule the impact beat for the moment of arrival.
        public static float Launch(GameObject fx, Vector3 from, Vector3 to, float fallbackSpeed = 16f)
        {
            // Neutralize any pack-specific mover/autodestroy scripts — flight is owned here.
            foreach (var mb in fx.GetComponentsInChildren<MonoBehaviour>())
                if (!(mb is ProjectileFlight)) mb.enabled = false;

            Vector3 dir = to - from;
            float dist = Mathf.Max(0.01f, dir.magnitude);
            fx.transform.position = from;
            if (dist > 0.01f) fx.transform.rotation = Quaternion.LookRotation(dir / dist);

            float speed = fallbackSpeed;
            var ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                // Self-propelled prefab: adopt its authored speed; die (=> sub-emitter explosion)
                // exactly on arrival. This runs before the system's first simulation step, so it
                // shapes the play-on-awake burst too.
                if (main.startSpeed.constant > 0.5f) speed = main.startSpeed.constant;
                main.startLifetime = dist / speed;
            }

            float duration = Mathf.Max(0.15f, dist / speed);
            var p = fx.AddComponent<ProjectileFlight>();
            p.from = from; p.to = to; p.duration = duration;
            return duration;
        }

        private void Update()
        {
            if (done) return;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.position = Vector3.Lerp(from, to, k);   // carry the Light with the shot
            if (k >= 1f)
            {
                done = true;
                foreach (var ps in GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                var light = GetComponent<Light>();
                if (light != null) light.enabled = false;     // no lingering glow after the hit
                Destroy(gameObject, 2f);                      // let the death-explosion play out
            }
        }
    }
}
