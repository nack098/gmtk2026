using System;
using UnityEngine;
using TrashCount.Data;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace TrashCount.Gameplay.Phases
{
    public class GamePhaseManager : MonoBehaviour
    {
        public static GamePhaseManager Instance { get; private set; }

        [Header("Data Reference")]
        [SerializeField] private GameData gameData;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private UnityEngine.InputSystem.InputActionAsset uiActionsAsset;
#endif

        [Header("Phase Duration Settings")]
        [Tooltip("Duration of Gameplay Phase in real-life minutes (Default: 10 mins)")]
        [SerializeField] private float gameplayDurationMinutes = 10f;

        [Header("Data Reset Settings")]
        [Tooltip("If checked, resets GameData (Day 1, Healthy 100, Hunger 100, Money 0) when starting a new game.")]
        [SerializeField] private bool resetDataOnStart = true;

        public IGamePhaseState CurrentPhase { get; private set; }
        public GameData Data => gameData;
        public float GameplayDurationMinutes => gameplayDurationMinutes;

        public event Action<IGamePhaseState> OnPhaseChanged;
        public event Action OnGameOver;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureEventSystemExists();
        }

        private void EnsureEventSystemExists()
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            }

            if (eventSystem == null)
            {
                var esGO = new GameObject("EventSystem");
                eventSystem = esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();

#if ENABLE_INPUT_SYSTEM
                var inputModule = esGO.AddComponent<InputSystemUIInputModule>();
                if (uiActionsAsset != null)
                {
                    inputModule.actionsAsset = uiActionsAsset;
                }
                else
                {
                    inputModule.AssignDefaultActions();
                }
#else
                esGO.AddComponent<StandaloneInputModule>();
#endif
                Debug.Log("[GamePhaseManager] Auto-created missing EventSystem in scene.");
            }
        }

        private static bool _isNewGameSession = true;

        private void Start()
        {
            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (activeScene == "ZenShopScene" || activeScene == "ShopScene")
            {
                // Force unlock cursor for shopping UI interaction
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                // Disable ALL StarterAssetsInputs to prevent OnApplicationFocus from re-locking cursor
                var allStarterInputs = UnityEngine.Object.FindObjectsByType<StarterAssets.StarterAssetsInputs>();
                foreach (var si in allStarterInputs)
                {
                    si.cursorLocked = false;
                    si.cursorInputForLook = false;
                    si.enabled = false;
                    Debug.Log($"[GamePhaseManager] Disabled StarterAssetsInputs on '{si.gameObject.name}' to prevent cursor re-lock.");
                }

                // Enter Shopping Phase automatically when loading shop scene
                ChangePhase(new ShoppingPhase(this));
            }
            else
            {
                // Only reset data on the very first fresh game session launch, not on scene transitions
                if (resetDataOnStart && _isNewGameSession)
                {
                    ResetNewGameData();
                    _isNewGameSession = false;
                }

                // Start Gameplay Phase for 3D exploration scene
                StartGameplayPhase();
            }
        }

        public void ResetNewGameData()
        {
            if (gameData == null) return;

            gameData.Money = 0;

            if (gameData.DateTime != null)
            {
                gameData.DateTime.Day = 1;
                gameData.DateTime.TotalSeconds = 9f * 3600f; // 09:00 AM
            }

            if (gameData.PlayerData != null)
            {
                gameData.PlayerData.Healthy = 100f;
                gameData.PlayerData.Hunger = 100f;
                gameData.PlayerData.IsSick = false;
            }

            if (gameData.FatherData != null)
            {
                gameData.FatherData.Healthy = 100f;
                gameData.FatherData.Hunger = 100f;
                gameData.FatherData.IsSick = false;
            }

            if (gameData.CartItemsData != null)
            {
                gameData.CartItemsData.Clear();
            }

            Debug.Log("[GamePhaseManager] GameData reset for Day 1 fresh game!");
        }

        public void StartGameplayPhase()
        {
            float durationSeconds = gameplayDurationMinutes * 60f;
            ChangePhase(new GameplayPhase(this, durationSeconds));
        }

        private void Update()
        {
            // Check Game Over Condition in any phase
            if (CheckGameOverCondition())
            {
                if (!(CurrentPhase is GameOverPhase))
                {
                    ChangePhase(new GameOverPhase(this));
                }
            }

            CurrentPhase?.Update();
        }

        public void ChangePhase(IGamePhaseState newPhase)
        {
            CurrentPhase?.Exit();
            CurrentPhase = newPhase;
            CurrentPhase?.Enter();

            OnPhaseChanged?.Invoke(CurrentPhase);
            Debug.Log($"[GamePhaseManager] Entered phase: {CurrentPhase?.PhaseName}");
        }

        public bool CheckGameOverCondition()
        {
            if (gameData == null) return false;

            bool playerDead = gameData.PlayerData != null && gameData.PlayerData.Healthy <= 0f;
            bool fatherDead = gameData.FatherData != null && gameData.FatherData.Healthy <= 0f;

            return playerDead || fatherDead;
        }

        public void TriggerGameOver()
        {
            OnGameOver?.Invoke();
        }
    }
}
