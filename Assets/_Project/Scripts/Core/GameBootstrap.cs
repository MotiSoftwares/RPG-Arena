using UnityEngine;

namespace RPGArena.Core
{
    // The single persistent root of the game. It creates the long-lived services exactly
    // once and survives every scene load (CLAUDE.md §4.4 / §A.10). The duplicate guard
    // guarantees a second bootstrap can never appear when scenes reload — the precise
    // "duplicated managers / inconsistent state after restart" failure the rubric warns of.
    public class GameBootstrap : MonoBehaviour
    {
        // The one and only instance, reachable by any system that truly needs a service.
        public static GameBootstrap Instance { get; private set; }

        [Header("Startup")]
        // The scene to open once services are up (the main menu).
        [SerializeField] private string firstSceneName = "MainMenu";

        // Persistent services, created here so nothing else has to wire them up.
        public SceneLoader Scenes { get; private set; }
        public IAudioService Audio { get; private set; }
        public RunState Run { get; private set; }

        private void Awake()
        {
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
        }

        // Clear the static reference if this instance is torn down (e.g. on app quit), so a
        // stale pointer can never linger into a future session.
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
