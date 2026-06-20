using UnityEngine;
using UnityEngine.InputSystem;
using RPGArena.Combat;
using RPGArena.Characters;
using RPGArena.Combat.Status;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RPGArena.Cheats
{
    // A developer cheat panel (CLAUDE.md §14.1, bonus +5). The ENTIRE RPGArena.Cheats assembly
    // compiles only when UNITY_EDITOR || DEVELOPMENT_BUILD is defined (see the asmdef's
    // defineConstraints), so it is stripped wholesale from a normal release build — there is no
    // cheat code, GameObject, or missing-script reference left behind.
    //
    // It self-installs via RuntimeInitializeOnLoadMethod, so no scene wiring is needed (which also
    // keeps the shipped scenes free of any dev-only references). Toggle the panel with backquote ` .
    public class CheatManager : MonoBehaviour
    {
        // Create the manager exactly once, after the first scene loads. Because the assembly
        // itself is dev-only, this method simply doesn't exist in a release build.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<CheatManager>() != null) return;
            var go = new GameObject("[CheatManager]");
            go.AddComponent<CheatManager>();
            DontDestroyOnLoad(go);
        }

        private bool show;
        private bool godMode;
        private bool infiniteMP;
        private float timeScale = 1f;
        private Rect window = new Rect(16, 16, 280, 560);
        private BattleController battle;

        // Re-find the live battle each time it might have changed (cheap; only on demand).
        private BattleController Battle => battle != null ? battle : (battle = FindFirstObjectByType<BattleController>());
        private BattleContext Ctx => Battle != null ? Battle.Context : null;

        private void Update()
        {
            // Toggle with the backquote key (new Input System; legacy Input may be disabled).
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame) show = !show;

            // Enforce the held toggles every frame so they can't be undone by combat.
            var ctx = Ctx;
            if (ctx == null) return;
            if (godMode)
                foreach (var h in ctx.heroes) { if (h.IsAlive) h.currentHP = h.stats.maxHP; }
            if (infiniteMP)
                foreach (var h in ctx.heroes) h.currentMP = h.stats.maxMP;
        }

        private void OnGUI()
        {
            if (!show) return;
            window = GUI.Window(0xC4EA7, window, DrawWindow, "CHEATS  (`)");
        }

        private void DrawWindow(int id)
        {
            var ctx = Ctx;
            if (ctx == null)
            {
                GUILayout.Label("No active battle.");
                GUI.DragWindow();
                return;
            }

            GUILayout.Label("— Party —", Bold());
            godMode = GUILayout.Toggle(godMode, " God Mode (party invincible)");
            infiniteMP = GUILayout.Toggle(infiniteMP, " Infinite MP");
            if (GUILayout.Button("Refill party HP + MP"))
                foreach (var h in ctx.heroes) { h.currentHP = h.stats.maxHP; h.currentMP = h.stats.maxMP; }
            if (GUILayout.Button("Wipe party (force DEFEAT)"))
                foreach (var h in ctx.heroes) h.currentHP = 0;

            GUILayout.Space(6);
            GUILayout.Label("— Boss —", Bold());
            if (ctx.boss != null)
            {
                GUILayout.Label($"HP {ctx.boss.currentHP}/{ctx.boss.stats.maxHP}   stagger {ctx.boss.staggerMeter:0}/{ctx.boss.staggerThreshold:0}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("-25%")) ctx.boss.currentHP = Mathf.Max(1, ctx.boss.currentHP - ctx.boss.stats.maxHP / 4);
                if (GUILayout.Button("→ 1 HP")) ctx.boss.currentHP = 1;
                if (GUILayout.Button("Kill (WIN)")) ctx.boss.currentHP = 0;
                GUILayout.EndHorizontal();

                if (GUILayout.Button("FORCE BREAK now"))
                    ctx.stagger.Break(ctx.boss, ctx);
                if (GUILayout.Button("Heal boss to full"))
                    ctx.boss.currentHP = ctx.boss.stats.maxHP;

#if UNITY_EDITOR
                GUILayout.Space(4);
                GUILayout.Label("Apply status to boss:");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Wet")) ApplyStatus(ctx.boss, "Wet");
                if (GUILayout.Button("Oiled")) ApplyStatus(ctx.boss, "Oiled");
                if (GUILayout.Button("Marked")) ApplyStatus(ctx.boss, "Marked");
                if (GUILayout.Button("Frozen")) ApplyStatus(ctx.boss, "Frozen");
                GUILayout.EndHorizontal();
#endif
            }

            GUILayout.Space(6);
            GUILayout.Label("— Time —", Bold());
            GUILayout.Label($"Time scale: {timeScale:0.00}x");
            timeScale = GUILayout.HorizontalSlider(timeScale, 0f, 2f);
            if (GUILayout.Button("Reset time scale")) timeScale = 1f;
            Time.timeScale = timeScale;

            GUILayout.Space(6);
            GUILayout.Label("— Info —", Bold());
            if (ctx.boss != null)
                GUILayout.Label($"AI cycle idx: {ctx.boss.aiCycleIndex}\nTelegraphed: {(ctx.boss.telegraphedAbility != null ? ctx.boss.telegraphedAbility.displayName : "none")}");

            GUILayout.Space(8);
            if (GUILayout.Button("Close (`)")) show = false;

            GUI.DragWindow();
        }

        private static GUIStyle _bold;
        private static GUIStyle Bold() => _bold ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

#if UNITY_EDITOR
        // Editor-only: load a status definition by name and apply it, so synergies can be tested
        // instantly. Uses AssetDatabase, which only exists in the Editor — hence the guard.
        private static void ApplyStatus(Entity target, string statusName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:StatusEffectDefinition {statusName}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(path);
                if (def != null && def.name == statusName) { target.Status.Apply(def); return; }
            }
            Debug.LogWarning($"[Cheats] Status '{statusName}' not found.");
        }
#endif
    }
}
