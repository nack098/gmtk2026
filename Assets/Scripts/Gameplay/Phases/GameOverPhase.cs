using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashCount.Gameplay.Phases
{
    public class GameOverPhase : IGamePhaseState
    {
        public string PhaseName => "Game Over Phase";

        private readonly GamePhaseManager _manager;

        public GameOverPhase(GamePhaseManager manager)
        {
            _manager = manager;
        }

        private float _inputCooldown = 0.5f;

        public void Enter()
        {
            Debug.Log("[GameOverPhase] GAME OVER! Player or Father died (Healthy <= 0).");

            _inputCooldown = 0.5f; // Cooldown (0.5s real time) to avoid accidental instant skip

            // Pause time when Game Over occurs
            Time.timeScale = 0f;

            // Unlock and show cursor for Game Over UI interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var starterInputs = Object.FindObjectsByType<StarterAssets.StarterAssetsInputs>();
            foreach (var si in starterInputs)
            {
                if (si != null)
                {
                    si.cursorLocked = false;
                    si.cursorInputForLook = false;
                    si.enabled = false;
                }
            }

            if (_manager != null)
            {
                _manager.TriggerGameOver();
            }
        }

        public void Update()
        {
            if (_inputCooldown > 0f)
            {
                _inputCooldown -= Time.unscaledDeltaTime;
                return;
            }

            bool anyInputTriggered = false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame)
            {
                anyInputTriggered = true;
            }
            else if (UnityEngine.InputSystem.Mouse.current != null &&
                (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame ||
                 UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame ||
                 UnityEngine.InputSystem.Mouse.current.middleButton.wasPressedThisFrame))
            {
                anyInputTriggered = true;
            }
            else if (UnityEngine.InputSystem.Gamepad.current != null)
            {
                foreach (var control in UnityEngine.InputSystem.Gamepad.current.allControls)
                {
                    if (control is UnityEngine.InputSystem.Controls.ButtonControl btn && btn.wasPressedThisFrame)
                    {
                        anyInputTriggered = true;
                        break;
                    }
                }
            }
#endif

            if (!anyInputTriggered && Input.anyKeyDown)
            {
                anyInputTriggered = true;
            }

            if (anyInputTriggered)
            {
                Debug.Log("[GameOverPhase] Press any button detected! Loading Main Menu...");
                LoadMainMenu();
            }
        }

        public void Exit()
        {
            Time.timeScale = 1.0f;
            Debug.Log("[GameOverPhase] Exited Game Over Phase.");
        }

        public void RestartGame()
        {
            Time.timeScale = 1.0f;
            string currentScene = SceneManager.GetActiveScene().name;
            Debug.Log($"[SceneTransition] Restarting active scene '{currentScene}'...");
            SceneManager.LoadScene(currentScene);
        }

        public void LoadMainMenu()
        {
            Time.timeScale = 1.0f;
            GamePhaseManager.ResetSession();
            Debug.Log("[SceneTransition] Navigating to 'MenuScene' from Game Over...");
            SceneManager.LoadScene("MenuScene");
        }
    }
}
