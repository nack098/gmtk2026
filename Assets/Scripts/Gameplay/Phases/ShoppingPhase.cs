using UnityEngine;
using TrashCount.Data.Models;

namespace TrashCount.Gameplay.Phases
{
    public class ShoppingPhase : IGamePhaseState
    {
        public string PhaseName => "Shopping Phase";

        private readonly GamePhaseManager _manager;

        public ShoppingPhase(GamePhaseManager manager)
        {
            _manager = manager;
        }

        public void Enter()
        {
            Debug.Log("[ShoppingPhase] Entered Shopping Phase. Player can manage items in cart and buy new items from shop.");

            // Unlock cursor and disable camera look input for UI interaction
            SetCursorUnlockedState(true);
        }

        public void Update()
        {
            // Waiting for user actions via Shopping UI
        }

        public void Exit()
        {
            Debug.Log("[ShoppingPhase] Shopping Phase completed.");

            // Lock cursor and re-enable camera look input for gameplay
            SetCursorUnlockedState(false);
        }

        private void SetCursorUnlockedState(bool unlocked)
        {
            Cursor.lockState = unlocked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = unlocked;

            var starterInputs = Object.FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.cursorLocked = !unlocked;
                starterInputs.cursorInputForLook = !unlocked;
            }
        }

        public bool BuyItem(ItemModel item)
        {
            if (item == null || _manager.Data == null) return false;

            if (item.TryGetCapability<BuyableCapability>(out var buyable))
            {
                if (_manager.Data.Money >= (uint)buyable.BuyPrice)
                {
                    _manager.Data.Money -= (uint)buyable.BuyPrice;

                    if (_manager.Data.InventoryData != null)
                    {
                        _manager.Data.InventoryData.Add(item);
                    }

                    Debug.Log($"[ShoppingPhase] Successfully bought item for {buyable.BuyPrice} money. Remaining Money: {_manager.Data.Money}");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"[ShoppingPhase] Not enough money! Required: {buyable.BuyPrice}, Has: {_manager.Data.Money}");
                }
            }
            else
            {
                Debug.LogWarning($"[ShoppingPhase] Item cannot be bought (Missing BuyableCapability).");
            }

            return false;
        }

        public void SellItem(ItemModel item)
        {
            if (item == null || _manager.Data == null) return;

            _manager.Data.Money += (uint)item.SellPrice;
            Debug.Log($"[ShoppingPhase] Sold item for {item.SellPrice} money. Total Money: {_manager.Data.Money}");
        }

        public void KeepInInventory(ItemModel item)
        {
            if (item == null || _manager.Data == null) return;

            if (_manager.Data.InventoryData != null)
            {
                _manager.Data.InventoryData.Add(item);
                Debug.Log($"[ShoppingPhase] Stored item into inventory.");
            }
        }

        public bool UseOnPlayer(ItemModel item)
        {
            if (item == null || _manager.Data == null) return false;

            if (item.TryGetCapability<EatableCapability>(out var eatable) && _manager.Data.PlayerData != null)
            {
                _manager.Data.PlayerData.Hunger = Mathf.Min(100f, _manager.Data.PlayerData.Hunger + eatable.RestoreAmount);
                if (eatable.HealthAmount > 0)
                {
                    _manager.Data.PlayerData.Healthy = Mathf.Min(100f, _manager.Data.PlayerData.Healthy + eatable.HealthAmount);
                }

                // Sync modified GameData stats into Playstat runtime instance
                var playstat = Object.FindAnyObjectByType<Playstat>();
                if (playstat != null)
                {
                    playstat.SyncFromGameData();
                }

                Debug.Log($"[ShoppingPhase] Used item on Player. Restored {eatable.RestoreAmount} hunger, {eatable.HealthAmount} health. Current Player Hunger: {_manager.Data.PlayerData.Hunger}");
                return true;
            }
            return false;
        }

        public bool UseOnFather(ItemModel item)
        {
            if (item == null || _manager.Data == null) return false;

            if (item.TryGetCapability<EatableCapability>(out var eatable) && _manager.Data.FatherData != null)
            {
                _manager.Data.FatherData.Hunger = Mathf.Min(100f, _manager.Data.FatherData.Hunger + eatable.RestoreAmount);
                if (eatable.HealthAmount > 0)
                {
                    _manager.Data.FatherData.Healthy = Mathf.Min(100f, _manager.Data.FatherData.Healthy + eatable.HealthAmount);
                }
                Debug.Log($"[ShoppingPhase] Used item on Father. Restored {eatable.RestoreAmount} hunger, {eatable.HealthAmount} health. Current Father Hunger: {_manager.Data.FatherData.Hunger}");
                return true;
            }
            return false;
        }

        public void CompleteShoppingPhase()
        {
            Debug.Log("[ShoppingPhase] Completing Shopping Phase and transitioning to Standby Phase.");
            _manager.ChangePhase(new StandbyPhase(_manager));
        }
    }
}
