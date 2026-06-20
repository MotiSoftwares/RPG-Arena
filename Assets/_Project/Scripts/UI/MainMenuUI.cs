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
        private GameObject mainPanel, selectPanel, infoPanel;
        private Text infoText, selectHint;
        private Button confirmBtn;
        private readonly List<string> picked = new();
        private readonly Dictionary<string, Image> cardImages = new();

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUI();
            ShowMain();
        }

        // --- flow ---------------------------------------------------------------------
        private void ShowMain() { mainPanel.SetActive(true); selectPanel.SetActive(false); infoPanel.SetActive(false); }
        private void ShowSelect() { mainPanel.SetActive(false); selectPanel.SetActive(true); infoPanel.SetActive(false); picked.Clear(); RefreshCards(); }

        private void ShowInfo(string title, string body)
        {
            mainPanel.SetActive(false); selectPanel.SetActive(false); infoPanel.SetActive(true);
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
            MenuButton(mp, "Play", 0.55f, () => ShowSelect());
            MenuButton(mp, "How to Play", 0.46f, () => ShowInfo("HOW TO PLAY",
                "Pick 3 of 4 heroes. Read the boss: exploit its WEAKNESS (don't use what it absorbs!),\nset up cross-class combos (Oil+Fire, Wet+Ice/Lightning, Mark), and build the STAGGER bar.\nBreak the boss during its telegraphed charge to cancel the attack, then unload in the window.\nStrong attacks can miss — the action menu shows each move's hit %. Click an ability to act."));
            MenuButton(mp, "Credits", 0.37f, () => ShowInfo("CREDITS",
                "Arena of the Algorithms — a student software-engineering project.\nBuilt with Unity 6.3 (URP). SFX: ElevenLabs. Art: Pollinations. 3D: Tripo. Narrative: Ink.\nMade with AI assistance (Claude Code + Unity MCP)."));
            MenuButton(mp, "Quit", 0.28f, Quit);

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
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img; btn.onClick.AddListener(onClick);
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
