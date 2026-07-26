using UnityEngine;
using TMPro;
using TrashCount.Gameplay.Phases;

namespace TrashCount.Gameplay.Phases.UI
{
    public class GamePhaseUI : MonoBehaviour
    {
        [Header("UI Text References")]
        [SerializeField] private TextMeshProUGUI phaseNameText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI dayText;

        [Header("Game Over UI Panel")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TextMeshProUGUI daysSurvivedText;
        [SerializeField] private UnityEngine.UI.Button mainMenuButton;

        private void Awake()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(OnClickMainMenu);
                mainMenuButton.onClick.AddListener(OnClickMainMenu);
            }
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void Start()
        {
            SubscribeToEvents();

            if (GamePhaseManager.Instance != null && GamePhaseManager.Instance.CurrentPhase != null)
            {
                HandlePhaseChanged(GamePhaseManager.Instance.CurrentPhase);
            }
        }

        private void SubscribeToEvents()
        {
            if (GamePhaseManager.Instance != null)
            {
                GamePhaseManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
                GamePhaseManager.Instance.OnPhaseChanged += HandlePhaseChanged;

                GamePhaseManager.Instance.OnGameOver -= HandleGameOver;
                GamePhaseManager.Instance.OnGameOver += HandleGameOver;
            }
        }

        private void OnDisable()
        {
            if (GamePhaseManager.Instance != null)
            {
                GamePhaseManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
                GamePhaseManager.Instance.OnGameOver -= HandleGameOver;
            }
        }

        private void Update()
        {
            if (GamePhaseManager.Instance == null) return;

            // Update Phase Name
            if (phaseNameText != null && GamePhaseManager.Instance.CurrentPhase != null)
            {
                phaseNameText.text = GamePhaseManager.Instance.CurrentPhase.PhaseName;
            }

            // Update Timer for GameplayPhase
            if (timerText != null && GamePhaseManager.Instance.CurrentPhase is GameplayPhase gameplayPhase)
            {
                float time = gameplayPhase.RemainingTime;
                int minutes = Mathf.FloorToInt(time / 60f);
                int seconds = Mathf.FloorToInt(time % 60f);
                timerText.text = $"{minutes:00}:{seconds:00}";
            }

            // Update Day counter
            if (dayText != null && GamePhaseManager.Instance.Data != null && GamePhaseManager.Instance.Data.DateTime != null)
            {
                dayText.text = $"DAY {GamePhaseManager.Instance.Data.DateTime.Day}";
            }
        }

        private void HandlePhaseChanged(IGamePhaseState newPhase)
        {
            Debug.Log($"[GamePhaseUI] UI updated for new phase: {newPhase?.PhaseName}");
            if (newPhase is GameOverPhase)
            {
                HandleGameOver();
            }
            else
            {
                if (gameOverPanel != null)
                {
                    gameOverPanel.SetActive(false);
                }
            }
        }

        private void HandleGameOver()
        {
            // 1. Force unlock cursor and disable StarterAssetsInputs to prevent mouse capture
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

            // 2. Activate GameOverPanel and bring to front
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                gameOverPanel.transform.SetAsLastSibling();
            }

            // 3. Auto-find button if unassigned
            if (mainMenuButton == null && gameOverPanel != null)
            {
                mainMenuButton = gameOverPanel.GetComponentInChildren<UnityEngine.UI.Button>(true);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(OnClickMainMenu);
                mainMenuButton.onClick.AddListener(OnClickMainMenu);

                var buttonText = mainMenuButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (buttonText != null)
                {
                    buttonText.text = "Press Any Button to Main Menu";
                }
                else
                {
                    var uiText = mainMenuButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
                    if (uiText != null) uiText.text = "Press Any Button to Main Menu";
                }
            }

            float daysSurvived = 1;
            if (GamePhaseManager.Instance != null && GamePhaseManager.Instance.Data != null && GamePhaseManager.Instance.Data.DateTime != null)
            {
                daysSurvived = GamePhaseManager.Instance.Data.DateTime.Day;
            }

            if (daysSurvivedText != null)
            {
                daysSurvivedText.text = $"You Survived {daysSurvived} Days!";
            }

            Debug.Log("[GamePhaseUI] GameOverPanel displayed! (Press Any Button to Main Menu)");
        }

        public void OnClickMainMenu()
        {
            Debug.Log("[GamePhaseUI] User clicked Main Menu button on Game Over Panel.");

            if (GamePhaseManager.Instance != null && GamePhaseManager.Instance.CurrentPhase is GameOverPhase gameOverPhase)
            {
                gameOverPhase.LoadMainMenu();
            }
            else
            {
                Time.timeScale = 1.0f;
                GamePhaseManager.ResetSession();
                Debug.Log("[GamePhaseUI] Navigating directly to 'MenuScene'...");
                UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
            }
        }
    }
}
