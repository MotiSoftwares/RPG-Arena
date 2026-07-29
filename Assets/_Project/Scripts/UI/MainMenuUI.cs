using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
            ("Warrior", "TANK — soaks hits and TAUNTS the boss off your casters; breaks it with big physical finishers."),
            ("Mage", "MAGE — the ONLY hero who can exploit a boss's elemental WEAKNESS (the core mechanic) — and your healer."),
            ("Thief", "SETUP — Wet / Mark to enable cross-class combos (Wet+Ice = Freeze); high crit; vanish with Dark Sight."),
            ("Archer", "RANGED — never misses; Puppet decoy to peel; Arrow Rain hits the whole field."),
        };

        [Header("Art")]
        public Sprite backgroundSprite;     // title-screen background (assigned by SceneSetup)
        public Sprite[] classPortraits = new Sprite[4];   // Warrior/Mage/Thief/Archer (select cards)

        [SerializeField] private TMP_FontAsset uiFont;     // SlimUI Poppins-Bold SDF (wired in scene); falls back to TMP default
        private TMP_FontAsset font;
        private GameObject mainPanel, selectPanel, infoPanel, settingsPanel;
        private TMP_Text infoText, selectHint, difficultyLabel;
        private Button confirmBtn;
        private readonly List<string> picked = new();
        private readonly Dictionary<string, Image> cardImages = new();
        // HARD by default — the game is designed to kill players who don't engage with its systems.
        private Difficulty pendingDifficulty = Difficulty.Hard;

        // Audio service (persistent bootstrap); null-safe so the menu also works when entered directly.
        private static IAudioService Audio => GameBootstrap.Instance != null ? GameBootstrap.Instance.Audio : null;

        private void Awake()
        {
            // Pressing Play on this scene skips Boot, which is where the services normally come
            // from — without this the menu has no audio and Confirm has no RunState to write the
            // party into, so it silently drops the player's whole selection.
            GameBootstrap.EnsureRuntime();

            font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
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
                // Start a brand-new run: reset boss progress + boons, then set the chosen party.
                boot.Run.Reset();
                boot.Run.partyClassNames.AddRange(picked);
                boot.Run.difficulty = pendingDifficulty;
                boot.Scenes.LoadScene("BattleArena");
            }
            else
            {
                // No bootstrap (entered Play directly here) — load the scene; battle uses its default party.
                UnityEngine.SceneManagement.SceneManager.LoadScene("BattleArena");
            }
        }

        private void RefreshDifficultyLabel()
        {
            if (difficultyLabel == null) return;
            difficultyLabel.text = pendingDifficulty == Difficulty.Hard
                ? "Difficulty:  HARD  —  combo or die"
                : "Difficulty:  EASY  —  a gentler arena";
            difficultyLabel.color = pendingDifficulty == Difficulty.Hard
                ? new Color(1f, 0.45f, 0.35f)
                : new Color(0.55f, 0.9f, 0.6f);
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

            // Shared cinematic background + dark overlay behind every sub-panel.
            BuildBackground(root);

            // Main panel.
            mainPanel = Panel(root, "Main");
            var mp = (RectTransform)mainPanel.transform;
            var title = Label(mp, "ARENA OF THE ALGORITHMS", new Vector2(0.5f, 0.80f), 74, TextAnchor.MiddleCenter, 1600);
            title.color = new Color(1f, 0.82f, 0.35f); title.fontStyle = FontStyles.Bold;
            AddOutline(title.gameObject, new Color(0.25f, 0.04f, 0f, 0.95f), 3);
            var sub = Label(mp, "A turn-based boss-rush RPG", new Vector2(0.5f, 0.72f), 26, TextAnchor.MiddleCenter, 1000);
            sub.color = new Color(0.85f, 0.85f, 0.9f); AddOutline(sub.gameObject, new Color(0, 0, 0, 0.8f), 2);
            MenuButton(mp, "Play", 0.56f, () => ShowSelect());
            MenuButton(mp, "How to Play", 0.47f, () => ShowInfo("HOW TO PLAY",
                "Pick 3 of 4 heroes. Read the boss: exploit its WEAKNESS (Ice), never use what it ABSORBS (Fire heals it!).\nSpamming basics can't win — set up cross-class combos: Wet (Thief) -> Ice (Mage) = FREEZE, then a physical\nhit = SHATTER. Mark + Freeze = BRITTLE crits. Build the STAGGER bar and BREAK the dragon to VENT its\nSearing Fury (its damage grows every turn you don't). Charge VALOR for an OVERDRIVE surge. Strong attacks\ncan miss — the menu shows each move's hit %. Click an ability to act."));
            MenuButton(mp, "Settings", 0.38f, ShowSettings);
            MenuButton(mp, "Credits", 0.29f, () => ShowInfo("CREDITS",
                "Arena of the Algorithms — a student software-engineering project.\nBuilt with Unity 6.3 (URP). SFX: ElevenLabs. Art: Pollinations. 3D: Tripo. Narrative: Ink.\nMade with AI assistance (Claude Code + Unity MCP)."));
            MenuButton(mp, "Quit", 0.20f, Quit);

            // Select panel.
            selectPanel = Panel(root, "Select");
            var sp = (RectTransform)selectPanel.transform;
            Label(sp, "CHOOSE YOUR PARTY", new Vector2(0.5f, 0.88f), 40, TextAnchor.MiddleCenter, 1200);
            selectHint = Label(sp, "Pick exactly 3 heroes  (0/3 chosen)", new Vector2(0.5f, 0.80f), 22, TextAnchor.MiddleCenter, 1000);
            Label(sp, "Tip: a Mage covers elemental WEAKNESS — most parties want one.", new Vector2(0.5f, 0.755f), 17, TextAnchor.MiddleCenter, 1100)
                .color = new Color(0.72f, 0.85f, 1f);
            for (int i = 0; i < Classes.Length; i++)
            {
                var c = Classes[i];
                float y = 0.66f - i * 0.135f;
                var portrait = i < classPortraits.Length ? classPortraits[i] : null;
                var card = Card(sp, c.name, c.blurb, portrait, y, () => TogglePick(c.name));
                cardImages[c.name] = card;
            }
            // Difficulty toggle — HARD is the default and says so. One click flips it.
            var diffBtn = MenuButton(sp, "", 0.17f, () =>
            {
                pendingDifficulty = pendingDifficulty == Difficulty.Hard ? Difficulty.Easy : Difficulty.Hard;
                RefreshDifficultyLabel();
            });
            difficultyLabel = diffBtn.GetComponentInChildren<TMP_Text>();
            RefreshDifficultyLabel();

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
            // Transparent container so the shared cinematic background shows through every sub-panel.
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0f); img.raycastTarget = false;
            var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go;
        }

        // Full-screen title art + a dark vignette overlay so text stays readable.
        private void BuildBackground(RectTransform root)
        {
            var bg = new GameObject("Background"); bg.transform.SetParent(root, false);
            var img = bg.AddComponent<Image>();
            img.sprite = backgroundSprite;
            img.color = backgroundSprite != null ? Color.white : new Color(0.05f, 0.05f, 0.09f, 1f);
            img.raycastTarget = false;
            var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var ov = new GameObject("Overlay"); ov.transform.SetParent(root, false);
            var oimg = ov.AddComponent<Image>(); oimg.color = new Color(0.02f, 0.02f, 0.05f, 0.5f); oimg.raycastTarget = false;
            var ort = oimg.rectTransform; ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
        }

        // Give a TMP label a crisp dark outline (legible over the cinematic background) via an
        // instanced font material — selective, so cards/blurbs on dark panels stay clean.
        private static void AddOutline(GameObject go, Color color, int dist)
        {
            var t = go.GetComponent<TMP_Text>();
            if (t == null) return;
            var m = t.fontMaterial;   // TMP creates a per-instance material on access
            m.EnableKeyword("OUTLINE_ON");
            m.SetColor("_OutlineColor", color);
            m.SetFloat("_OutlineWidth", Mathf.Clamp01(dist * 0.06f));
        }

        private static TextAlignmentOptions MapAlign(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
            TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center
        };

        private TMP_Text Label(RectTransform parent, string text, Vector2 anchor, int size, TextAnchor align, float width)
        {
            var go = new GameObject("Label"); go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font; t.text = text; t.fontSize = size; t.alignment = MapAlign(align); t.color = Color.white;
            t.richText = true; t.raycastTarget = false; t.enableWordWrapping = true; t.overflowMode = TextOverflowModes.Overflow;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(width, size * 4 + 20);
            return t;
        }

        private Button MenuButton(RectTransform parent, string label, float anchorY, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button"); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(420, 60);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = new Color(0.16f, 0.26f, 0.42f, 0.92f);
            cb.highlightedColor = new Color(0.34f, 0.52f, 0.8f, 1f);
            cb.pressedColor = new Color(0.1f, 0.16f, 0.28f, 1f);
            cb.selectedColor = cb.normalColor; cb.fadeDuration = 0.12f;
            btn.colors = cb; img.color = cb.normalColor;
            btn.onClick.AddListener(() => { Audio?.PlaySfx("ui_click"); onClick(); });
            var t = Label((RectTransform)go.transform, label, new Vector2(0.5f, 0.5f), 26, TextAnchor.MiddleCenter, 420);
            t.fontStyle = FontStyles.Bold; AddOutline(t.gameObject, new Color(0, 0, 0, 0.7f), 1);
            return btn;
        }

        private Image Card(RectTransform parent, string name, string blurb, Sprite portrait, float anchorY, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Card_" + name); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0.1f, 0.12f, 0.2f, 0.85f);
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(960, 115);
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(() => { Audio?.PlaySfx("ui_click"); onClick(); });

            // Hero art on the left of the card.
            if (portrait != null)
            {
                var pg = new GameObject("Portrait"); pg.transform.SetParent(rt, false);
                var pimg = pg.AddComponent<Image>(); pimg.sprite = portrait; pimg.preserveAspect = true; pimg.raycastTarget = false;
                var prt = pimg.rectTransform;
                prt.anchorMin = new Vector2(0, 0.5f); prt.anchorMax = new Vector2(0, 0.5f); prt.pivot = new Vector2(0, 0.5f);
                prt.anchoredPosition = new Vector2(18, 4); prt.sizeDelta = new Vector2(150, 150);
            }

            var nameT = Label(rt, name, new Vector2(0.5f, 0.74f), 30, TextAnchor.MiddleLeft, 760);
            nameT.color = new Color(1f, 0.9f, 0.5f); nameT.fontStyle = FontStyles.Bold;
            nameT.rectTransform.anchoredPosition = new Vector2(95, nameT.rectTransform.anchoredPosition.y);
            var blurbT = Label(rt, blurb, new Vector2(0.5f, 0.3f), 18, TextAnchor.MiddleLeft, 740);
            blurbT.rectTransform.anchoredPosition = new Vector2(105, blurbT.rectTransform.anchoredPosition.y);
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
