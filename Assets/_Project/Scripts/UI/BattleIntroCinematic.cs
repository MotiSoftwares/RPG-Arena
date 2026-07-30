using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RPGArena.Core;
using RPGArena.Combat;

namespace RPGArena.UI
{
    // The pre-fight cinematic: fade up from black on a low, close shot of the boss, hold while it
    // ROARS its title card, then pull the camera back into the battle framing and hand control to
    // the HUD. Implements IBattleIntro so the battle loop simply waits on IsIntroDone — combat
    // logic never knows a camera moved. All camera motion is driven through
    // JuiceController.SetCameraBase so the two systems never fight over the transform.
    public class BattleIntroCinematic : MonoBehaviour, IBattleIntro
    {
        [SerializeField] private TMP_FontAsset uiFont;    // SlimUI Poppins-Bold SDF (wired in scene)
        [Header("Timing")]
        public float fadeIn = 0.5f;
        public float holdOnBoss = 2.1f;
        public float pullBack = 1.4f;

        public bool IsIntroDone { get; private set; }
        public bool StartTelegraph => false;
        public bool RevealWeak => false;
        public void PlayOutro(bool won, bool brokeBoss, int heroesLost) { }

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            var juice = FindFirstObjectByType<JuiceController>();
            var controller = FindFirstObjectByType<BattleController>();
            var cam = Camera.main;
            if (juice == null || cam == null || controller == null) { IsIntroDone = true; yield break; }

            // Wait until the combatants exist (BattleController.BuildContext runs from its own Start).
            float guard = Time.realtimeSinceStartup + 3f;
            while ((controller.Context == null || controller.Context.boss == null) && Time.realtimeSinceStartup < guard)
                yield return null;
            var boss = controller.Context != null ? controller.Context.boss : null;
            if (boss == null) { IsIntroDone = true; yield break; }

            Vector3 homePos = juice.CameraBasePos;
            float homeFov = juice.CameraBaseFov;

            // Close-up: low angle just left of the boss, looking up at its HEAD.
            //
            // Framing is derived from the boss's real rendered bounds, never from fixed offsets. The
            // old constants (look at bossPos + 2.6u) were authored against a 5u dragon; once it grew
            // the shot pointed at its throat, and it would have been just as wrong for the 3.4u
            // Black Mage in the other direction. Scaling off the silhouette keeps every boss framed
            // on the face no matter how big it is.
            Vector3 bossPos = boss.transform.position;
            Bounds b = BossBounds(boss);
            float h = Mathf.Max(1.5f, b.size.y);
            Vector3 head = new Vector3(b.center.x, b.min.y + h * 0.86f, b.center.z);
            float dist = Mathf.Clamp(h * 1.30f, 4.5f, 16f);
            Vector3 camOffset = new Vector3(-dist * 0.62f, h * 0.06f, -dist * 0.80f);
            // COMPOSITION. The camera sits at aim + camOffset and the LookRotation below is derived
            // from that SAME offset, so whatever we aim at lands on the exact centre pixel: these
            // offset constants choose only the ANGLE and DISTANCE of the shot and can never move the
            // boss within the frame. Moving the AIM translates the whole rig, which slides the boss
            // the OPPOSITE way on screen — so to seat him lower and further left we aim slightly
            // ABOVE and to SCREEN-RIGHT of his face. screenRight is read off the live optical axis
            // (the shot is yawed ~38 deg, so screen-left is NOT -X); crossing with world up drops
            // the offset's y term, which makes screenRight independent of both h and the dist clamp.
            // Both nudges are fractions of h, so every boss composes identically: +0.060h up reads
            // as ~7.5% of frame height DOWN, +0.086h right as ~6.0% of frame width LEFT (dragon
            // 7.46/6.02, Evil Warrior 7.46/6.02, Black Mage 7.31/5.90 — his dist is the only one
            // that clamps). Tuning: 0.010 of the up coefficient = 1.24% of frame height, 0.010 of
            // the right coefficient = 0.70% of frame width, both independent of h.
            Vector3 screenRight = Vector3.Cross(Vector3.up, -camOffset).normalized;
            Vector3 aim = head + Vector3.up * (h * 0.060f) + screenRight * (h * 0.086f);
            Vector3 closePos = aim + camOffset;
            juice.SetCameraBase(closePos, 34f);
            cam.transform.localPosition = closePos;
            cam.transform.rotation = Quaternion.LookRotation((aim - closePos).normalized, Vector3.up);
            Quaternion closeRot = cam.transform.rotation;

            var (overlay, title, sub) = BuildOverlay(boss.displayName);

            // Fade up from black.
            for (float t = 0f; t < fadeIn; t += Time.deltaTime)
            {
                overlay.color = new Color(0f, 0f, 0f, 1f - t / fadeIn);
                yield return null;
            }
            overlay.color = Color.clear;

            // The boss announces itself: roar SFX + its area-attack flourish, title card punches in.
            GameBootstrap.Instance?.Audio?.PlaySfx("dragon_roar");
            boss.GetComponentInChildren<Characters.AnimationDriver>()?.PlayRoar();
            for (float t = 0f; t < holdOnBoss; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 0.35f);
                title.alpha = k; sub.alpha = Mathf.Clamp01((t - 0.25f) / 0.4f);
                title.transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.SmoothStep(0f, 1f, k));
                // slow push-in for menace, scaled to the boss so it reads the same at any size
                // -camOffset IS the optical axis by construction (it equals (aim - closePos)), so the
                // menace push-in stays dead along the lens. Using (head - closePos) here would creep
                // diagonally and drag the face back toward centre, undoing the reframe over the hold.
                Vector3 creep = -camOffset.normalized * (h * 0.10f);
                juice.SetCameraBase(Vector3.Lerp(closePos, closePos + creep, t / holdOnBoss), 34f);
                yield return null;
            }

            // Pull back into the battle frame; title fades away; rotation eases home.
            Quaternion homeRot = Quaternion.Euler(10f, 0f, 0f);
            for (float t = 0f; t < pullBack; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / pullBack);
                juice.SetCameraBase(Vector3.Lerp(juice.CameraBasePos, homePos, k), Mathf.Lerp(34f, homeFov, k));
                cam.transform.rotation = Quaternion.Slerp(closeRot, homeRot, k);
                float fade = 1f - Mathf.Clamp01(t / (pullBack * 0.5f));
                title.alpha = fade; sub.alpha = fade;
                yield return null;
            }
            juice.SetCameraBase(homePos, homeFov);
            cam.transform.rotation = homeRot;
            Destroy(overlay.transform.parent.gameObject);
            IsIntroDone = true;
        }

        // The boss's true on-screen silhouette: every renderer under it, merged. Falls back to a
        // sane box if the model has not attached yet, so the intro can never divide by nothing.
        private static Bounds BossBounds(Characters.Entity boss)
        {
            var rends = boss.GetComponentsInChildren<Renderer>(false);
            bool any = false;
            Bounds b = new Bounds(boss.transform.position + Vector3.up, Vector3.one * 2f);
            foreach (var r in rends)
            {
                if (r == null || r is ParticleSystemRenderer) continue;   // VFX would balloon the box
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // Full-screen black fade + centered title card, on its own topmost canvas.
        private (Image, TMP_Text, TMP_Text) BuildOverlay(string bossName)
        {
            var font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
            var go = new GameObject("IntroCanvas");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var fadeGo = new GameObject("Fade");
            fadeGo.transform.SetParent(go.transform, false);
            var fade = fadeGo.AddComponent<Image>();
            fade.color = Color.black; fade.raycastTarget = false;
            var frt = fade.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(go.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.font = font; title.fontSize = 84; title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center; title.text = bossName.ToUpper();
            title.color = new Color(1f, 0.62f, 0.45f); title.alpha = 0f; title.raycastTarget = false;
            title.enableWordWrapping = false; title.overflowMode = TextOverflowModes.Overflow;
            var trt = title.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.72f); trt.sizeDelta = new Vector2(1600, 110);

            var subGo = new GameObject("Sub");
            subGo.transform.SetParent(go.transform, false);
            var sub = subGo.AddComponent<TextMeshProUGUI>();
            sub.font = font; sub.fontSize = 26;
            sub.alignment = TextAlignmentOptions.Center; sub.text = "CHAMPION OF THE ARENA";
            sub.color = new Color(0.85f, 0.88f, 0.95f); sub.alpha = 0f; sub.raycastTarget = false;
            sub.characterSpacing = 14;
            var srt = sub.rectTransform; srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.64f); srt.sizeDelta = new Vector2(1600, 44);

            return (fade, title, sub);
        }
    }
}
