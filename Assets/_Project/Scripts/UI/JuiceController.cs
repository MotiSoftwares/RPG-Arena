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
    public class JuiceController : MonoBehaviour, IActionCamera
    {
        [Header("Channels (subscribed)")]
        public DamageResultChannel onDamageDealt;
        public EntityChannel onStaggerBroken;
        public EntityChannel onEntityDied;
        public EntityChannel onTurnStarted;      // drives the actor-focus punch-in
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

        [Header("Melee slash arcs (Hovl 'Slash effects' — the only URP-clean part of that pack)")]
        public GameObject slashNormal;    // Stone slash — plain physical connect
        public GameObject slashCrit;      // Charge slash red — crits
        public GameObject slashShatter;   // Snow slash — the SHATTER detonation

        [Header("Ultimate wind-up")]
        // Gathering VFX played at the caster's feet while an ultimate charges. Assigned in the scene
        // (an Erb 'Effects normal/' buff prefab); the charge-up degrades to camera-only if unset.
        public GameObject chargeUpFx;

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
        private Vector3 focusOffset;   // the actor-focus lean, eased toward focusDesired then home
        private Vector3 focusDesired;
        private float focusFovBias;    // current focus FOV tighten (degrees)
        private float focusFovDesired;
        private float focusHold;       // seconds left before the lean eases back home
        private static readonly Vector3 BreakDolly = new Vector3(0.8f, -0.25f, 1.6f);  // local push: right/down/forward
        private float shakeAmount;
        private float flashAmount;
        private Vector3 kickOffset;    // directional camera kick along the blow — decays in LateUpdate
        private bool timeEffectActive;

        // One in-flight body flash: the victim's renderers tinted white-hot at contact, easing back
        // to their exact original colours. MaterialPropertyBlocks so the shared materials are never
        // touched, and the block is CLEARED at the end so the renderer returns to its authored state.
        private class BodyFlash
        {
            public Renderer[] rends;
            public Color[] baseCols;
            public float t, dur;
            public Color tint;
        }
        private readonly List<BodyFlash> bodyFlashes = new();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock sharedMpb;

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
            // Time.timeScale is GLOBAL and survives scene loads. If the previous battle was reloaded
            // mid-hit-stop, its TimeEffect coroutine died before its finally ran and left the clock
            // parked at 0 — and the watchdog below won't rescue it, because this fresh instance owns
            // no time effect. Reclaim the clock on entry (unless the player is legitimately paused).
            if (!PauseMenu.IsPaused && Time.timeScale < 0.999f) Time.timeScale = 1f;

            font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
            popupMat = BuildOutlineMaterial(font);
            cam = Camera.main;
            if (cam != null) { camBasePos = cam.transform.localPosition; camBaseFov = cam.fieldOfView; }
            BuildCanvas();
        }

        // THE camera-ownership seam: this controller rewrites the camera from its cached base every
        // LateUpdate (shake/dolly/FOV punches layer on top), so anything that wants to MOVE the
        // camera — the intro cinematic, a future boss-kill cam — must drive it through here rather
        // than fight the stomp. Passing the current values each frame animates the base smoothly.
        public void SetCameraBase(Vector3 localPos, float fov)
        {
            camBasePos = localPos;
            camBaseFov = fov;
        }
        public Vector3 CameraBasePos => camBasePos;
        public float CameraBaseFov => camBaseFov;

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
            onTurnStarted?.Subscribe(OnTurnFocus);
        }

        // The tiny action cinematic: when someone's turn starts, the camera leans toward them and
        // the FOV tightens a touch, then eases home. Composed INSIDE the LateUpdate stomp (this
        // class owns the camera), so it stacks safely with shake, hit-stop and the Break dolly.
        private void OnTurnFocus(Entity actor)
        {
            if (actor == null || cam == null) return;
            // ENEMIES ONLY. An enemy's turn start and its swing are the same beat, so focusing here
            // reads correctly. A HERO's turn start is the moment their action MENU opens — the
            // player then sits choosing a skill while the camera has already pushed in and eased
            // back out, so by the time they actually swing the shot is over. Heroes are focused
            // from AttackBeat/FocusOnActor instead, when the strike genuinely begins.
            if (actor.team != Characters.Team.Enemies) return;
            FocusOn(actor.transform.position + Vector3.up * 1.4f, 1.25f);
        }

        // Called by the battle loop the instant an action actually starts resolving, so the punch-in
        // lands on the character who is swinging rather than on whoever is browsing a menu.
        public void FocusOnActor(Entity actor)
        {
            if (actor == null || cam == null) return;
            FocusOn(actor.transform.position + Vector3.up * 1.4f,
                    actor.team == Characters.Team.Enemies ? 1.25f : 1f);
        }

        // The ultimate wind-up: a hard push-in on the caster plus a gathering effect at their feet.
        // Deliberately stronger and longer-held than a normal FocusOnActor — this is the beat that
        // tells the player something big is coming, and it is the only camera move the HUD asks for
        // before an action resolves. Camera work goes through FocusOn so it composes inside the
        // LateUpdate stomp instead of fighting it.
        public void PlayChargeUp(Entity caster)
        {
            if (caster == null) return;
            FocusOn(caster.transform.position + Vector3.up * 1.2f, 1.6f, 1.2f);
            shakeAmount = Mathf.Max(shakeAmount, shakeOnCrit * 0.6f);   // a low rumble, not an impact
            if (chargeUpFx == null) return;
            var fx = Instantiate(chargeUpFx, caster.transform.position, Quaternion.identity);
            Destroy(fx, 2.5f);
        }

        public void FocusOn(Vector3 worldPoint, float strength = 1f, float hold = 1.5f)
        {
            Vector3 v = worldPoint - camBasePos;
            v.y *= 0.25f;                                    // lean, don't dive
            focusDesired = Vector3.ClampMagnitude(v * 0.14f, 2.4f) * strength;
            focusFovDesired = 5.5f * strength;
            focusHold = hold;
        }

        private void OnDisable()
        {
            onDamageDealt?.Unsubscribe(OnDamage);
            onStaggerBroken?.Unsubscribe(OnBreak);
            onBossTelegraph?.Unsubscribe(OnTelegraph);
            onEntityDied?.Unsubscribe(OnDied);
            onBattleWon?.Unsubscribe(OnWon);
            onTurnStarted?.Unsubscribe(OnTurnFocus);
        }

        // Play the death animation on a fallen combatant's rigged model (no-op for billboards),
        // and pop the gold payout over a slain minion. Minion corpses SINK into the meadow after
        // the death anim so the battlefield doesn't fill with frozen bodies.
        private void OnDied(Characters.Entity e)
        {
            if (e == null) return;
            e.GetComponentInChildren<Characters.AnimationDriver>()?.PlayDie();
            if (e.goldDrop > 0)
                SpawnFloating(e.transform.position + Vector3.up * 2.2f, $"+{e.goldDrop}g", new Color(0.98f, 0.80f, 0.32f), 34);
            if (e.team == Characters.Team.Enemies && !e.isBoss)
                StartCoroutine(SinkCorpse(e));
        }

        private IEnumerator SinkCorpse(Characters.Entity e)
        {
            yield return new WaitForSeconds(2.6f);                        // let the death anim land
            if (e == null) yield break;
            var body = e.transform.Find("Body");
            var shadow = e.transform.Find("Shadow");
            if (shadow != null) shadow.gameObject.SetActive(false);
            if (body == null) yield break;
            Vector3 from = body.position;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                if (body == null) yield break;
                body.position = from + Vector3.down * (2.2f * (t / 1.6f));
                yield return null;
            }
            if (e != null) e.gameObject.SetActive(false);
        }

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

        // Wait until the attacker's CURRENT action clip reaches its contact frame, rather than for a
        // fixed number of seconds. The clips in play run 0.67s–3.60s, so one constant cannot be
        // right for more than one of them; asking the clip that is actually playing also handles the
        // attack-variant blend trees, where the motion is chosen at random each swing.
        //
        // `fallback` stays authoritative in two places: while the animator has not yet entered the
        // action state (a trigger set this frame needs a frame or two), and as a ceiling, so an
        // interrupted or missing state can never stall the battle waiting for a frame that never comes.
        private IEnumerator WaitForContact(Entity source, float fallback)
        {
            var drv = source != null ? source.GetComponentInChildren<Characters.AnimationDriver>() : null;
            if (drv == null) { yield return new WaitForSeconds(fallback); yield break; }

            float giveUp = Time.time + 0.30f;
            float toContact = drv.TimeToContact();
            while (toContact < 0f && Time.time < giveUp)
            {
                yield return null;
                toContact = drv.TimeToContact();
            }

            if (toContact < 0f) { yield return new WaitForSeconds(fallback); yield break; }
            yield return new WaitForSeconds(Mathf.Min(toContact, fallback + 0.8f));
        }

        private IEnumerator AttackBeat(DamageResult r)
        {
            // Schedule on a shared timeline so several hits resolved in one frame play one-by-one.
            float start = Mathf.Max(Time.time, nextBeat);
            nextBeat = start + 0.30f;
            BattleController.PresentationBusyUntil = Mathf.Max(BattleController.PresentationBusyUntil, start + 1.1f);
            float lead = start - Time.time;
            if (lead > 0f) yield return new WaitForSeconds(lead);

            // The action punch-in, fired HERE rather than on turn-start, so a hero gets their
            // close-up as the blow begins instead of while the menu is still open.
            if (r.source != null) FocusOnActor(r.source);

            TargetAnchors(r.target, r.target.isBoss ? 1.6f : 1.0f, r.target.isBoss ? 3.6f : 2.2f, out Vector3 bodyCenter, out Vector3 head);
            Vector3 dir = r.source != null ? (r.target.transform.position - r.source.transform.position) : Vector3.forward;

            // APPROACH + WIND-UP. Three shapes, all landing the impact ON the contact beat:
            //   melee      — run to the foe (eased, with the Run anim), swing ON ARRIVAL, connect mid-swing;
            //   projectile — cast/draw, LAUNCH the authored prefab from the caster's chest, arc to the
            //                target nose-first, impact when it ARRIVES (was: spawned static at the target);
            //   other      — cast in place, impact on the swing's natural contact frame.
            bool melee = false;
            bool projectileDelivered = false;
            bool meleeSwing = false;      // a dash-and-swing, whose contact frame the clip can tell us
            float dashTime = 0f;          // the run-in; the swing only STARTS once it lands
            float impactDelay = 0.42f;
            if (r.hit && r.source != null)
            {
                var srcMotion = r.source.GetComponent<Characters.CombatantMotion>();
                var drv = r.source.GetComponentInChildren<Characters.AnimationDriver>();
                bool area = r.ability != null && r.ability.targetRule == TargetRule.AllEnemies;
                melee = r.ability != null && !r.ability.isMagic && !area
                        && !r.ability.HasTag("Ranged")
                        && !r.ability.vfxIsProjectile          // thrown weapons (Lucky Seven…) fly, they don't dash
                        && r.source.team != r.target.team;

                if (melee && srcMotion != null)
                {
                    float gap = new Vector2(dir.x, dir.z).magnitude;
                    // Stop at the edge of the target's body, not a fixed 2.4u — a whelp and a
                    // 5u dragon need very different stopping distances or you clip inside them.
                    float reach = TargetRadius(r.target) + 0.9f;
                    float travel = Mathf.Max(0.6f, gap - reach);
                    // Swing starts when the runner ARRIVES; mixamo one-handers connect ~0.35s in.
                    srcMotion.DashStrike(dir, travel, () => drv?.PlayAttack());
                    // Impact rides the ACTUAL travel time (distance/speed), matching the new dash.
                    // The 0.35s tail is only the FALLBACK now: once the dash lands, the wait below
                    // asks the swing clip itself where it connects.
                    dashTime = Mathf.Clamp(travel / srcMotion.runSpeed, 0.28f, 0.95f);
                    impactDelay = dashTime + 0.35f;
                    meleeSwing = drv != null;
                }
                else
                {
                    srcMotion?.FaceTarget(dir);   // aim before you shoot
                    srcMotion?.Lunge(dir);
                    if (drv != null)
                    {
                        if (area) drv.PlayAreaAttack();
                        else if (r.ability != null && r.ability.isMagic) drv.PlayCast();
                        else drv.PlayAttack();
                    }

                    if (r.ability != null && r.ability.vfxPrefab != null && r.ability.vfxIsProjectile && !area)
                    {
                        // Release the shot on the cast/draw's OWN release frame. The Archer's draw is
                        // 1.03s and her overdraw 3.60s — a flat 0.45s loosed the arrow before she had
                        // drawn the string on one and long before the other.
                        const float releaseDelay = 0.45f;
                        yield return WaitForContact(r.source, releaseDelay);
                        TargetAnchors(r.source, 1.2f, 2.0f, out Vector3 muzzle, out _);
                        // Spawn already positioned + aimed: these prefabs burst-fire a world-space
                        // particle on their very first frame, so origin-spawn = shot into the bushes.
                        Vector3 pdir = bodyCenter - muzzle;
                        var aim = pdir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(pdir.normalized) : Quaternion.identity;
                        var fx = Instantiate(r.ability.vfxPrefab, muzzle, aim);
                        float flight = ProjectileFlight.Launch(fx, muzzle, bodyCenter);
                        impactDelay = flight;                        // impact = moment of arrival
                        projectileDelivered = true;
                    }
                }
            }

            BattleController.PresentationBusyUntil = Mathf.Max(BattleController.PresentationBusyUntil, Time.time + impactDelay + 0.65f);
            // A projectile's impact is its ARRIVAL, which ProjectileFlight already timed exactly, so
            // only the swing consults the clip. Everything else keeps the estimate.
            if (meleeSwing)
            {
                // Two beats, not one: run in, THEN connect. The swing does not start until the dash
                // callback fires, so asking the clip before arrival would just read the Run state.
                yield return new WaitForSeconds(dashTime);
                yield return WaitForContact(r.source, 0.35f);
            }
            else yield return new WaitForSeconds(impactDelay);

            // IMPACT beat: VFX blooms on the target body, damage number pops, hit-stop + shake fire.
            string text; Color color; float size;
            if (!r.hit) { text = "MISS"; color = CResist; size = 26; }
            else if (r.glanced) { text = r.amount + "  graze"; color = CResist; size = 28; }
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
                // Layer the ability's authored effect on top (impact/explosion prefabs read strongly
                // here) — unless a projectile already delivered it to this exact spot.
                if (!projectileDelivered && r.ability != null && r.ability.vfxPrefab != null)
                {
                    var fx = Instantiate(r.ability.vfxPrefab, bodyCenter, Quaternion.identity);
                    fx.transform.localScale *= r.target.isBoss ? 1.6f : 1.25f;
                    Destroy(fx, 4f);
                }
            }

            if (r.hit && !r.isHeal && !r.absorbed && r.source != null)
            {
                // A graze is a weak connect: small flinch, no hit-stop, barely a shake.
                float recoilScale = r.glanced ? 0.4f : r.crit ? 1.8f : (r.reaction == ElementReaction.Weak ? 1.4f : 1f);
                r.target.GetComponent<Characters.CombatantMotion>()?.Recoil(dir, recoilScale);
                r.target.GetComponentInChildren<Characters.AnimationDriver>()?.PlayHit();
                if (!r.glanced) HitStop(r.crit ? hitStopCrit : hitStopNormal);
                shakeAmount = Mathf.Max(shakeAmount, r.glanced ? shakeOnHit * 0.35f : r.crit ? shakeOnCrit : (r.reaction == ElementReaction.Weak ? shakeOnCrit * 0.8f : shakeOnHit));
                if (r.crit) { flashAmount = Mathf.Max(flashAmount, flashOnCrit); fovPunch = Mathf.Max(fovPunch, 9f); }   // crit snaps the camera in
                else if (r.reaction == ElementReaction.Weak) fovPunch = Mathf.Max(fovPunch, 4.5f);

                // THE IMPACT FRAME: the victim's body lights up on the contact frame. White-hot for a
                // clean hit, warmer for a crit, ice-blue for the SHATTER detonation, faint for a graze.
                bool shatter = r.synergyNote != null && r.synergyNote.Contains("SHATTER");
                Color flashTint = shatter ? new Color(1.6f, 2.2f, 2.6f)
                                : r.crit ? new Color(2.6f, 1.9f, 1.2f)
                                : new Color(2.1f, 2.1f, 2.1f);
                FlashBody(r.target, flashTint, r.glanced ? 0.05f : r.crit || shatter ? 0.13f : 0.09f);

                // DIRECTIONAL KICK: the camera jolts a touch ALONG the blow, not just randomly.
                // Random shake is energy; a directional kick is causality — together they read AAA.
                if (!r.glanced && cam != null)
                {
                    Vector3 local = cam.transform.InverseTransformDirection(dir.normalized);
                    local.z *= 0.3f;                       // mostly a screen-space shove, not a zoom
                    kickOffset += local * (r.crit || shatter ? 0.30f : 0.14f);
                }

                // SLASH ARC: melee connects draw a blade arc through the target — the Hovl slash
                // prefabs are self-playing; aim them along the blow with a random roll so no two
                // swings stamp the same arc.
                if (melee && !r.glanced)
                {
                    var slashPf = shatter && slashShatter != null ? slashShatter
                                : r.crit && slashCrit != null ? slashCrit
                                : slashNormal;
                    if (slashPf != null)
                    {
                        var rot = Quaternion.LookRotation(dir.normalized)
                                * Quaternion.Euler(0f, 0f, Random.Range(-55f, 55f));
                        var arc = Instantiate(slashPf, bodyCenter, rot);
                        arc.transform.localScale *= (r.target.isBoss ? 1.9f : 1.15f) * (r.crit ? 1.2f : 1f);
                        // The Hovl slash prefabs ship LOOPING — one arc, not a strobe. Configure
                        // before the first sim frame (same rule as the Erb projectiles).
                        foreach (var p in arc.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            var main = p.main;
                            main.loop = false;
                        }
                        Destroy(arc, 2.2f);
                    }
                }
            }
        }

        // Horizontal half-extent of a combatant's body — how close an attacker may run before they'd
        // be standing inside them. Measured from the renderers so it scales with the boss/minion.
        private static float TargetRadius(Entity e)
        {
            var body = e != null ? e.transform.Find("Body") : null;
            if (body == null) return 1.2f;
            var rends = body.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return 1.2f;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 0.5f, 3.2f);
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

        // THE IMPACT FRAME. Fighting games sell a hit in the 3 frames around contact: the victim's
        // body lights up, then cools. We tint every renderer toward the flash colour and ease back
        // to the exact authored colour over ~90ms of UNSCALED time — unscaled because the hit-stop
        // parks timeScale at 0 on that very frame, and a flash frozen at full white reads as a bug.
        private void FlashBody(Entity e, Color tint, float duration)
        {
            if (e == null) return;
            var rends = e.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            var usable = new List<Renderer>();
            var cols = new List<Color>();
            foreach (var r in rends)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;   // never tint VFX
                var m = r.sharedMaterial;
                if (m == null || !m.HasProperty(BaseColorId)) continue;
                usable.Add(r);
                cols.Add(m.GetColor(BaseColorId));
            }
            if (usable.Count == 0) return;
            bodyFlashes.Add(new BodyFlash { rends = usable.ToArray(), baseCols = cols.ToArray(), t = 0f, dur = duration, tint = tint });
        }

        private void UpdateBodyFlashes()
        {
            if (bodyFlashes.Count == 0) return;
            if (sharedMpb == null) sharedMpb = new MaterialPropertyBlock();
            for (int i = bodyFlashes.Count - 1; i >= 0; i--)
            {
                var f = bodyFlashes[i];
                f.t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(f.t / f.dur);
                bool done = k >= 1f;
                for (int j = 0; j < f.rends.Length; j++)
                {
                    if (f.rends[j] == null) continue;
                    if (done)
                    {
                        // Return the renderer to its authored state EXACTLY: clear the override.
                        f.rends[j].SetPropertyBlock(null);
                        continue;
                    }
                    // Hot at contact, cooling on a squared ease so the peak reads for ~2 frames.
                    Color c = Color.Lerp(f.tint, f.baseCols[j], k * k);
                    sharedMpb.Clear();
                    sharedMpb.SetColor(BaseColorId, c);
                    f.rends[j].SetPropertyBlock(sharedMpb);
                }
                if (done) bodyFlashes.RemoveAt(i);
            }
        }

        private void OnBreak(Entity boss)
        {
            shakeAmount = Mathf.Max(shakeAmount, shakeOnBreak);
            flashAmount = Mathf.Max(flashAmount, flashOnBreak);
            fovPunch = Mathf.Max(fovPunch, 14f); breakPunch = 1f;   // big FOV snap + a slow dolly toward the action
            FlashBody(boss, new Color(2.6f, 2.1f, 0.9f), 0.35f);    // the armour cracks GOLD — the reward colour
            // The break slow-mo QUEUES behind any in-flight hit-stop (never dropped, never races it).
            StartCoroutine(TimeEffect(breakSlowMoScale, breakSlowMoDuration, true));
        }

        private void OnTelegraph(Ability a)
        {
            // The boss REARS UP AND SCREAMS its wind-up (Roar state = the pack's Scream clip) —
            // paired with the dragon_roar SFX and the HUD banner, the charged turn feels dangerous.
            shakeAmount = Mathf.Max(shakeAmount, shakeOnHit * 0.6f);
            foreach (var e in FindObjectsByType<Characters.Entity>(FindObjectsSortMode.None))
                if (e.isBoss && e.IsAlive) { e.GetComponentInChildren<Characters.AnimationDriver>()?.PlayRoar(); break; }
        }

        // Public floater for other presentation systems (the HUD's PERFECT!/BRACED! callouts).
        public void Announce(Vector3 worldPos, string text, Color color, float size = 36f)
            => SpawnFloating(worldPos, text, color, size);

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
                    // Manually integrate UNSCALED time instead of WaitForSecondsRealtime: the realtime
                    // wait never resumes when the editor is unfocused and frame-stepped (MCP-driven
                    // validation), which left timeScale stuck at 0 and froze the whole battle. The
                    // unscaled clock provably ticks every player-loop frame in both modes.
                    float t = 0f;
                    while (t < duration) { t += Mathf.Min(Time.unscaledDeltaTime, 0.05f); yield return null; }
                }
            }
            finally
            {
                // Hand the clock back to whoever legitimately owns it: a hit-stop that ends while
                // the player is in the pause menu must NOT resume the game behind the overlay.
                Time.timeScale = PauseMenu.IsPaused ? 0f : 1f;
                timeEffectActive = false;
            }
        }

        private float frozenClockTimer;   // watchdog: how long timeScale has sat at ~0 (unscaled)

        private void LateUpdate()
        {
            // WATCHDOG: OUR time effects must never park the game at timeScale 0. If a hit-stop /
            // slow-mo we own has held the clock frozen for over 1.5 unscaled seconds (a leaked or
            // stalled coroutine), force-restore — dropping one hit-stop beats freezing the battle.
            // Deliberate pauses are not ours to cancel: skip while the pause menu holds the clock.
            if (Time.timeScale > 0.001f || PauseMenu.IsPaused || !timeEffectActive) frozenClockTimer = 0f;
            else
            {
                frozenClockTimer += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                if (frozenClockTimer > 1.5f) { Time.timeScale = 1f; timeEffectActive = false; frozenClockTimer = 0f; }
            }

            // Action camera: FOV punch (crit/break) + a Break dolly push, plus decaying shake — all on
            // UNSCALED time so the move animates through the hit-stop freeze and the BREAK slow-mo. The
            // base pos/FOV are restored as the punches ease to zero, so this never deadlocks the framing.
            if (cam != null)
            {
                fovPunch = Mathf.MoveTowards(fovPunch, 0f, 26f * Time.unscaledDeltaTime);
                breakPunch = Mathf.MoveTowards(breakPunch, 0f, 1.45f * Time.unscaledDeltaTime);

                // Actor focus: ease toward the lean while the hold lasts, then ease home. Unscaled,
                // like everything else here, so hit-stop can't freeze the camera mid-lean.
                if (focusHold > 0f) focusHold -= Time.unscaledDeltaTime;
                Vector3 wantOff = focusHold > 0f ? focusDesired : Vector3.zero;
                float wantFov = focusHold > 0f ? focusFovDesired : 0f;
                focusOffset = Vector3.MoveTowards(focusOffset, wantOff, 6.5f * Time.unscaledDeltaTime);
                focusFovBias = Mathf.MoveTowards(focusFovBias, wantFov, 24f * Time.unscaledDeltaTime);

                cam.fieldOfView = camBaseFov - fovPunch - focusFovBias;

                Vector3 shakeOff = Vector3.zero;
                if (shakeAmount > 0.0001f)
                {
                    Vector2 o = Random.insideUnitCircle * shakeAmount;
                    shakeOff = new Vector3(o.x, o.y, 0f);
                    shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, shakeDecay * Time.unscaledDeltaTime);
                }
                // The directional kick decays fast (spring-back), unscaled like every camera motion here.
                kickOffset = Vector3.MoveTowards(kickOffset, Vector3.zero, 2.6f * Time.unscaledDeltaTime);
                cam.transform.localPosition = camBasePos + BreakDolly * breakPunch + focusOffset + shakeOff + kickOffset;
            }

            if (flashImage != null)
            {
                flashAmount = Mathf.MoveTowards(flashAmount, 0f, flashDecay * Time.unscaledDeltaTime);
                flashImage.color = new Color(1f, 1f, 1f, flashAmount);
            }

            UpdateBodyFlashes();   // unscaled fade — a flash must cool THROUGH the hit-stop freeze
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
            // Scale with the screen like every other canvas — without this, damage numbers rendered
            // at raw font pixels (tiny on a 4K game view, huge at 720p).
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
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
