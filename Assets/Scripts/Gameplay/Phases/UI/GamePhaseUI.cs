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

        private void Awake()
        {
            // Ensure GamePhasePanel background images do not block UI raycasts
            var images = GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach (var img in images)
            {
                img.raycastTarget = false;
            }

            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnEnable()
        {
            if (GamePhaseManager.Instance != null)
            {
                GamePhaseManager.Instance.OnPhaseChanged += HandlePhaseChanged;
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
            Debug.Log($"[GamePhaseUI] UI updated for new phase: {newPhase.PhaseName}");
        }

        private void HandleGameOver()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }

            if (daysSurvivedText != null && GamePhaseManager.Instance.Data != null && GamePhaseManager.Instance.Data.DateTime != null)
            {
                daysSurvivedText.text = $"You Survived {GamePhaseManager.Instance.Data.DateTime.Day} Days!";
            }
        }
    }
}
