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

            // Close-up: low angle just left of the boss, looking up at its head.
            Vector3 bossPos = boss.transform.position;
            Vector3 closePos = bossPos + new Vector3(-4.6f, 1.6f, -3.4f);
            juice.SetCameraBase(closePos, 34f);
            cam.transform.localPosition = closePos;
            Vector3 lookTarget = bossPos + Vector3.up * 2.6f;
            cam.transform.rotation = Quaternion.LookRotation((lookTarget - closePos).normalized, Vector3.up);
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
            boss.GetComponentInChildren<Characters.AnimationDriver>()?.PlayAreaAttack();
            for (float t = 0f; t < holdOnBoss; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 0.35f);
                title.alpha = k; sub.alpha = Mathf.Clamp01((t - 0.25f) / 0.4f);
                title.transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.SmoothStep(0f, 1f, k));
                // slow push-in for menace
                juice.SetCameraBase(Vector3.Lerp(closePos, closePos + new Vector3(0.35f, 0.1f, 0.5f), t / holdOnBoss), 34f);
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
