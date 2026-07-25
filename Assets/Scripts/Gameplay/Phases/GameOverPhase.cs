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

        public void Enter()
        {
            Debug.Log("[GameOverPhase] GAME OVER! Player or Father died (Healthy <= 0).");

            // Unlock and show cursor for Game Over UI interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_manager != null)
            {
                _manager.TriggerGameOver();
            }
        }

        public void Update()
        {
            // Game Over state - waiting for user restart or main menu input
        }

        public void Exit()
        {
            Debug.Log("[GameOverPhase] Exited Game Over Phase.");
        }

        public void RestartGame()
        {
            Time.timeScale = 1.0f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void LoadMainMenu()
        {
            Time.timeScale = 1.0f;
            SceneManager.LoadScene("MainMenu");
        }
    }
}
