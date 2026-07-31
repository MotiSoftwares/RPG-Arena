using UnityEngine;

namespace RPGArena.UI
{
    // Turns the pretty meadow into a BATTLEFIELD. Everything here is built in code at battle start
    // (no scene wiring, no imported assets) and is pure presentation — combat never sees it.
    //
    // Three cheap reads do most of the work: a scorched ring under the fighters says "things die
    // here", warm braziers frame the stage and motivate the key light, and drifting embers give the
    // air depth. The vegetation cull matters as much as anything added: waist-high flowers directly
    // under a dragon fight are what made the arena read as a garden.
    [DefaultExecutionOrder(-5)]
    public class ArenaDressing : MonoBehaviour
    {
        [Header("Ring")]
        public Vector3 ringCenter = new Vector3(0.4f, 0.02f, 0f);
        public float ringRadius = 8.5f;

        [Header("Braziers")]
        public float brazierIntensity = 7f;

        private void Start()
        {
            var root = new GameObject("ArenaDressing").transform;
            root.position = Vector3.zero;
            OpenTheVista();      // before the rest: it decides what the player can actually see
            BuildScorchRing(root);
            BuildBraziers(root);
            BuildEmbers(root);
            CullVegetation();
        }

        [Header("Vista")]
        [Tooltip("Trees inside this wedge behind the fight are hidden so the battlefield opens onto the valley instead of being walled in by trunks.")]
        public float vistaHalfWidth = 13f;
        public float vistaDepth = 34f;

        // The meadow's trees stood in a dense wall directly behind the combat line, so the arena
        // felt like a clearing the size of a room. Clearing the CENTRAL wedge (the camera's view
        // cone past the fighters) opens the fight onto the distant valley and sky, while every tree
        // outside the wedge is kept — those are what frame the shot and give it depth.
        private void OpenTheVista()
        {
            int hidden = 0;
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var go = r.gameObject;
                string n = go.name;
                bool foliage = n.IndexOf("Tree", System.StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("Bush", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!foliage) continue;
                var p = go.transform.position;
                // the wedge widens with distance, matching the camera's frustum
                float t = Mathf.InverseLerp(-6f, vistaDepth, p.z);
                if (t <= 0f || t >= 1f) continue;
                float halfW = Mathf.Lerp(vistaHalfWidth * 0.55f, vistaHalfWidth, t);
                if (Mathf.Abs(p.x - ringCenter.x) > halfW) continue;
                r.enabled = false;
                hidden++;
            }
            if (hidden > 0) Debug.Log($"[ArenaDressing] opened the vista — hid {hidden} trees/bushes behind the arena.");
        }

        // A soft dark patch: the ground this fight has already trampled and burned.
        // Uses a SpriteRenderer (like the combatants' contact shadows) rather than a Lit/Unlit quad —
        // URP's _Surface float alone does NOT reconfigure blending, so a quad rendered opaque black.
        private void BuildScorchRing(Transform root)
        {
            var go = new GameObject("ScorchRing");
            go.transform.SetParent(root, false);
            go.transform.position = ringCenter;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ScorchSprite();
            sr.color = new Color(0.14f, 0.11f, 0.08f, 0.5f);
            sr.sortingOrder = -2;                       // under the combatants' shadow blobs
            float scale = (ringRadius * 2f) / (128f / 100f);   // sprite is 128px @ 100 ppu
            go.transform.localScale = new Vector3(scale, scale * 0.72f, 1f);   // foreshortened for the 3/4 view
        }

        private static Sprite scorchSprite;
        private static Sprite ScorchSprite()
        {
            if (scorchSprite != null) return scorchSprite;
            var tex = ScorchTexture();
            scorchSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return scorchSprite;
        }

        private static Texture2D scorchTex;
        private static Texture2D ScorchTexture()
        {
            if (scorchTex != null) return scorchTex;
            const int s = 128;
            scorchTex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var c = new Vector2(s / 2f, s / 2f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (s / 2f);
                    // Darkest at the centre where the fighting is, fading out well before the rim,
                    // with noise so the edge is ragged rather than a drawn circle.
                    float edge = 1f - Mathf.SmoothStep(0.10f, 0.92f, d);
                    float noise = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.5f + 0.5f;
                    float aVal = Mathf.Clamp01(edge * noise);
                    scorchTex.SetPixel(x, y, new Color(1f, 1f, 1f, aVal));
                }
            scorchTex.Apply();
            return scorchTex;
        }

        // Four fire pots at the corners of the arena: they frame the stage, justify the warm key
        // light, and give the eye something alive in the mid-ground.
        private void BuildBraziers(Transform root)
        {
            Vector3[] spots = {
                new Vector3(-7.2f, 0f, -4.6f), new Vector3(7.6f, 0f, -4.2f),
                new Vector3(-7.6f, 0f, 5.0f),  new Vector3(8.0f, 0f, 5.4f)
            };
            var stoneShader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var p in spots)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Brazier";
                var pc = post.GetComponent<Collider>(); if (pc != null) Destroy(pc);
                post.transform.SetParent(root, false);
                post.transform.position = p + Vector3.up * 0.55f;
                post.transform.localScale = new Vector3(0.42f, 0.55f, 0.42f);
                var m = new Material(stoneShader);
                m.SetColor("_BaseColor", new Color(0.13f, 0.12f, 0.12f));
                m.SetFloat("_Smoothness", 0.1f);
                post.GetComponent<MeshRenderer>().sharedMaterial = m;

                var lightGo = new GameObject("BrazierLight");
                lightGo.transform.SetParent(post.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.48f, 0.16f);
                l.intensity = brazierIntensity; l.range = 9f; l.shadows = LightShadows.None;
                lightGo.AddComponent<FlickerLight>();

                BuildFlame(post.transform);
            }
        }

        private void BuildFlame(Transform parent)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            go.transform.localScale = new Vector3(2.4f, 1.8f, 2.4f);   // undo the cylinder's squash
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(3f, 1.35f, 0.35f), new Color(2.2f, 0.7f, 0.12f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60; main.gravityModifier = -0.35f;
            var em = ps.emission; em.rateOverTime = 26f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f; shape.radius = 0.14f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 1f) },
                         new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = GlowMaterial();
            rend.sortingOrder = 3;
            ps.Play();
        }

        // Slow embers drifting through the whole arena — free atmosphere, sells the scale of the air.
        private void BuildEmbers(Transform root)
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0.5f, 1.2f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(3.2f, 1.4f, 0.45f), new Color(2.4f, 0.85f, 0.2f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260; main.gravityModifier = -0.015f;
            var em = ps.emission; em.rateOverTime = 26f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 3f, 14f);
            var vel = ps.velocityOverLifetime; vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            // ALL THREE AXES, AND ALL IN THE SAME MODE. Unity requires the x/y/z of a velocity
            // module to share one MinMaxCurve mode; setting only x left y and z at their default
            // Constant(0) while x became TwoConstants, which logs "Particle Velocity curves must
            // all be in the same mode" every time the system is built. The two-argument
            // MinMaxCurve constructor is what selects TwoConstants, so y and z have to use it too
            // even where the range is zero.
            vel.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);          // lift comes from gravityModifier
            vel.z = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.15f), 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = GlowMaterial();
            rend.sortingOrder = 4;
            ps.Play();
        }

        // Hide the bright meadow flowers standing inside the fight. A dragon duel in a flowerbed is
        // the single biggest reason the stage read as "garden" instead of "arena".
        private void CullVegetation()
        {
            int hidden = 0;
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                string n = r.gameObject.name;
                if (n.IndexOf("Flower", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var p = r.transform.position;
                float d = new Vector2(p.x - ringCenter.x, p.z - ringCenter.z).magnitude;
                if (d < ringRadius * 1.15f) { r.enabled = false; hidden++; }
            }
            if (hidden > 0) Debug.Log($"[ArenaDressing] cleared {hidden} flowers from the battlefield.");
        }

        private static Material glowMat;
        private static Material GlowMaterial()
        {
            if (glowMat != null) return glowMat;
            glowMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = GlowTex() };
            return glowMat;
        }

        private static Texture2D glowTex;
        private static Texture2D GlowTex()
        {
            if (glowTex != null) return glowTex;
            const int s = 32;
            glowTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var c = new Vector2(s / 2f, s / 2f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (s / 2f);
                    float aVal = Mathf.Clamp01(1f - d); aVal *= aVal;
                    glowTex.SetPixel(x, y, new Color(1, 1, 1, aVal));
                }
            glowTex.Apply();
            return glowTex;
        }
    }

    // Firelight never sits still. Cheap per-brazier noise, desynced so the four don't pulse together.
    public class FlickerLight : MonoBehaviour
    {
        private Light l;
        private float baseIntensity, seed;

        private void Awake()
        {
            l = GetComponent<Light>();
            baseIntensity = l != null ? l.intensity : 1f;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            if (l == null) return;
            float n = Mathf.PerlinNoise(seed, Time.time * 2.1f);
            l.intensity = baseIntensity * Mathf.Lerp(0.78f, 1.16f, n);
        }
    }
}
