using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        private float camBaseFov;
        private float fovPunch;        // degrees subtracted from base FOV on crit/break (eases back, unscaled)
        private float breakPunch;      // 0..1 dolly push toward the action on a Break
        private static readonly Vector3 BreakDolly = new Vector3(0.8f, -0.25f, 1.6f);  // local push: right/down/forward
        private float shakeAmount;
        private float flashAmount;
        private bool timeEffectActive;

        private Canvas canvas;
        private RectTransform canvasRect;
        private Image flashImage;
        private TMP_Text breakBanner;
        [SerializeField] private TMP_FontAsset uiFont;     // SlimUI Poppins-Bold (wired in scene); falls back to TMP default
        private TMP_FontAsset font;
        private Material popupMat;                          // shared outlined SDF material so numbers read over the scene
        private readonly Queue<TMP_Text> pool = new();     // reuse popups instead of churning GC

        private void Awake()
        {
            font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
            popupMat = BuildOutlineMaterial(font);
            cam = Camera.main;
            if (cam != null) { camBasePos = cam.transform.localPosition; camBaseFov = cam.fieldOfView; }
            BuildCanvas();
        }

        // A crisp dark outline so floating numbers stay legible over the bright meadow without a panel.
        private static Material BuildOutlineMaterial(TMP_FontAsset f)
        {
            if (f == null || f.material == null) return null;
            var m = new Material(f.material);
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.22f);
            m.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0.92f));
            return m;
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
        private static float nextBeat;   // shared timeline so multi-hit impacts play in sequence

        // Presentation is a timed SEQUENCE (anticipation/wind-up + extended recovery): the attacker
        // approaches + winds up, THEN the spell/impact VFX + damage number + hit-stop land on the
        // contact beat, THEN the target reacts. The combat loop waits on
        // BattleController.PresentationBusyUntil so the next turn doesn't start mid-strike.
        private void OnDamage(DamageResult r)
        {
            if (r.target == null) return;
            StartCoroutine(AttackBeat(r));
        }

        private IEnumerator AttackBeat(DamageResult r)
        {
            // Schedule on a shared timeline so several hits resolved in one frame play one-by-one.
            float start = Mathf.Max(Time.time, nextBeat);
            nextBeat = start + 0.24f;
            BattleController.PresentationBusyUntil = Mathf.Max(BattleController.PresentationBusyUntil, start + 0.95f);
            float lead = start - Time.time;
            if (lead > 0f) yield return new WaitForSeconds(lead);

            TargetAnchors(r.target, r.target.isBoss ? 1.6f : 1.0f, r.target.isBoss ? 3.6f : 2.2f, out Vector3 bodyCenter, out Vector3 head);
            Vector3 dir = r.source != null ? (r.target.transform.position - r.source.transform.position) : Vector3.forward;

            // APPROACH + WIND-UP: melee attackers dash in; ranged/magic stay put. Play the swing/cast.
            bool melee = false;
            if (r.hit && r.source != null)
            {
                var srcMotion = r.source.GetComponent<Characters.CombatantMotion>();
                melee = r.ability != null && !r.ability.isMagic
                        && r.ability.targetRule != TargetRule.AllEnemies
                        && r.source.team != r.target.team;
                if (melee && srcMotion != null)
                {
                    float gap = new Vector2(dir.x, dir.z).magnitude;
                    srcMotion.DashStrike(dir, Mathf.Max(0.6f, gap - 2.4f));   // RUN in to the foe, strike, run back
                }
                else srcMotion?.Lunge(dir);
                var drv = r.source.GetComponentInChildren<Characters.AnimationDriver>();
                if (drv != null)
                {
                    if (r.ability != null && r.ability.targetRule == TargetRule.AllEnemies) drv.PlayAreaAttack();
                    else if (r.ability != null && r.ability.isMagic) drv.PlayCast();
                    else drv.PlayAttack();
                }
            }

            // Wind-up window before contact. The Animator's attack/cast trigger blends in then the
            // swing CONNECTS ~0.35-0.45s later — firing the impact at the old ~0.17s landed the VFX +
            // damage while the hero was still mid-windup (the "not synced" look). Wait for the swing.
            yield return new WaitForSeconds(melee ? 0.38f : 0.42f);

            // IMPACT beat: VFX blooms on the target body, damage number pops, hit-stop + shake fire.
            string text; Color color; float size;
            if (!r.hit) { text = "MISS"; color = CResist; size = 26; }
            else if (r.isHeal || r.absorbed) { text = (r.absorbed ? "ABSORB +" : "+") + r.amount; color = CHeal; size = 32; }
            else if (r.crit) { text = r.amount + "!"; color = CCrit; size = 48; }
            else if (r.reaction == ElementReaction.Weak) { text = r.amount + "  WEAK!"; color = CWeak; size = 44; }
            else if (r.reaction == ElementReaction.Resist) { text = r.amount.ToString(); color = CResist; size = 28; }
            else { text = r.amount.ToString(); color = CNormal; size = 36; }
            SpawnFloating(head, text, color, size);

            // COMBO CALLOUT: when a synergy fires, announce it in gold above the number so the player
            // LEARNS the combo web (SHATTER / BRITTLE / WET+PHYSICAL / FREEZE) — the heart of the design.
            if (r.hit && !string.IsNullOrEmpty(r.synergyNote))
            {
                bool big = r.synergyNote.Contains("SHATTER") || r.synergyNote.Contains("Brittle");
                SpawnFloating(head + Vector3.up * 0.7f, r.synergyNote.ToUpper(), new Color(1f, 0.84f, 0.32f), big ? 38 : 30);
                if (big) { shakeAmount = Mathf.Max(shakeAmount, shakeOnCrit * 0.7f); fovPunch = Mathf.Max(fovPunch, 6f); }
            }

            // The SPECIAL's risk-die reveal — a colour-coded d20 result floats off the gambler, and
            // the camera/flash sell the swing: a Jackpot punches in, a Backfire flashes red + shakes.
            if (r.risked)
            {
                Color dc = r.riskBand == RiskBand.Backfire ? new Color(1f, 0.32f, 0.32f)
                         : r.riskBand == RiskBand.Whiff ? new Color(0.72f, 0.72f, 0.72f)
                         : r.riskBand == RiskBand.Big ? new Color(1f, 0.85f, 0.3f)
                         : r.riskBand == RiskBand.Jackpot ? new Color(0.45f, 1f, 1f) : Color.white;
                Vector3 dicePos = r.source != null ? r.source.transform.position + Vector3.up * 2.3f : head + Vector3.up * 0.7f;
                bool loud = r.riskBand == RiskBand.Jackpot || r.riskBand == RiskBand.Backfire;
                SpawnFloating(dicePos, $"d20: {r.riskFace}  {r.riskBand.ToString().ToUpper()}", dc, loud ? 40 : 30);
                if (r.riskBand == RiskBand.Backfire) { flashAmount = Mathf.Max(flashAmount, 0.3f); shakeAmount = Mathf.Max(shakeAmount, shakeOnCrit); }
                else if (r.riskBand == RiskBand.Jackpot) fovPunch = Mathf.Max(fovPunch, 12f);
            }

            if (r.hit)
            {
                // Always spawn a reliable elemental impact BURST on the target body — guarantees a
                // visible hit even when the ability's authored prefab is a fly-by projectile.
                Color burstCol = (r.isHeal || r.absorbed) ? new Color(0.4f, 1f, 0.5f) : ElementColor(r.element);
                ElementType vfxEl = (r.isHeal || r.absorbed) ? ElementType.Holy : r.element;
                SpawnVFX(bodyCenter, burstCol, vfxEl, r.crit ? 64 : 44, r.target.isBoss ? 2.4f : 1.25f);
                // Layer the ability's authored effect on top (impact/explosion prefabs read strongly here).
                if (r.ability != null && r.ability.vfxPrefab != null)
                {
                    var fx = Instantiate(r.ability.vfxPrefab, bodyCenter, Quaternion.identity);
                    fx.transform.localScale *= r.target.isBoss ? 1.6f : 1.25f;
                    Destroy(fx, 4f);
                }
            }

            if (r.hit && !r.isHeal && !r.absorbed && r.source != null)
            {
                float recoilScale = r.crit ? 1.8f : (r.reaction == ElementReaction.Weak ? 1.4f : 1f);   // weight the flinch to the hit
                r.target.GetComponent<Characters.CombatantMotion>()?.Recoil(dir, recoilScale);
                r.target.GetComponentInChildren<Characters.AnimationDriver>()?.PlayHit();
                HitStop(r.crit ? hitStopCrit : hitStopNormal);
                shakeAmount = Mathf.Max(shakeAmount, r.crit ? shakeOnCrit : (r.reaction == ElementReaction.Weak ? shakeOnCrit * 0.8f : shakeOnHit));
                if (r.crit) { flashAmount = Mathf.Max(flashAmount, flashOnCrit); fovPunch = Mathf.Max(fovPunch, 9f); }   // crit snaps the camera in
                else if (r.reaction == ElementReaction.Weak) fovPunch = Mathf.Max(fovPunch, 4.5f);
            }
        }

        // World anchors for presentation on a target: where its spell VFX lands (the model's bounds
        // centre) and where its damage number floats (just above the model). Measured from the
        // renderers so both auto-scale with the combatant's size — so effects land ON a much-bigger
        // boss dragon instead of at its feet. Falls back to fixed offsets when there are no renderers.
        private static void TargetAnchors(Entity e, float vfxFallbackUp, float textFallbackUp, out Vector3 vfxPos, out Vector3 textPos)
        {
            var body = e.transform.Find("Body");
            if (body != null)
            {
                var rends = body.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    // Bias the VFX up toward the upper torso — the geometric centre sits low on
                    // bottom-heavy models (e.g. the dragon), which would land effects at its belly.
                    vfxPos = new Vector3(b.center.x, Mathf.Lerp(b.center.y, b.max.y, 0.45f), b.center.z);
                    textPos = new Vector3(b.center.x, b.max.y + 0.5f, b.center.z);
                    return;
                }
            }
            vfxPos = e.transform.position + Vector3.up * vfxFallbackUp;
            textPos = e.transform.position + Vector3.up * textFallbackUp;
        }

        private void OnBreak(Entity boss)
        {
            shakeAmount = Mathf.Max(shakeAmount, shakeOnBreak);
            flashAmount = Mathf.Max(flashAmount, flashOnBreak);
            fovPunch = Mathf.Max(fovPunch, 14f); breakPunch = 1f;   // big FOV snap + a slow dolly toward the action
            // The break slow-mo QUEUES behind any in-flight hit-stop (never dropped, never races it).
            StartCoroutine(TimeEffect(breakSlowMoScale, breakSlowMoDuration, true));
        }

        private void OnTelegraph(Ability a)
        {
            // A small shake to sell the wind-up; the HUD shows the warning banner text.
            shakeAmount = Mathf.Max(shakeAmount, shakeOnHit * 0.6f);
        }

        // --- effects ------------------------------------------------------------------
        // A tiny freeze-frame on impact. Dropped (not queued) if a bigger time effect already owns
        // the clock — the break slow-mo during a crit is dramatic enough without stacking a freeze.
        private void HitStop(float duration)
        {
            if (timeEffectActive) return;
            StartCoroutine(TimeEffect(0f, duration, false));
        }

        // THE single owner of Time.timeScale (§12.1/§12.3). Only one of these ever runs at a time —
        // a new effect WAITS for the in-flight one to finish, so HitStop (0) and the BREAK slow-mo
        // (0.35) can never interleave and leave the game stuck frozen or in permanent slow-mo. The
        // try/finally guarantees the clock is restored to 1 even if the coroutine is interrupted.
        private IEnumerator TimeEffect(float scale, float duration, bool banner)
        {
            while (timeEffectActive) yield return null;   // serialize: one owner of the clock
            timeEffectActive = true;
            try
            {
                Time.timeScale = scale;
                if (banner && breakBanner != null)
                {
                    breakBanner.gameObject.SetActive(true);
                    float t = 0f;
                    while (t < duration)
                    {
                        t += Time.unscaledDeltaTime;
                        float k = t / duration;
                        breakBanner.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, Mathf.Clamp01(k * 3f));
                        breakBanner.color = new Color(1f, 0.85f, 0.2f, 1f - Mathf.Clamp01((k - 0.6f) / 0.4f));
                        yield return null;
                    }
                    breakBanner.gameObject.SetActive(false);
                }
                else
                {
                    yield return new WaitForSecondsRealtime(duration);
                }
            }
            finally
            {
                Time.timeScale = 1f;
                timeEffectActive = false;
            }
        }

        private void LateUpdate()
        {
            // Action camera: FOV punch (crit/break) + a Break dolly push, plus decaying shake — all on
            // UNSCALED time so the move animates through the hit-stop freeze and the BREAK slow-mo. The
            // base pos/FOV are restored as the punches ease to zero, so this never deadlocks the framing.
            if (cam != null)
            {
                fovPunch = Mathf.MoveTowards(fovPunch, 0f, 26f * Time.unscaledDeltaTime);
                breakPunch = Mathf.MoveTowards(breakPunch, 0f, 1.45f * Time.unscaledDeltaTime);
                cam.fieldOfView = camBaseFov - fovPunch;

                Vector3 shakeOff = Vector3.zero;
                if (shakeAmount > 0.0001f)
                {
                    Vector2 o = Random.insideUnitCircle * shakeAmount;
                    shakeOff = new Vector3(o.x, o.y, 0f);
                    shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, shakeDecay * Time.unscaledDeltaTime);
                }
                cam.transform.localPosition = camBasePos + BreakDolly * breakPunch + shakeOff;
            }

            if (flashImage != null)
            {
                flashAmount = Mathf.MoveTowards(flashAmount, 0f, flashDecay * Time.unscaledDeltaTime);
                flashImage.color = new Color(1f, 1f, 1f, flashAmount);
            }
        }

        // --- elemental VFX ------------------------------------------------------------
        // Push a colour into HDR (>1) so the bright impact core exceeds the Bloom threshold (0.9)
        // and actually GLOWS over the sunlit meadow instead of washing out as a flat decal.
        private static Color Hdr(Color c, float intensity) => new Color(c.r * intensity, c.g * intensity, c.b * intensity, c.a);

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
        private void SpawnVFX(Vector3 pos, Color color, ElementType element, int count, float scale = 1f)
        {
            // Per-element MOTION identity (not just tint): fire licks upward, frost drifts slow & cool,
            // lightning snaps fast & jagged, holy motes rise softly, dark wisps implode. Physical = the
            // generic spark-puff. All code-built — no new assets, no logic/test surface touched.
            float gravity = 0.6f, spMin = 2.5f, spMax = 6.5f, lifeMin = 0.3f, lifeMax = 0.6f;
            float radius = 0.2f, trailRatio = 0.7f, trailW = 0.4f, coreWhite = 0.5f, hdr = 2.6f, flash = 1.5f;
            bool hotCore = true;
            switch (element)
            {
                case ElementType.Fire:      gravity = -0.7f; spMin = 1.5f; spMax = 4.5f; lifeMax = 0.7f; trailRatio = 0.9f; trailW = 0.3f; break;
                case ElementType.Ice:       gravity = 0.05f; spMin = 1.2f; spMax = 3.5f; lifeMin = 0.4f; lifeMax = 0.8f; radius = 0.28f; trailRatio = 0.22f; hotCore = false; coreWhite = 0.25f; hdr = 1.8f; break;
                case ElementType.Lightning: gravity = 0f; spMin = 7f; spMax = 13f; lifeMin = 0.07f; lifeMax = 0.2f; radius = 0.1f; trailRatio = 1f; trailW = 0.16f; flash = 2.1f; break;
                case ElementType.Holy:      gravity = -0.35f; spMin = 0.8f; spMax = 2.2f; lifeMin = 0.5f; lifeMax = 0.9f; trailRatio = 0.3f; hotCore = false; coreWhite = 0.7f; hdr = 1.9f; break;
                case ElementType.Dark:      gravity = 0f; spMin = -5f; spMax = -1.5f; radius = 0.5f; trailRatio = 0.6f; hotCore = false; coreWhite = 0.25f; hdr = 1.8f; break;
                default: break;   // Physical
            }

            var go = new GameObject("VFX");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.duration = 0.7f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin * scale, lifeMax * scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(spMin * scale, spMax * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f * scale, 0.5f * scale);
            main.startColor = Hdr(color, hdr);   // HDR core -> blooms on impact
            main.maxParticles = 250; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.gravityModifier = gravity;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius * scale;

            // Core (hot-white for fire/physical/lightning, a pale element tint for frost/holy/dark)
            // cools to the element colour, then fades — gives the hit a "flash".
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            Color core = hotCore ? Color.white : Color.Lerp(color, Color.white, coreWhite);
            grad.SetKeys(
                new[] { new GradientColorKey(core, 0f), new GradientColorKey(Color.Lerp(color, Color.white, coreWhite * 0.6f), 0.25f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            // Pop big on contact, then shrink.
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1.25f, 1f, 0f));

            // Trails: thick & energetic for fire/physical, hair-thin jagged streaks for lightning, faint for frost.
            var trails = ps.trails; trails.enabled = true; trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.25f); trails.dieWithParticles = true; trails.ratio = trailRatio;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(trailW); trails.inheritParticleColor = true;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = VfxMaterial();
            rend.trailMaterial = VfxMaterial();
            rend.sortingOrder = 10;

            ps.Play();

            SpawnFlash(pos, color, flash * scale);   // a bright flash disc at the contact point
        }

        // A short, bright disc that pops then fades — the impact flash that sells the hit.
        private void SpawnFlash(Vector3 pos, Color color, float size)
        {
            var go = new GameObject("Flash");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.duration = 0.3f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = 0.18f; main.startSpeed = 0f;
            main.startSize = size; main.startColor = Hdr(Color.Lerp(color, Color.white, 0.6f), 2.4f);
            main.maxParticles = 2; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
            var shape = ps.shape; shape.enabled = false;
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.7f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                         new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = VfxMaterial(); rend.sortingOrder = 11;
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

        private IEnumerator AnimatePopup(TMP_Text t, Vector3 worldPos)
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

        private TMP_Text MakePopup()
        {
            var go = new GameObject("Popup");
            go.transform.SetParent(canvasRect, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
            if (popupMat != null) t.fontSharedMaterial = popupMat;
            t.rectTransform.sizeDelta = new Vector2(320, 64);
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
            breakBanner = bg.AddComponent<TextMeshProUGUI>();
            breakBanner.font = font; breakBanner.fontSize = 110; breakBanner.fontStyle = FontStyles.Bold;
            breakBanner.alignment = TextAlignmentOptions.Center; breakBanner.text = "BREAK!";
            breakBanner.color = new Color(1f, 0.85f, 0.2f);
            breakBanner.raycastTarget = false;
            breakBanner.enableWordWrapping = false; breakBanner.overflowMode = TextOverflowModes.Overflow;
            if (popupMat != null) breakBanner.fontSharedMaterial = popupMat;
            var brt = breakBanner.rectTransform; brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0, 60); brt.sizeDelta = new Vector2(900, 200);
            bg.SetActive(false);
        }
    }
}
