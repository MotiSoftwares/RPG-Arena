using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RPGArena.Core;

namespace RPGArena.UI
{
    // The Main Menu + Character Select front-end, built in code (built-in font; TMP/art come in
    // the M4/M5 polish passes). Play -> pick exactly 3 of 4 classes -> writes the party into
    // RunState (on the persistent bootstrap) -> loads the battle. Also exposes How to Play,
    // Credits, and Quit. Replaces the M0 placeholder menu.
    public class MainMenuUI : MonoBehaviour
    {
        // The four classes (name + one-line role) shown on the select cards.
        private static readonly (string name, string blurb)[] Classes =
        {
            ("Warrior", "STR — tanky anchor; taunts, Berserk/Guardian stance, big finishers"),
            ("Mage", "INT — elemental nuker + healer; attune Fire/Ice/Lightning/Holy"),
            ("Thief", "LUK — combo enabler; Oil/Wet/Mark setup, crit, Dark Sight"),
            ("Archer", "DEX — ranged precision; never misses, Puppet decoy, Arrow Rain"),
        };

        private Font font;
        private GameObject mainPanel, selectPanel, infoPanel, settingsPanel;
        private Text infoText, selectHint;
        private Button confirmBtn;
        private readonly List<string> picked = new();
        private readonly Dictionary<string, Image> cardImages = new();

        // Audio service (persistent bootstrap); null-safe so the menu also works when entered directly.
        private static IAudioService Audio => GameBootstrap.Instance != null ? GameBootstrap.Instance.Audio : null;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUI();
            ShowMain();
            Audio?.PlayMusic("menu");        // calm menu theme, routed through the mixer
        }

        // --- flow ---------------------------------------------------------------------
        private void ShowMain() { mainPanel.SetActive(true); selectPanel.SetActive(false); infoPanel.SetActive(false); settingsPanel.SetActive(false); }
        private void ShowSelect() { mainPanel.SetActive(false); selectPanel.SetActive(true); infoPanel.SetActive(false); settingsPanel.SetActive(false); picked.Clear(); RefreshCards(); }
        private void ShowSettings() { mainPanel.SetActive(false); selectPanel.SetActive(false); infoPanel.SetActive(false); settingsPanel.SetActive(true); }

        private void ShowInfo(string title, string body)
        {
            mainPanel.SetActive(false); selectPanel.SetActive(false); infoPanel.SetActive(false); settingsPanel.SetActive(false); infoPanel.SetActive(true);
            infoText.text = title + "\n\n" + body;
        }

        private void TogglePick(string className)
        {
            if (picked.Contains(className)) picked.Remove(className);
            else if (picked.Count < 3) picked.Add(className);
            RefreshCards();
        }

        private void RefreshCards()
        {
            foreach (var kv in cardImages)
                kv.Value.color = picked.Contains(kv.Key) ? new Color(0.2f, 0.55f, 0.3f, 0.95f) : new Color(0.16f, 0.18f, 0.28f, 0.95f);
            if (selectHint) selectHint.text = $"Pick exactly 3 heroes  ({picked.Count}/3 chosen)";
            if (confirmBtn) confirmBtn.interactable = picked.Count == 3;
        }

        private void Confirm()
        {
            if (picked.Count != 3) return;
            var boot = GameBootstrap.Instance;
            if (boot != null && boot.Run != null)
            {
                boot.Run.partyClassNames.Clear();
                boot.Run.partyClassNames.AddRange(picked);
                boot.Scenes.LoadScene("BattleArena");
            }
            else
            {
                // No bootstrap (entered Play directly here) — load the scene; battle uses its default party.
                UnityEngine.SceneManagement.SceneManager.LoadScene("BattleArena");
            }
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // --- construction -------------------------------------------------------------
        private void BuildUI()
        {
            var canvasGo = new GameObject("MainMenu_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            // Main panel.
            mainPanel = Panel(root, "Main");
            var mp = (RectTransform)mainPanel.transform;
            Label(mp, "ARENA OF THE ALGORITHMS", new Vector2(0.5f, 0.78f), 52, TextAnchor.MiddleCenter, 1200);
            Label(mp, "A turn-based boss-rush RPG", new Vector2(0.5f, 0.70f), 24, TextAnchor.MiddleCenter, 1000);
            MenuButton(mp, "Play", 0.56f, () => ShowSelect());
            MenuButton(mp, "How to Play", 0.47f, () => ShowInfo("HOW TO PLAY",
                "Pick 3 of 4 heroes. Read the boss: exploit its WEAKNESS (don't use what it absorbs!),\nset up cross-class combos (Oil+Fire, Wet+Ice/Lightning, Mark), and build the STAGGER bar.\nBreak the boss during its telegraphed charge to cancel the attack, then unload in the window.\nStrong attacks can miss — the action menu shows each move's hit %. Click an ability to act."));
            MenuButton(mp, "Settings", 0.38f, ShowSettings);
            MenuButton(mp, "Credits", 0.29f, () => ShowInfo("CREDITS",
                "Arena of the Algorithms — a student software-engineering project.\nBuilt with Unity 6.3 (URP). SFX: ElevenLabs. Art: Pollinations. 3D: Tripo. Narrative: Ink.\nMade with AI assistance (Claude Code + Unity MCP)."));
            MenuButton(mp, "Quit", 0.20f, Quit);

            // Select panel.
            selectPanel = Panel(root, "Select");
            var sp = (RectTransform)selectPanel.transform;
            Label(sp, "CHOOSE YOUR PARTY", new Vector2(0.5f, 0.88f), 40, TextAnchor.MiddleCenter, 1200);
            selectHint = Label(sp, "Pick exactly 3 heroes  (0/3 chosen)", new Vector2(0.5f, 0.80f), 22, TextAnchor.MiddleCenter, 1000);
            for (int i = 0; i < Classes.Length; i++)
            {
                var c = Classes[i];
                float y = 0.66f - i * 0.13f;
                var card = Card(sp, c.name, c.blurb, y, () => TogglePick(c.name));
                cardImages[c.name] = card;
            }
            confirmBtn = MenuButton(sp, "Confirm & Fight", 0.10f, Confirm); confirmBtn.interactable = false;
            MenuButton(sp, "Back", 0.03f, ShowMain);

            // Info panel.
            infoPanel = Panel(root, "Info");
            var ip = (RectTransform)infoPanel.transform;
            infoText = Label(ip, "", new Vector2(0.5f, 0.6f), 22, TextAnchor.UpperCenter, 1300);
            MenuButton(ip, "Back", 0.12f, ShowMain);

            // Settings panel: three volume sliders bound to the AudioMixer's exposed parameters.
            settingsPanel = Panel(root, "Settings");
            var stp = (RectTransform)settingsPanel.transform;
            Label(stp, "SETTINGS", new Vector2(0.5f, 0.85f), 40, TextAnchor.MiddleCenter, 1000);
            VolumeSlider(stp, "Master Volume", AudioBus.Master, 0.66f);
            VolumeSlider(stp, "Music Volume", AudioBus.Music, 0.54f);
            VolumeSlider(stp, "SFX Volume", AudioBus.Sfx, 0.42f);
            MenuButton(stp, "Back", 0.18f, ShowMain);
        }

        // A labelled horizontal slider wired to a mixer bus; persists via the AudioManager.
        private void VolumeSlider(RectTransform parent, string label, string bus, float anchorY)
        {
            Label(parent, label, new Vector2(0.5f, anchorY + 0.05f), 22, TextAnchor.MiddleCenter, 600);
            var go = new GameObject("Slider_" + bus); go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>(); bg.color = new Color(0.1f, 0.12f, 0.2f, 1f);
            var rt = bg.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(520, 24);

            var slider = go.AddComponent<Slider>();
            var fillArea = new GameObject("Fill"); fillArea.transform.SetParent(go.transform, false);
            var fillImg = fillArea.AddComponent<Image>(); fillImg.color = new Color(0.3f, 0.6f, 0.9f, 1f);
            var frt = fillImg.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(1, 1); frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            slider.fillRect = frt; slider.targetGraphic = fillImg; slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.value = Audio != null ? Audio.GetVolume(bus) : 0.85f;
            slider.onValueChanged.AddListener(v => Audio?.SetVolume(bus, v));
        }

        // --- tiny uGUI helpers --------------------------------------------------------
        private GameObject Panel(RectTransform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0.05f, 0.05f, 0.09f, 1f);
            var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go;
        }

        private Text Label(RectTransform parent, string text, Vector2 anchor, int size, TextAnchor align, float width)
        {
            var go = new GameObject("Label"); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.font = font; t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(width, size * 4 + 20);
            return t;
        }

        private Button MenuButton(RectTransform parent, string label, float anchorY, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0.16f, 0.3f, 0.5f, 0.95f);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(360, 56);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(() => { Audio?.PlaySfx("ui_click"); onClick(); });
            var t = Label((RectTransform)go.transform, label, new Vector2(0.5f, 0.5f), 24, TextAnchor.MiddleCenter, 360);
            return btn;
        }

        private Image Card(RectTransform parent, string name, string blurb, float anchorY, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Card_" + name); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0.16f, 0.18f, 0.28f, 0.95f);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(1100, 90);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
            var nameT = Label(rt, name, new Vector2(0.5f, 0.72f), 26, TextAnchor.MiddleCenter, 1080);
            nameT.color = new Color(1f, 0.9f, 0.5f);
            Label(rt, blurb, new Vector2(0.5f, 0.32f), 18, TextAnchor.MiddleCenter, 1060);
            return img;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }
    }
}
