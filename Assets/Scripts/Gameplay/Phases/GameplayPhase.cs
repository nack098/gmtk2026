using UnityEngine;

namespace TrashCount.Gameplay.Phases
{
    public class GameplayPhase : IGamePhaseState
    {
        public string PhaseName => "Gameplay Phase";

        private readonly GamePhaseManager _manager;
        public float RemainingTime { get; private set; }
        public float DurationSeconds { get; private set; }

        private readonly float _timeMultiplier;
        private const float StartHourSeconds = 9f * 3600f; // 09:00 AM (32,400s)
        private const float InGameDurationSeconds = 10f * 3600f; // 10 in-game hours (09:00 to 19:00 = 36,000s)

        public GameplayPhase(GamePhaseManager manager, float durationSeconds = 600f)
        {
            _manager = manager;
            DurationSeconds = durationSeconds;
            RemainingTime = durationSeconds;

            // Dynamically calculate time multiplier based on configured real-life duration
            _timeMultiplier = InGameDurationSeconds / (durationSeconds > 0f ? durationSeconds : 600f);
        }

        public void Enter()
        {
            Debug.Log($"[GameplayPhase] Gameplay phase started with duration {DurationSeconds / 60f} mins (09:00 to 19:00 in-game time)!");

            // Lock cursor for third-person gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var starterInputs = Object.FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
            

            // Initialize/Reset clock time to 09:00 AM for the current day
            if (_manager.Data != null && _manager.Data.DateTime != null)
            {
                _manager.Data.DateTime.TotalSeconds = StartHourSeconds;
            }

            // Sync latest GameData into Playstat
            var playstat = Object.FindAnyObjectByType<Playstat>();
            if (playstat != null)
            {
                playstat.SyncFromGameData();
            }
        }

        public void Update()
        {
            RemainingTime -= Time.deltaTime;

            // Dynamically advance in-game clock from 09:00 to 19:00 over the configured duration
            if (_manager.Data != null && _manager.Data.DateTime != null)
            {
                _manager.Data.DateTime.Update(Time.deltaTime * _timeMultiplier);
            }

            if (RemainingTime <= 0f)
            {
                RemainingTime = 0f;
                if (_manager != null && _manager.CheckGameOverCondition())
                {
                    Debug.LogWarning("[GameplayPhase] Timer completed, but Game Over condition met! Transitioning to GameOverPhase.");
                    _manager.ChangePhase(new GameOverPhase(_manager));
                }
                else
                {
                    Debug.Log("[GameplayPhase] Gameplay phase completed! Transitioning to Shopping Phase.");
                    _manager.ChangePhase(new ShoppingPhase(_manager));
                }
            }
        }

        public void Exit()
        {
            Debug.Log("[GameplayPhase] Exited Gameplay Phase.");

            // Sync latest Playstat stats back into GameData
            var playstat = Object.FindAnyObjectByType<Playstat>();
            if (playstat != null)
            {
                playstat.SyncToGameData();
            }

            // Save items from PushCart into GameData.CartItemsData
            if (TrashCount.Gameplay.TrashSystem.PushCart.Instance != null && _manager.Data != null)
            {
                _manager.Data.CartItemsData.Clear();
                foreach (var worldItem in TrashCount.Gameplay.TrashSystem.PushCart.Instance.ItemsInCart)
                {
                    if (worldItem != null && worldItem.Model != null)
                    {
                        if (string.IsNullOrEmpty(worldItem.Model.ItemName) || worldItem.Model.ItemName == "Item")
                        {
                            worldItem.Model.ItemName = worldItem.State.ToString();
                        }
                        _manager.Data.CartItemsData.Add(worldItem.Model);
                    }
                }
                Debug.Log($"[GameplayPhase] Saved {_manager.Data.CartItemsData.Count} items from PushCart into GameData.CartItemsData!");
            }
    
            // Transition to ZenShopScene if not Game Over and currently in another scene
            if (_manager != null && !_manager.CheckGameOverCondition() && !(_manager.CurrentPhase is GameOverPhase))
            {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "ZenShopScene")
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("ZenShopScene");
                }
            }
            else
            {
                Debug.Log("[GameplayPhase] Game Over condition active - Skipping transition to ZenShopScene.");
            }
        }
    }
}
