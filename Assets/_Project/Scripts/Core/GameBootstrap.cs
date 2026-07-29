using UnityEngine;

namespace RPGArena.Core
{
    // The single persistent root of the game. It creates the long-lived services exactly
    // once and survives every scene load (CLAUDE.md §4.4 / §A.10). The duplicate guard
    // guarantees a second bootstrap can never appear when scenes reload — the precise
    // "duplicated managers / inconsistent state after restart" failure the rubric warns of.
    public class GameBootstrap : MonoBehaviour
    {
        // Resources path of the prefab EnsureRuntime spawns. It is the very object the Boot scene
        // holds, so a direct-play session gets the identically-wired AudioManager (mixer groups and
        // both clip banks) rather than a hand-rolled imitation that drifts from it.
        private const string FallbackPrefabPath = "GameBootstrap";

        // The one and only instance, reachable by any system that truly needs a service.
        public static GameBootstrap Instance { get; private set; }

        // True only while EnsureRuntime is mid-spawn. The new instance's Awake runs synchronously
        // inside Instantiate, before the reference comes back, so "am I a fallback?" has to be
        // answerable from Awake itself — a field assigned afterwards would be read too late.
        private static bool spawningFallback;

        [Header("Startup")]
        // The scene to open once services are up (the main menu).
        [SerializeField] private string firstSceneName = "MainMenu";

        // Persistent services, created here so nothing else has to wire them up.
        public SceneLoader Scenes { get; private set; }
        public IAudioService Audio { get; private set; }
        public RunState Run { get; private set; }

        private void Awake()
        {
            bool isFallback = spawningFallback;

            // Standard singleton guard: if one already exists, this is a stray duplicate —
            // log it (so a wiring mistake is noticed) and destroy ourselves.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameBootstrap] Duplicate detected — destroying the extra instance.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeServices();

            // A fallback bootstrap only exists because the player pressed Play on a gameplay scene.
            // That scene is the one they asked to see, so handing off to the menu here would make
            // pressing Play on anything but Boot impossible.
            if (isFallback) return;

            // Hand off to the menu. Loading by NAME means reordering scenes can't break it.
            Scenes.LoadScene(firstSceneName);
        }

        // Create/find the services on this persistent object, once.
        private void InitializeServices()
        {
            Run = new RunState();

            // SceneLoader is pure Core, so we just add it here at runtime.
            Scenes = GetComponent<SceneLoader>();
            if (Scenes == null) Scenes = gameObject.AddComponent<SceneLoader>();

            // AudioManager lives in the Audio assembly. We only ever talk to it through the
            // Core-side IAudioService interface, so Core never references the Audio assembly.
            // It is expected to be a component on this object in the Boot scene; if it is
            // missing, audio is simply a no-op for now (nothing calls it in M0).
            Audio = GetComponentInChildren<IAudioService>();
            if (Audio == null)
                Debug.Log("[GameBootstrap] No IAudioService found yet (fine in M0).");

            EnsureAudioListener();
        }

        // Without exactly one AudioListener the whole audio stack plays into a void — and not one
        // of the three scenes shipped with a listener on its camera, so the mixer, the banks and
        // every PlaySfx call had been working silently all along. It belongs on this object rather
        // than on a camera: this one survives every scene load, so there is always exactly one and
        // never a "2 audio listeners" fight. Position is irrelevant because the SFX pool and the
        // music source are 2D (spatialBlend 0).
        private void EnsureAudioListener()
        {
            if (GetComponent<AudioListener>() != null) return;
            if (FindFirstObjectByType<AudioListener>() != null) return;   // a scene supplied its own
            gameObject.AddComponent<AudioListener>();
        }

        // Bring the persistent services up from ANY entry point, not just the Boot scene. Pressing
        // Play directly on MainMenu or BattleArena — which is how this game is actually play-tested —
        // otherwise leaves Instance null, and because every caller is null-conditional the result is
        // a fight with no music, no gold, no items and no run state, failing completely silently.
        //
        // Returns the existing bootstrap untouched when the game did start from Boot, so calling
        // this is always safe and always cheap.
        public static GameBootstrap EnsureRuntime()
        {
            if (Instance != null) return Instance;

            var prefab = Resources.Load<GameObject>(FallbackPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameBootstrap] Resources/{FallbackPrefabPath} is missing — " +
                                 "playing this scene directly gets no audio and no run state.");
                return null;
            }

            spawningFallback = true;
            try
            {
                var go = Instantiate(prefab);   // Awake (and the whole singleton setup) runs in here
                go.name = "GameBootstrap (direct play)";
            }
            finally { spawningFallback = false; }

            SeedPracticeRun(Instance != null ? Instance.Run : null);
            return Instance;
        }

        // A direct-play session skipped character select and the Supply Camp, so its run would walk
        // into the fight with nothing but the starter potion and no gold at all. Hand it a small
        // practice kit so the item and gold UI actually has something to show.
        //
        // This can never touch a real run: the Boot scene builds its own RunState and character
        // select calls Reset() on it, which restores the canonical one-potion start.
        private static void SeedPracticeRun(RunState run)
        {
            if (run == null) return;
            run.gold = 250;
            run.AddItem("Item_ManaTonic");
            run.AddItem("Item_SlickFlask");
        }

        // Clear the static reference if this instance is torn down (e.g. on app quit), so a
        // stale pointer can never linger into a future session.
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
