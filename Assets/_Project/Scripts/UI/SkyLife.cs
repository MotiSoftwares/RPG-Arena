using UnityEngine;

namespace RPGArena.UI
{
    // The living backdrop: a wild dragon circling far over the valley, god-rays slanting through
    // the treeline, and slow fog banks drifting across the far field. Pure presentation — nothing
    // here touches combat state; it exists so the arena reads as a PLACE, not a diorama.
    public class SkyLife : MonoBehaviour
    {
        [Header("Sky dragon (a distant wild one, not the boss)")]
        public GameObject skyDragonPrefab;          // FourEvilDragonsPBR SoulEater variant
        public RuntimeAnimatorController flyController;
        public float orbitRadius = 46f;
        public float orbitHeight = 26f;
        public float orbitSecondsPerLap = 75f;
        public Vector3 orbitCenter = new Vector3(0f, 0f, 18f);

        private Transform flyer;
        private float orbitPhase;

        private readonly System.Collections.Generic.List<SpriteRenderer> fogBanks = new();
        private readonly System.Collections.Generic.List<float> fogSpeeds = new();
        private readonly System.Collections.Generic.List<SpriteRenderer> rays = new();

        private void Start()
        {
            BuildSkyDragon();
            BuildGodRays();
            BuildFog();
        }

        private void BuildSkyDragon()
        {
            if (skyDragonPrefab == null) return;
            var go = Instantiate(skyDragonPrefab);
            go.name = "SkyDragon";
            flyer = go.transform;
            flyer.localScale = Vector3.one * 3.2f;
            orbitPhase = Random.Range(0f, Mathf.PI * 2f);

            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null && flyController != null)
            {
                anim.runtimeAnimatorController = flyController;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // it lives at the frustum edge
            }

            // Darken to a storm-wyrm silhouette (and kill the vendor _EMISSION blowout) — it should
            // read as a distant living shape, not compete with the boss for attention.
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials)   // instances, never the shared pack assets
                {
                    m.DisableKeyword("_EMISSION");
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.16f, 0.15f, 0.22f));
                }
        }

        private void BuildGodRays()
        {
            // Soft vertical gradient sprite, generated once — same SpriteRenderer trick as the
            // scorch ring (URP's _Surface float can't be flipped reliably at runtime; sprites can).
            var tex = new Texture2D(4, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++)
            {
                float a = Mathf.Sin(y / 127f * Mathf.PI);          // fade at both ends
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 128), new Vector2(0.5f, 0.5f), 16f);

            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"GodRay_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(1f, 0.93f, 0.72f, 0.10f);
                go.transform.position = new Vector3(-14f + i * 9f, 13f, 14f + (i % 2) * 5f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, 16f + i * 4f);
                go.transform.localScale = new Vector3(2.2f + i * 0.7f, 3.4f, 1f);
                rays.Add(sr);
            }
        }

        private void BuildFog()
        {
            // One big soft blob, reused for every bank.
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2(31.5f, 31.5f);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / 32f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * 0.8f));
                }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 8f);

            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject($"FogBank_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(0.85f, 0.88f, 0.98f, 0.07f + i * 0.02f);
                go.transform.position = new Vector3(-20f + i * 16f, 2.2f + i * 1.1f, 20f + i * 6f);
                go.transform.localScale = new Vector3(26f, 7f, 1f);
                fogBanks.Add(sr);
                fogSpeeds.Add(0.25f + i * 0.12f);
            }
        }

        private void Update()
        {
            // The wild dragon rides a slow circle, banking into the turn, bobbing on thermals.
            if (flyer != null)
            {
                orbitPhase += (Mathf.PI * 2f / orbitSecondsPerLap) * Time.deltaTime;
                float bob = Mathf.Sin(Time.time * 0.6f) * 1.6f;
                var pos = orbitCenter + new Vector3(Mathf.Cos(orbitPhase) * orbitRadius,
                                                     orbitHeight + bob,
                                                     Mathf.Sin(orbitPhase) * orbitRadius * 0.55f);
                var vel = new Vector3(-Mathf.Sin(orbitPhase), 0f, Mathf.Cos(orbitPhase) * 0.55f);
                flyer.position = pos;
                if (vel.sqrMagnitude > 0.001f)
                    flyer.rotation = Quaternion.Slerp(flyer.rotation,
                        Quaternion.LookRotation(vel) * Quaternion.Euler(0f, 0f, -14f), Time.deltaTime * 1.5f);
            }

            // Fog banks drift and wrap; rays breathe.
            for (int i = 0; i < fogBanks.Count; i++)
            {
                var t = fogBanks[i].transform;
                t.position += Vector3.right * (fogSpeeds[i] * Time.deltaTime);
                if (t.position.x > 30f) t.position = new Vector3(-30f, t.position.y, t.position.z);
            }
            for (int i = 0; i < rays.Count; i++)
            {
                var col = rays[i].color;
                col.a = 0.085f + 0.035f * Mathf.Sin(Time.time * 0.35f + i * 1.7f);
                rays[i].color = col;
            }
        }
    }
}
