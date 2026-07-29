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
        private readonly System.Collections.Generic.List<Transform> clouds = new();
        private readonly System.Collections.Generic.List<float> cloudSpeeds = new();

        // One flock = one leader path + members holding loose offsets, flapping out of phase.
        private class Flock
        {
            public Transform root;
            public Transform[] birds;
            public float[] flapPhase;
            public Vector3 velocity;
        }
        private readonly System.Collections.Generic.List<Flock> flocks = new();

        // Every Texture2D/Sprite this script generates. Runtime-created assets are NOT destroyed by
        // a scene unload, so without explicit cleanup each Retry reload leaked another set.
        private readonly System.Collections.Generic.List<Object> generated = new();

        private Sprite Track(Sprite s, Texture2D t) { generated.Add(t); generated.Add(s); return s; }

        private void OnDestroy()
        {
            foreach (var o in generated) if (o != null) Destroy(o);
            generated.Clear();
        }

        private void Start()
        {
            BuildSun();
            BuildSkyDragon();
            BuildGodRays();
            BuildFog();
            BuildClouds();
            BuildBirds();
        }

        // THE SUN. The far sky was a flat nebula gradient with no anchor in it — nothing for the
        // god-rays to come FROM and no focal point above the horizon. This puts a real disc up
        // there, aligned with the scene's key light so the rays, the shadows and the sun all agree
        // about where the light is coming from. Sprites render unlit, so it reads as a light source
        // rather than a lit object, and it sits at 700u so nothing in the arena can ever occlude it.
        private void BuildSun()
        {
            // The scene's key light comes from BEHIND the camera, so placing the disc up-light put
            // it off-screen at negative Z where the player can never see it. The sun has to live in
            // the sky the camera is actually pointed at; the painted nebula backdrop is not
            // physically consistent anyway, so matching the visible bright quadrant beats matching
            // the shadow vector nobody can cross-check.
            var key = FindKeyLight();
            Vector3 dir = new Vector3(0.30f, 0.46f, 0.84f).normalized;
            if (key != null)
            {
                // Keep the sun on the same SIDE as the key light horizontally, so the rim lighting
                // on the characters still reads as coming from roughly the right place.
                float side = -key.transform.forward.x;
                dir = new Vector3(Mathf.Sign(side == 0f ? 1f : side) * 0.30f, 0.46f, 0.84f).normalized;
            }
            Vector3 pos = dir * 700f;

            var core = MakeSkySprite("Sun", RadialSprite(128, 2.4f), pos,
                                     new Color(1f, 0.95f, 0.82f, 0.95f), 44f);
            var halo = MakeSkySprite("SunHalo", RadialSprite(128, 0.75f), pos + dir * -6f,
                                     new Color(1f, 0.78f, 0.52f, 0.30f), 190f);
            sunCore = core.transform; sunHalo = halo.transform;
        }

        private Transform sunCore, sunHalo;

        private Light FindKeyLight()
        {
            Light best = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && (best == null || l.intensity > best.intensity)) best = l;
            return best;
        }

        // A soft radial disc. `falloff` above 1 tightens the core into a bright sun; below 1 spreads
        // it into an atmospheric halo.
        private Sprite RadialSprite(int size, float falloff)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), falloff);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Track(Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 8f), tex);
        }

        private GameObject MakeSkySprite(string name, Sprite sprite, Vector3 pos, Color col, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.color = col;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            return go;
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
            var sprite = Track(Sprite.Create(tex, new Rect(0, 0, 4, 128), new Vector2(0.5f, 0.5f), 16f), tex);

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
            var sprite = Track(Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 8f), tex);

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

        // A slow band of clouds between the treeline and the peaks. Same soft-blob sprite trick as
        // the fog banks, but bigger, higher and further — they parallax against the mountains and
        // keep the upper third of the frame alive during long turns.
        private void BuildClouds()
        {
            var tex = new Texture2D(128, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 128; x++)
                {
                    // Two overlapping soft lobes make a believable cumulus profile.
                    float d1 = Vector2.Distance(new Vector2(x, y), new Vector2(44f, 26f)) / 40f;
                    float d2 = Vector2.Distance(new Vector2(x, y), new Vector2(84f, 34f)) / 34f;
                    float a = Mathf.Max(Mathf.Clamp01(1f - d1), Mathf.Clamp01(1f - d2));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * 0.9f));
                }
            tex.Apply();
            var sprite = Track(Sprite.Create(tex, new Rect(0, 0, 128, 64), new Vector2(0.5f, 0.5f), 8f), tex);

            var rng = new System.Random(23);
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject($"Cloud_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                // Warm-lit undersides, matching the nebula light.
                sr.color = new Color(1f, 0.94f, 0.88f, 0.10f + (float)rng.NextDouble() * 0.07f);
                go.transform.position = new Vector3(-320f + i * 115f + (float)rng.NextDouble() * 40f,
                                                    46f + (float)rng.NextDouble() * 26f,
                                                    215f + (float)rng.NextDouble() * 110f);
                float s = 5.5f + (float)rng.NextDouble() * 4.5f;
                go.transform.localScale = new Vector3(s, s * 0.55f, 1f);
                clouds.Add(go.transform);
                cloudSpeeds.Add(0.5f + (float)rng.NextDouble() * 0.5f);
            }
        }

        // Distant birds. Three flocks in loose Vs crossing the valley — the cheapest "this world is
        // alive" signal there is. Each bird is a dark chevron that flaps by squashing its Y scale;
        // at 130u+ that silhouette IS a bird, and nothing closer would survive scrutiny.
        private void BuildBirds()
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) tex.SetPixel(x, y, Color.clear);
            // Two wing strokes meeting at the body: rows follow |x-16| so it reads as a chevron.
            for (int x = 0; x < 32; x++)
            {
                int y = 20 - Mathf.RoundToInt(Mathf.Abs(x - 16f) * 0.55f);
                for (int t = 0; t < 3; t++)
                {
                    int yy = Mathf.Clamp(y + t, 0, 31);
                    float a = t == 1 ? 1f : 0.55f;                     // soft edge above and below
                    tex.SetPixel(x, yy, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            var sprite = Track(Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 24f), tex);

            var rng = new System.Random(41);
            for (int f = 0; f < 3; f++)
            {
                var flock = new Flock();
                var rootGo = new GameObject($"Flock_{f}");
                rootGo.transform.SetParent(transform, false);
                flock.root = rootGo.transform;
                bool leftward = f % 2 == 1;
                rootGo.transform.position = new Vector3(leftward ? 200f : -200f - f * 90f,
                                                        30f + f * 7f, 135f + f * 45f);
                flock.velocity = new Vector3(leftward ? -1f : 1f, 0f, 0f) * (4.5f + f * 1.2f);

                int n = 5 + f * 2;
                flock.birds = new Transform[n];
                flock.flapPhase = new float[n];
                for (int i = 0; i < n; i++)
                {
                    var b = new GameObject($"Bird_{i}");
                    b.transform.SetParent(rootGo.transform, false);
                    var sr = b.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.color = new Color(0.10f, 0.09f, 0.13f, 0.85f);   // near-black against the sky
                    // Loose V behind the leader: alternate sides, drift the ranks slightly.
                    int rank = (i + 1) / 2;
                    float side = i == 0 ? 0f : (i % 2 == 0 ? 1f : -1f);
                    b.transform.localPosition = new Vector3(side * rank * 2.6f + (float)rng.NextDouble() * 0.8f,
                                                            -rank * 0.55f + (float)rng.NextDouble() * 0.7f,
                                                            rank * 1.9f);
                    b.transform.localScale = Vector3.one * (0.85f + (float)rng.NextDouble() * 0.4f);
                    flock.birds[i] = b.transform;
                    flock.flapPhase[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
                }
                flocks.Add(flock);
            }
        }

        private void Update()
        {
            // The sun and its halo are flat sprites, so they must square up to the lens or they
            // vanish edge-on. The halo also breathes very slowly, which sells atmosphere over a
            // static decal.
            var cam = Camera.main;
            if (cam != null && sunCore != null)
            {
                Quaternion face = Quaternion.LookRotation(sunCore.position - cam.transform.position);
                sunCore.rotation = face;
                if (sunHalo != null)
                {
                    sunHalo.rotation = face;
                    float pulse = 1f + 0.035f * Mathf.Sin(Time.time * 0.22f);
                    sunHalo.localScale = Vector3.one * (190f * pulse);
                }
            }

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

            // Clouds crawl and wrap — slow enough to be subliminal, present enough that a screenshot
            // taken a minute apart is a different sky.
            for (int i = 0; i < clouds.Count; i++)
            {
                var t = clouds[i];
                t.position += Vector3.right * (cloudSpeeds[i] * Time.deltaTime);
                if (t.position.x > 420f) t.position = new Vector3(-420f, t.position.y, t.position.z);
            }

            // Flocks cross the valley, bob on a soft sine, and each bird flaps out of phase.
            for (int f = 0; f < flocks.Count; f++)
            {
                var fl = flocks[f];
                fl.root.position += fl.velocity * Time.deltaTime
                                  + Vector3.up * (Mathf.Sin(Time.time * 0.5f + f * 2.1f) * 0.35f * Time.deltaTime);
                if (Mathf.Abs(fl.root.position.x) > 320f)
                    fl.root.position = new Vector3(-Mathf.Sign(fl.velocity.x) * 320f,
                                                   28f + f * 8f + Random.Range(0f, 6f),
                                                   130f + f * 45f);
                for (int i = 0; i < fl.birds.Length; i++)
                {
                    // Flap = squash Y. Distant birds are a two-state silhouette, and that is enough.
                    float flap = Mathf.Abs(Mathf.Sin(Time.time * 7f + fl.flapPhase[i]));
                    var s = fl.birds[i].localScale;
                    fl.birds[i].localScale = new Vector3(s.x, s.x * (0.35f + flap * 0.75f), 1f);
                }
            }
        }
    }
}
