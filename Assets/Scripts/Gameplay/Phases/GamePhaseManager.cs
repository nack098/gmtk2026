using System;
using UnityEngine;
using TrashCount.Data;

namespace TrashCount.Gameplay.Phases
{
    public class GamePhaseManager : MonoBehaviour
    {
        public static GamePhaseManager Instance { get; private set; }

        [Header("Data Reference")]
        [SerializeField] private GameData gameData;

        [Header("Phase Duration Settings")]
        [Tooltip("Duration of Gameplay Phase in real-life minutes (Default: 10 mins)")]
        [SerializeField] private float gameplayDurationMinutes = 10f;

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
        }

        private void Start()
        {
            // Start the game loop at GameplayPhase with configurable duration
            StartGameplayPhase();
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

            bool playerDead = gameData.PlayerData != null && gameData.PlayerData.Hunger <= 0f;
            bool fatherDead = gameData.FatherData != null && gameData.FatherData.Hunger <= 0f;

            return playerDead || fatherDead;
        }

        public void TriggerGameOver()
        {
            OnGameOver?.Invoke();
        }
    }
}
