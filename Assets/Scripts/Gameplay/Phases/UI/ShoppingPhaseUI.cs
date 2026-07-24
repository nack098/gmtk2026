using System.Collections.Generic;
using UnityEngine;
using TMPro;
using TrashCount.Data.Models;

namespace TrashCount.Gameplay.Phases.UI
{
    public class ShoppingPhaseUI : MonoBehaviour
    {
        public static ShoppingPhaseUI Instance { get; private set; }

        [Header("Shopping Panel Reference")]
        [SerializeField] private GameObject shoppingPanel;

        [Header("Zone Containers")]
        [SerializeField] private Transform cartZoneContainer;
        [SerializeField] private Transform inventoryZoneContainer;
        [SerializeField] private Transform buyZoneContainer;
        [SerializeField] private Transform sellZoneContainer;

        [Header("Prefabs")]
        [SerializeField] private UIDragItem dragItemPrefab;

        [Header("UI Text References")]
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private TextMeshProUGUI playerHungerText;
        [SerializeField] private TextMeshProUGUI fatherHungerText;
        [SerializeField] private TextMeshProUGUI statusMessageText;

        private ShoppingPhase _currentShoppingPhase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (GamePhaseManager.Instance != null)
            {
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

        private void Update()
        {
            if (shoppingPanel != null && shoppingPanel.activeSelf)
            {
                UpdateUIDisplay();
            }
        }

        private void HandlePhaseChanged(IGamePhaseState newPhase)
        {
            if (newPhase is ShoppingPhase shoppingPhase)
            {
                _currentShoppingPhase = shoppingPhase;
                if (shoppingPanel != null) shoppingPanel.SetActive(true);
            }
            else
            {
                _currentShoppingPhase = null;
                if (shoppingPanel != null) shoppingPanel.SetActive(false);
            }
        }

        private void UpdateUIDisplay()
        {
            if (GamePhaseManager.Instance == null || GamePhaseManager.Instance.Data == null) return;

            var data = GamePhaseManager.Instance.Data;

            if (moneyText != null) moneyText.text = $"Money: ${data.Money}";
            if (playerHungerText != null && data.PlayerData != null) playerHungerText.text = $"Player Hunger: {data.PlayerData.Hunger:F0}%";
            if (fatherHungerText != null && data.FatherData != null) fatherHungerText.text = $"Father Hunger: {data.FatherData.Hunger:F0}%";
        }

        public void HandleItemDrop(UIDragItem dragItem, ZoneType targetZone)
        {
            if (_currentShoppingPhase == null || dragItem == null || dragItem.Item == null) return;

            ItemModel item = dragItem.Item;
            ZoneType sourceZone = dragItem.CurrentZone;

            // Handle Buying from Buy Zone
            if (sourceZone == ZoneType.Buy && targetZone != ZoneType.Buy)
            {
                bool success = _currentShoppingPhase.BuyItem(item);
                if (success)
                {
                    SetStatusMessage("Item purchased successfully!");
                }
                else
                {
                    SetStatusMessage("Cannot buy item! (Not enough money)");
                    return;
                }
            }

            // Handle Selling into Sell Zone
            if (targetZone == ZoneType.Sell)
            {
                _currentShoppingPhase.SellItem(item);
                SetStatusMessage($"Sold item for ${item.SellPrice}!");
                Destroy(dragItem.gameObject);
                return;
            }

            // Handle Storing into Inventory Zone
            if (targetZone == ZoneType.Inventory)
            {
                _currentShoppingPhase.KeepInInventory(item);
                SetStatusMessage("Stored item in Inventory.");
            }

            // Move UI element transform to target container
            Transform targetContainer = GetZoneContainer(targetZone);
            if (targetContainer != null)
            {
                dragItem.transform.SetParent(targetContainer, false);
                dragItem.Setup(item, targetZone);
            }
        }

        private Transform GetZoneContainer(ZoneType zone)
        {
            return zone switch
            {
                ZoneType.Cart => cartZoneContainer,
                ZoneType.Inventory => inventoryZoneContainer,
                ZoneType.Buy => buyZoneContainer,
                ZoneType.Sell => sellZoneContainer,
                _ => null
            };
        }

        public void OnClickUseOnPlayer(ItemModel item)
        {
            if (_currentShoppingPhase == null || item == null) return;

            _currentShoppingPhase.UseOnPlayer(item);
            SetStatusMessage("Used item on Player!");
        }

        public void OnClickUseOnFather(ItemModel item)
        {
            if (_currentShoppingPhase == null || item == null) return;

            _currentShoppingPhase.UseOnFather(item);
            SetStatusMessage("Used item on Father!");
        }

        public void OnClickCompleteShopping()
        {
            if (_currentShoppingPhase == null) return;

            _currentShoppingPhase.CompleteShoppingPhase();
        }

        private void SetStatusMessage(string message)
        {
            if (statusMessageText != null)
            {
                statusMessageText.text = message;
            }
        }
    }
}
