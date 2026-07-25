using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TrashCount.Gameplay.Phases.UI
{
    public class StandbyUI : MonoBehaviour
    {
        public static StandbyUI Instance { get; private set; }

        [Header("Summary UI Panel Reference")]
        [SerializeField] private GameObject summaryPanel;

        [Header("Camera Switching (Standard Unity Camera)")]
        [SerializeField] private Camera summaryCamera;
        [SerializeField] private Camera mainCamera;

        [Header("Cinemachine Virtual Camera Support (Optional)")]
        [Tooltip("Drag Cinemachine Virtual Camera GameObject here to auto-switch during Standby Phase.")]
        [SerializeField] private GameObject summaryCinemachineCam;
        [SerializeField] private GameObject mainCinemachineCam;

        [Header("UI Text References")]
        [SerializeField] private TextMeshProUGUI daySummaryTitleText;
        [SerializeField] private TextMeshProUGUI playerStatusText;
        [SerializeField] private TextMeshProUGUI fatherStatusText;
        [SerializeField] private TextMeshProUGUI moneyBalanceText;

        [Header("UI Button Reference")]
        [SerializeField] private Button startNextDayButton;

        private StandbyPhase _currentStandbyPhase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (summaryPanel != null)
            {
                summaryPanel.SetActive(false);
            }

            if (startNextDayButton != null)
            {
                startNextDayButton.onClick.AddListener(OnClickStartNextDay);
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
            }
        }

        private void OnDisable()
        {
            if (GamePhaseManager.Instance != null)
            {
                GamePhaseManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            }
        }

        private void HandlePhaseChanged(IGamePhaseState newPhase)
        {
            if (newPhase is StandbyPhase standbyPhase)
            {
                _currentStandbyPhase = standbyPhase;
                ShowSummaryUI();
            }
            else
            {
                _currentStandbyPhase = null;
                HideSummaryUI();
            }
        }

        public void ShowSummaryUI()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (summaryPanel != null)
            {
                summaryPanel.SetActive(true);
                summaryPanel.transform.SetAsLastSibling();
            }

            // Cinemachine Camera Switching
            if (summaryCinemachineCam != null)
            {
                summaryCinemachineCam.SetActive(true);
                if (mainCinemachineCam != null) mainCinemachineCam.SetActive(false);
            }
            // Standard Unity Camera Switching
            else if (summaryCamera != null)
            {
                summaryCamera.enabled = true;
                if (mainCamera != null) mainCamera.enabled = false;
            }

            UpdateSummaryDisplay();
        }

        public void HideSummaryUI()
        {
            if (summaryPanel != null)
            {
                summaryPanel.SetActive(false);
            }

            // Restore Cinemachine Camera
            if (summaryCinemachineCam != null)
            {
                summaryCinemachineCam.SetActive(false);
                if (mainCinemachineCam != null) mainCinemachineCam.SetActive(true);
            }
            // Restore Standard Unity Camera
            else if (summaryCamera != null)
            {
                summaryCamera.enabled = false;
                if (mainCamera != null) mainCamera.enabled = true;
            }
        }

        private void UpdateSummaryDisplay()
        {
            if (GamePhaseManager.Instance == null || GamePhaseManager.Instance.Data == null) return;
            var data = GamePhaseManager.Instance.Data;

            float completedDay = data.DateTime != null ? Mathf.Max(1, data.DateTime.Day - 1) : 1;

            if (daySummaryTitleText != null)
            {
                daySummaryTitleText.text = $"DAY {completedDay} SUMMARY";
            }

            if (playerStatusText != null && data.PlayerData != null)
            {
                string sickStr = data.PlayerData.IsSick ? "<color=red>(SICK!)</color>" : "<color=green>(Healthy)</color>";
                playerStatusText.text = $"Player: Healthy {data.PlayerData.Healthy:F0}% | Hunger {data.PlayerData.Hunger:F0}% {sickStr}";
            }

            if (fatherStatusText != null && data.FatherData != null)
            {
                string sickStr = data.FatherData.IsSick ? "<color=red>(SICK!)</color>" : "<color=green>(Healthy)</color>";
                fatherStatusText.text = $"Father: Healthy {data.FatherData.Healthy:F0}% | Hunger {data.FatherData.Hunger:F0}% {sickStr}";
            }

            if (moneyBalanceText != null)
            {
                moneyBalanceText.text = $"Money Balance: ${data.Money}";
            }
        }

        public void OnClickStartNextDay()
        {
            if (_currentStandbyPhase != null)
            {
                _currentStandbyPhase.ConfirmNextDay();
            }
        }
    }
}
