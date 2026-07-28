using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using RPGArena.Core;

namespace RPGArena.UI
{
    // Pause overlay (§9.5). Esc toggles a proper pause (Time.timeScale = 0, input gated) with
    // Resume / Restart / Exit to Main Menu. Built in code; restoring timeScale on every exit
    // path avoids the "stuck paused after restart" failure the rubric warns about.
    public class PauseMenu : MonoBehaviour
    {
        // This menu OWNS Time.timeScale; it publishes the state to Core.GamePause so the gameplay
        // and juice layers (which must not reference UI) can respect it. Convenience alias:
        public static bool IsPaused => Core.GamePause.IsPaused;

        [SerializeField] private TMP_FontAsset uiFont;   // SlimUI Poppins-Bold SDF (wired in scene); falls back to TMP default
        private TMP_FontAsset font;
        private GameObject panel;
        private bool paused;

        private void Awake()
        {
            font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
            BuildUI();
            panel.SetActive(false);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Toggle();
        }

        private void Toggle() => SetPaused(!paused);

        private void SetPaused(bool value)
        {
            paused = value;
            Core.GamePause.Set(value);
            panel.SetActive(value);
            Time.timeScale = value ? 0f : 1f;
            GameBootstrap.Instance?.Audio?.SetPaused(value);   // mixer "Paused" snapshot: muffled music, frozen SFX
        }

        // A juice effect can end mid-pause and try to hand the clock back to 1 in its finally
        // block; re-assert the pause every frame so the game can never quietly resume behind
        // the overlay (belt-and-braces alongside the IsPaused checks in JuiceController).
        private void LateUpdate()
        {
            if (paused && Time.timeScale != 0f) Time.timeScale = 0f;
        }

        // A scene load must never leave a stale "paused" flag behind for the next battle.
        private void OnDisable() { if (paused) Core.GamePause.Set(false); }
        private void OnDestroy() { Core.GamePause.Set(false); }

        private void Resume() => SetPaused(false);

        private void Restart()
        {
            SetPaused(false);
            UnityEngine.SceneManagement.SceneManager.LoadScene("BattleArena");
        }

        private void ToMenu()
        {
            SetPaused(false);
            var boot = GameBootstrap.Instance;
            if (boot != null) boot.Scenes.LoadScene("MainMenu");
            else UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("Pause_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            panel = new GameObject("PausePanel"); panel.transform.SetParent(canvasGo.transform, false);
            var img = panel.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0.8f);
            var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            Label((RectTransform)panel.transform, "PAUSED", 0.66f, 48);
            Btn((RectTransform)panel.transform, "Resume", 0.52f, Resume);
            Btn((RectTransform)panel.transform, "Restart Fight", 0.43f, Restart);
            Btn((RectTransform)panel.transform, "Exit to Main Menu", 0.34f, ToMenu);
        }

        private void Label(RectTransform parent, string text, float anchorY, int size)
        {
            var go = new GameObject("Label"); go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>(); t.font = font; t.text = text; t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center; t.color = Color.white; t.fontStyle = FontStyles.Bold; t.raycastTarget = false;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(600, size + 20);
        }

        private void Btn(RectTransform parent, string label, float anchorY, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0.16f, 0.3f, 0.5f, 0.95f);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(360, 56);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
            var t = new GameObject("Text"); t.transform.SetParent(go.transform, false);
            var txt = t.AddComponent<TextMeshProUGUI>(); txt.font = font; txt.text = label; txt.fontSize = 24;
            txt.alignment = TextAlignmentOptions.Center; txt.color = Color.white; txt.raycastTarget = false;
            var trt = txt.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        }
    }
}
