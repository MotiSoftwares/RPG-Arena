using UnityEngine;
using UnityEngine.UI;
using RPGArena.Core;

namespace RPGArena.UI
{
    // Tiny M0 main-menu behaviour: a "Start" button calls StartGame(), which asks the
    // persistent bootstrap to load the battle scene through the faded SceneLoader. The full
    // menu (How to Play, Settings, Credits, character select) arrives in Milestone 3.
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string battleSceneName = "BattleArena";

        // Optional explicit reference; if left empty we auto-find the first Button in our
        // children, so the scene needs no Inspector UnityEvent wiring (code-driven, clean).
        [SerializeField] private Button startButton;

        private void Start()
        {
            if (startButton == null) startButton = GetComponentInChildren<Button>(true);
            if (startButton != null) startButton.onClick.AddListener(StartGame);
        }

        // Loads the battle scene via the persistent bootstrap's faded SceneLoader.
        public void StartGame()
        {
            if (GameBootstrap.Instance != null)
                GameBootstrap.Instance.Scenes.LoadScene(battleSceneName);
            else
                Debug.LogWarning("[MainMenuController] No GameBootstrap — enter play from the Boot scene.");
        }

        // Quits the application in a build, or stops play mode in the editor.
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
