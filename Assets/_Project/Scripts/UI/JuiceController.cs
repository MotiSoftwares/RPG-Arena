using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.Events;

namespace RPGArena.UI
{
    // "Juice it or lose it" (§12). Pure PRESENTATION: it subscribes to the SO event channels and
    // turns combat results into game-feel — floating damage numbers, hit-stop, camera shake, a
    // screen flash, and the BREAK slow-mo spectacle. It never touches combat logic, and every
    // effect degrades gracefully (a missing camera or canvas just skips that effect).
    public class JuiceController : MonoBehaviour
    {
        [Header("Channels (subscribed)")]
        public DamageResultChannel onDamageDealt;
        public EntityChannel onStaggerBroken;
        public EntityChannel onEntityDied;
        public AbilityChannel onBossTelegraph;
        public Core.Events.VoidChannel onBattleWon;

        [Header("Hit-stop (freeze-frame on impact, §12.1)")]
        public float hitStopNormal = 0.04f;
        public float hitStopCrit = 0.10f;

        [Header("Camera shake")]
        public float shakeOnHit = 0.10f;
        public float shakeOnCrit = 0.28f;
        public float shakeOnBreak = 0.55f;
        public float shakeDecay = 4.5f;

        [Header("Screen flash")]
        public float flashOnCrit = 0.35f;
        public float flashOnBreak = 0.6f;
        public float flashDecay = 3.2f;

        [Header("BREAK slow-mo (§12.3)")]
        public float breakSlowMoScale = 0.35f;
        public float breakSlowMoDuration = 0.7f;

        [Header("Floating text")]
        public float floatLife = 0.9f;
        public float floatRise = 90f;

        // Colour language (§9.8): white normal, orange crit, cyan weak, grey resist/miss, green heal.
        private static readonly Color CNormal = Color.white;
        private static readonly Color CCrit = new Color(1f, 0.55f, 0.1f);
        private static readonly Color CWeak = new Color(0.35f, 0.9f, 1f);
        private static readonly Color CResist = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color CHeal = new Color(0.35f, 0.95f, 0.4f);

        private Camera cam;
        private Vector3 camBasePos;
        private float shakeAmount;
        private float flashAmount;
        private bool timeEffectActive;

        private Canvas canvas;
        private RectTransform canvasRect;
        private Image flashImage;
        private Text breakBanner;
        private Font font;
        private readonly Queue<Text> pool = new();   // reuse popups instead of churning GC

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cam = Camera.main;
            if (cam != null) camBasePos = cam.transform.localPosition;
            BuildCanvas();
        }

        private void OnEnable()
        {
            onDamageDealt?.Subscribe(OnDamage);
            onStaggerBroken?.Subscribe(OnBreak);
            onBossTelegraph?.Subscribe(OnTelegraph);
            onEntityDied?.Subscribe(OnDied);
            onBattleWon?.Subscribe(OnWon);
        }

        private void OnDisable()
        {
            onDamageDealt?.Unsubscribe(OnDamage);
            onStaggerBroken?.Unsubscribe(OnBreak);
            onBossTelegraph?.Unsubscribe(OnTelegraph);
            onEntityDied?.Unsubscribe(OnDied);
            onBattleWon?.Unsubscribe(OnWon);
        }

        // Play the death animation on a fallen combatant's rigged model (no-op for billboards).
        private void OnDied(Characters.Entity e) => e?.GetComponentInChildren<Characters.AnimationDriver>()?.PlayDie();

        // On victory, every surviving hero plays its Victory pose.
        private void OnWon(bool _)
        {
            foreach (var e in FindObjectsByType<Characters.Entity>(FindObjectsSortMode.None))
                if (e.team == Characters.Team.Heroes && e.IsAlive)
                    e.GetComponentInChildren<Characters.AnimationDriver>()?.PlayVictory();
        }

        // --- channel handlers ---------------------------------------------------------
        private void OnDamage(DamageResult r)
        {
            if (r.target == null) return;
            Vector3 head = r.target.transform.position + Vector3.up * (r.target.isBoss ? 3.6f : 2.2f);

            string text; Color color; float size;
            if (!r.hit) { text = "MISS"; color = CResist; size = 26; }
            else if (r.isHeal || r.absorbed) { text = (r.absorbed ? "ABSORB +" : "+") + r.amount; color = CHeal; size = 30; }
            else if (r.crit) { text = r.amount + "!"; color = CCrit; size = 44; }
            else if (r.reaction == ElementReaction.Weak) { text = r.amount + "  WEAK!"; color = CWeak; size = 40; }
            else if (r.reaction == ElementReaction.Resist) { text = r.amount.ToString(); color = CResist; size = 26; }
            else { text = r.amount.ToString(); color = CNormal; size = 32; }

            SpawnFloating(head, text, color, size);

            // Elemental impact burst at the target (green for heals/absorbs).
            if (r.hit)
                SpawnVFX(r.target.transform.position + Vector3.up * (r.target.isBoss ? 2.0f : 1.2f),
                         (r.isHeal || r.absorbed) ? new Color(0.4f, 1f, 0.5f) : ElementColor(r.element),
                         r.crit ? 52 : 30);

            // Procedural motion + rigged-model animation: the attacker lunges + plays Attack;
            // the target recoils + plays its Hit reaction.
            if (r.hit && r.source != null && r.target != null)
            {
                Vector3 dir = r.target.transform.position - r.source.transform.position;
                r.source.GetComponent<Characters.CombatantMotion>()?.Lunge(dir);
                // Pick the attacker's animation from the ability: AoE -> area, magic -> cast, else attack.
                var drv = r.source.GetComponentInChildren<Characters.AnimationDriver>();
                if (drv != null)
                {
                    if (r.ability != null && r.ability.targetRule == TargetRule.AllEnemies) drv.PlayAreaAttack();
                    else if (r.ability != null && r.ability.isMagic) drv.PlayCast();
                    else drv.PlayAttack();
                }
                if (!r.isHeal && !r.absorbed)
                {
                    r.target.GetComponent<Characters.CombatantMotion>()?.Recoil(dir);
                    r.target.GetComponentInChildren<Characters.AnimationDriver>()?.PlayHit();
                }
            }

            // Impact feedback scales with the hit's weight.
            if (r.hit && !r.isHeal && !r.absorbed)
            {
                HitStop(r.crit ? hitStopCrit : hitStopNormal);
                shakeAmount = Mathf.Max(shakeAmount, r.crit ? shakeOnCrit : (r.reaction == ElementReaction.Weak ? shakeOnCrit * 0.8f : shakeOnHit));
                if (r.crit) flashAmount = Mathf.Max(flashAmount, flashOnCrit);
            }
        }

        private void OnBreak(Entity boss)
        {
            shakeAmount = Mathf.Max(shakeAmount, shakeOnBreak);
            flashAmount = Mathf.Max(flashAmount, flashOnBreak);
            StartCoroutine(BreakSpectacle());
        }

        private void OnTelegraph(Ability a)
        {
            // A small shake to sell the wind-up; the HUD shows the warning banner text.
            shakeAmount = Mathf.Max(shakeAmount, shakeOnHit * 0.6f);
        }

        // --- effects ------------------------------------------------------------------
        private void HitStop(float duration)
        {
            if (!timeEffectActive) StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            timeEffectActive = true;
            float prev = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = prev;
            timeEffectActive = false;
        }

        private IEnumerator BreakSpectacle()
        {
            // Slow-mo window + the big "BREAK!" banner punching in.
            timeEffectActive = true;
            Time.timeScale = breakSlowMoScale;

            if (breakBanner != null)
            {
                breakBanner.gameObject.SetActive(true);
                breakBanner.color = new Color(1f, 0.85f, 0.2f, 1f);
                float t = 0f;
                while (t < breakSlowMoDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float k = t / breakSlowMoDuration;
                    breakBanner.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, Mathf.Clamp01(k * 3f));
                    breakBanner.color = new Color(1f, 0.85f, 0.2f, 1f - Mathf.Clamp01((k - 0.6f) / 0.4f));
                    yield return null;
                }
                breakBanner.gameObject.SetActive(false);
            }
            else
            {
                yield return new WaitForSecondsRealtime(breakSlowMoDuration);
            }

            Time.timeScale = 1f;
            timeEffectActive = false;
        }

        private void LateUpdate()
        {
            // Camera shake: a decaying random offset on the cached base position (unscaled so it
            // still moves during hit-stop's freeze).
            if (cam != null)
            {
                if (shakeAmount > 0.0001f)
                {
                    Vector2 o = Random.insideUnitCircle * shakeAmount;
                    cam.transform.localPosition = camBasePos + new Vector3(o.x, o.y, 0f);
                    shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, shakeDecay * Time.unscaledDeltaTime);
                }
                else cam.transform.localPosition = camBasePos;
            }

            if (flashImage != null)
            {
                flashAmount = Mathf.MoveTowards(flashAmount, 0f, flashDecay * Time.unscaledDeltaTime);
                flashImage.color = new Color(1f, 1f, 1f, flashAmount);
            }
        }

        // --- elemental VFX ------------------------------------------------------------
        private static Color ElementColor(ElementType e)
        {
            switch (e)
            {
                case ElementType.Fire: return new Color(1f, 0.45f, 0.1f);
                case ElementType.Ice: return new Color(0.4f, 0.8f, 1f);
                case ElementType.Lightning: return new Color(1f, 0.95f, 0.3f);
                case ElementType.Holy: return new Color(1f, 0.95f, 0.6f);
                case ElementType.Dark: return new Color(0.65f, 0.3f, 0.95f);
                default: return new Color(1f, 0.95f, 0.85f);   // physical
            }
        }

        // A short-lived, self-destroying particle burst tinted to the element. Built in code so it
        // needs no imported VFX assets; uses a soft glow texture on a URP-safe sprite shader.
        private void SpawnVFX(Vector3 pos, Color color, int count)
        {
            var go = new GameObject("VFX");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.duration = 0.6f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = 0.55f; main.startSpeed = 3.2f;
            main.startSize = 0.45f; main.startColor = color;
            main.maxParticles = 80; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.gravityModifier = 0.15f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.35f;

            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = VfxMaterial();
            rend.sortingOrder = 10;

            ps.Play();
        }

        private static Material vfxMaterial;
        private static Material VfxMaterial()
        {
            if (vfxMaterial != null) return vfxMaterial;
            // "Sprites/Default" is URP-safe, alpha-blended, and respects the particle's vertex colour.
            var sh = Shader.Find("Sprites/Default");
            vfxMaterial = new Material(sh) { mainTexture = GlowTexture() };
            return vfxMaterial;
        }

        private static Texture2D glowTexture;
        private static Texture2D GlowTexture()
        {
            if (glowTexture != null) return glowTexture;
            const int s = 32;
            glowTexture = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var c = new Vector2(s / 2f, s / 2f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (s / 2f);
                    float a = Mathf.Clamp01(1f - d); a = a * a;
                    glowTexture.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            glowTexture.Apply();
            return glowTexture;
        }

        // --- floating text ------------------------------------------------------------
        private void SpawnFloating(Vector3 worldPos, string text, Color color, float size)
        {
            if (canvas == null || cam == null) return;
            var t = pool.Count > 0 ? pool.Dequeue() : MakePopup();
            t.text = text; t.color = color; t.fontSize = Mathf.RoundToInt(size);
            t.gameObject.SetActive(true);
            t.transform.localScale = Vector3.one;
            StartCoroutine(AnimatePopup(t, worldPos));
        }

        private IEnumerator AnimatePopup(Text t, Vector3 worldPos)
        {
            float life = 0f;
            var rt = t.rectTransform;
            Vector3 screen = cam.WorldToScreenPoint(worldPos);
            float jitterX = Random.Range(-26f, 26f);
            while (life < floatLife)
            {
                life += Time.unscaledDeltaTime;
                float k = life / floatLife;
                // Punch-in scale then settle, rise upward, fade out at the end.
                float scale = k < 0.18f ? Mathf.Lerp(1.5f, 1f, k / 0.18f) : 1f;
                rt.localScale = Vector3.one * scale;
                Vector3 s = cam.WorldToScreenPoint(worldPos);
                rt.position = new Vector3(s.x + jitterX, s.y + k * floatRise, 0f);
                var c = t.color; c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f; t.color = c;
                yield return null;
            }
            t.gameObject.SetActive(false);
            pool.Enqueue(t);
        }

        private Text MakePopup()
        {
            var go = new GameObject("Popup");
            go.transform.SetParent(canvasRect, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, 0.85f); outline.effectDistance = new Vector2(2, -2);
            t.rectTransform.sizeDelta = new Vector2(300, 60);
            return t;
        }

        // --- canvas -------------------------------------------------------------------
        private void BuildCanvas()
        {
            var go = new GameObject("JuiceCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;                       // above the HUD so numbers read on top
            go.AddComponent<GraphicRaycaster>();
            canvasRect = go.GetComponent<RectTransform>();

            // Full-screen white flash image (transparent until pulsed); never blocks clicks.
            var fg = new GameObject("Flash");
            fg.transform.SetParent(canvasRect, false);
            flashImage = fg.AddComponent<Image>();
            flashImage.color = new Color(1, 1, 1, 0f);
            flashImage.raycastTarget = false;
            var frt = flashImage.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

            // Centre "BREAK!" banner (hidden until a stagger break).
            var bg = new GameObject("BreakBanner");
            bg.transform.SetParent(canvasRect, false);
            breakBanner = bg.AddComponent<Text>();
            breakBanner.font = font; breakBanner.fontSize = 110; breakBanner.fontStyle = FontStyle.Bold;
            breakBanner.alignment = TextAnchor.MiddleCenter; breakBanner.text = "BREAK!";
            breakBanner.color = new Color(1f, 0.85f, 0.2f);
            breakBanner.raycastTarget = false;
            breakBanner.horizontalOverflow = HorizontalWrapMode.Overflow; breakBanner.verticalOverflow = VerticalWrapMode.Overflow;
            var bo = bg.AddComponent<Outline>(); bo.effectColor = new Color(0.4f, 0.1f, 0f, 0.9f); bo.effectDistance = new Vector2(4, -4);
            var brt = breakBanner.rectTransform; brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0, 60); brt.sizeDelta = new Vector2(900, 200);
            bg.SetActive(false);
        }
    }
}
