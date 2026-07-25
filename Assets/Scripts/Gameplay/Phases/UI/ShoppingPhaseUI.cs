using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrashCount.Data.Models;
using TrashCount.Gameplay.TrashSystem;

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

        [Header("UI Button References")]
        [SerializeField] private Button completeShoppingButton;

        [Header("UI Text References")]
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private TextMeshProUGUI playerHealthyText;
        [SerializeField] private TextMeshProUGUI playerHungerText;
        [SerializeField] private TextMeshProUGUI fatherHealthyText;
        [SerializeField] private TextMeshProUGUI fatherHungerText;
        [SerializeField] private TextMeshProUGUI statusMessageText;
        
        private Playstat _playStat;
        

        private ShoppingPhase _currentShoppingPhase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Ensure GraphicRaycaster exists on Canvas for UI raycasting
            EnsureCanvasRaycasterExists();
            EnsureDropZonesHaveRaycastTargets();

            // Ensure shoppingPanel is closed by default when game starts
            if (shoppingPanel != null)
            {
                shoppingPanel.SetActive(false);
            }

            if (completeShoppingButton != null)
            {
                completeShoppingButton.onClick.AddListener(OnClickCompleteShopping);
            }
        }

        private void EnsureCanvasRaycasterExists()
        {
            var canvases = Object.FindObjectsByType<Canvas>();
            foreach (var canvas in canvases)
            {
                if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                    Debug.Log($"[ShoppingPhaseUI] Auto-added GraphicRaycaster to Canvas '{canvas.name}'!");
                }
            }
        }

        private void EnsureDropZonesHaveRaycastTargets()
        {
            var dropZones = Object.FindObjectsByType<UIItemDropZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var zone in dropZones)
            {
                if (zone != null)
                {
                    var img = zone.GetComponent<UnityEngine.UI.Image>();
                    if (img == null)
                    {
                        img = zone.gameObject.AddComponent<UnityEngine.UI.Image>();
                        img.color = new Color(0f, 0f, 0f, 0.005f); // Invisible raycast receiver for EventSystem OnDrop
                    }
                    img.raycastTarget = true;
                    Debug.Log($"[ShoppingPhaseUI] Enabled Raycast Target on DropZone '{zone.gameObject.name}' ({zone.Zone})!");
                }
            }
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void Start()
        {
            SubscribeToEvents();
            EnsureDropZonesHaveRaycastTargets();

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

        private void Update()
        {
            if (shoppingPanel != null && shoppingPanel.activeSelf)
            {
                UpdateUIDisplay();

#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                {
                    var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                    eventData.position = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                    var results = new List<UnityEngine.EventSystems.RaycastResult>();
                    UnityEngine.EventSystems.EventSystem.current?.RaycastAll(eventData, results);

                    Debug.Log($"[ShoppingUI Diagnostic] Left Click at {eventData.position}. Hit UI count: {results.Count}");
                    foreach (var r in results)
                    {
                        Debug.Log($"   -> Raycast Hit UI Element: '{r.gameObject.name}' (Canvas: '{r.module?.gameObject.name}')");
                    }
                }
#endif
            }
        }

        private void HandlePhaseChanged(IGamePhaseState newPhase)
        {
            Debug.Log($"[ShoppingPhaseUI] Phase changed event received: {newPhase?.PhaseName}");

            if (newPhase is ShoppingPhase shoppingPhase)
            {
                _currentShoppingPhase = shoppingPhase;
                if (shoppingPanel != null)
                {
                    shoppingPanel.SetActive(true);
                    shoppingPanel.transform.SetAsLastSibling(); // Bring Shopping Panel to front of Canvas hierarchy

                    // Disable Raycast Target on Shopping Panel's background Image so buttons & drag items get clicks
                    var bgImage = shoppingPanel.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        bgImage.raycastTarget = false;
                    }

                    // Disable Raycast Target on GamePhasePanel background if it exists
                    var gamePhasePanel = GameObject.Find("GamePhasePanel");
                    if (gamePhasePanel != null)
                    {
                        var panelImg = gamePhasePanel.GetComponent<Image>();
                        if (panelImg != null) panelImg.raycastTarget = false;

                        var cg = gamePhasePanel.GetComponent<CanvasGroup>();
                        if (cg != null) cg.blocksRaycasts = false;
                    }

                    Debug.Log("[ShoppingPhaseUI] Shopping Panel activated successfully!");
                    PopulateAllZones();
                }
                else
                {
                    Debug.LogWarning("[ShoppingPhaseUI] Cannot activate Shopping Panel: 'shoppingPanel' field is null/unassigned in Inspector!");
                }
            }
            else
            {
                _currentShoppingPhase = null;
                if (shoppingPanel != null) shoppingPanel.SetActive(false);
            }
        }

        public void PopulateAllZones()
        {
            EnsureDropZonesHaveRaycastTargets();
            PopulateCartZone();
            PopulateInventoryZone();
            PopulateBuyZone();
        }

        private void PopulateCartZone()
        {
            if (cartZoneContainer == null || dragItemPrefab == null) return;
            ClearContainer(cartZoneContainer);

            // Primary source: GameData.CartItemsData (supports scene switching)
            if (GamePhaseManager.Instance != null && GamePhaseManager.Instance.Data != null && GamePhaseManager.Instance.Data.CartItemsData != null && GamePhaseManager.Instance.Data.CartItemsData.Count > 0)
            {
                foreach (var itemModel in GamePhaseManager.Instance.Data.CartItemsData)
                {
                    if (itemModel == null) continue;
                    string itemName = !string.IsNullOrEmpty(itemModel.ItemName) ? itemModel.ItemName : "Cart Item";
                    UIDragItem itemUI = Instantiate(dragItemPrefab, cartZoneContainer);
                    itemUI.Setup(itemModel, ZoneType.Cart, itemName);
                }
            }
            // Fallback source: PushCart.Instance.ItemsInCart (single-scene mode)
            else if (PushCart.Instance != null && PushCart.Instance.ItemsInCart != null)
            {
                foreach (var worldItem in PushCart.Instance.ItemsInCart)
                {
                    if (worldItem == null) continue;

                    string itemName = worldItem.State.ToString();
                    ItemModel model = worldItem.Model ?? new ItemModel { ItemName = itemName, SellPrice = 25 };
                    if (string.IsNullOrEmpty(model.ItemName) || model.ItemName == "Item") model.ItemName = itemName;

                    UIDragItem itemUI = Instantiate(dragItemPrefab, cartZoneContainer);
                    itemUI.Setup(model, ZoneType.Cart, itemName);
                }
            }
        }

        private void PopulateInventoryZone()
        {
            if (inventoryZoneContainer == null || dragItemPrefab == null) return;
            ClearContainer(inventoryZoneContainer);

            if (GamePhaseManager.Instance != null && GamePhaseManager.Instance.Data != null && GamePhaseManager.Instance.Data.InventoryData != null)
            {
                var inventory = GamePhaseManager.Instance.Data.InventoryData;
                if (inventory.Items != null)
                {
                    foreach (var itemModel in inventory.Items)
                    {
                        if (itemModel == null) continue;
                        string itemName = !string.IsNullOrEmpty(itemModel.ItemName) ? itemModel.ItemName : "Stored Item";
                        UIDragItem itemUI = Instantiate(dragItemPrefab, inventoryZoneContainer);
                        itemUI.Setup(itemModel, ZoneType.Inventory, itemName);
                    }
                }
            }
        }

        private void PopulateBuyZone()
        {
            if (buyZoneContainer == null || dragItemPrefab == null) return;
            ClearContainer(buyZoneContainer);

            // Populate sample buyable items
            ItemModel cannedFood = new ItemModel { ItemName = "Canned Food", SellPrice = 15 };
            cannedFood.capabilities.Add(new BuyableCapability { BuyPrice = 30 });
            cannedFood.capabilities.Add(new EatableCapability { RestoreAmount = 40 });

            UIDragItem itemUI = Instantiate(dragItemPrefab, buyZoneContainer);
            itemUI.Setup(cannedFood, ZoneType.Buy, cannedFood.ItemName);
        }

        private void ClearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }

        private void UpdateUIDisplay()
        {
            if (GamePhaseManager.Instance == null || GamePhaseManager.Instance.Data == null) return;

            var data = GamePhaseManager.Instance.Data;

            if (moneyText != null) moneyText.text = $"Money: ${data.Money}";
            if (playerHealthyText != null && data.PlayerData != null)
            {
                string sickStr = data.PlayerData.IsSick ? " (Sick)" : "";
                playerHealthyText.text = $"Player Healthy: {data.PlayerData.Healthy:F0}%{sickStr}";
            }
            if (playerHungerText != null && data.PlayerData != null)
            {
                playerHungerText.text = $"Player Hunger: {data.PlayerData.Hunger:F0}%";
            }

            if (fatherHealthyText != null && data.FatherData != null)
            {
                string sickStr = data.FatherData.IsSick ? " (Sick)" : "";
                fatherHealthyText.text = $"Father Healthy: {data.FatherData.Healthy:F0}%{sickStr}";
            }
            if (fatherHungerText != null && data.FatherData != null)
            {
                fatherHungerText.text = $"Father Hunger: {data.FatherData.Hunger:F0}%";
            }
        }

        public void HandleItemDrop(UIDragItem dragItem, ZoneType targetZone)
        {
            if (_currentShoppingPhase == null || dragItem == null || dragItem.Item == null) return;

            ItemModel item = dragItem.Item;
            ZoneType sourceZone = dragItem.CurrentZone;

            if (sourceZone == targetZone) return;

            // 1. Handle Buying from Buy Zone
            if (sourceZone == ZoneType.Buy && targetZone != ZoneType.Buy)
            {
                bool success = _currentShoppingPhase.BuyItem(item);
                if (success)
                {
                    SetStatusMessage("Item purchased successfully!");
                    // Re-stock Buy Zone shelf
                    PopulateBuyZone();
                }
                else
                {
                    SetStatusMessage("Cannot buy item! (Not enough money)");
                    return;
                }
            }

            // 2. Handle Selling into Sell Zone
            if (targetZone == ZoneType.Sell)
            {
                if (sourceZone == ZoneType.Inventory && GamePhaseManager.Instance?.Data?.InventoryData != null)
                {
                    GamePhaseManager.Instance.Data.InventoryData.Remove(item);
                }
                else if (sourceZone == ZoneType.Cart && GamePhaseManager.Instance?.Data?.CartItemsData != null)
                {
                    GamePhaseManager.Instance.Data.CartItemsData.Remove(item);
                }

                _currentShoppingPhase.SellItem(item);
                SetStatusMessage($"Sold item for ${item.SellPrice}!");
                Destroy(dragItem.gameObject);
                return;
            }

            // 3. Handle Moving from Cart to Inventory
            if (sourceZone == ZoneType.Cart && targetZone == ZoneType.Inventory)
            {
                if (GamePhaseManager.Instance?.Data?.CartItemsData != null)
                {
                    GamePhaseManager.Instance.Data.CartItemsData.Remove(item);
                }
                _currentShoppingPhase.KeepInInventory(item);
                SetStatusMessage("Stored item into Inventory.");
            }

            // 4. Handle Moving from Inventory to Cart
            if (sourceZone == ZoneType.Inventory && targetZone == ZoneType.Cart)
            {
                if (GamePhaseManager.Instance?.Data?.InventoryData != null)
                {
                    GamePhaseManager.Instance.Data.InventoryData.Remove(item);
                }
                if (GamePhaseManager.Instance?.Data?.CartItemsData != null)
                {
                    GamePhaseManager.Instance.Data.CartItemsData.Add(item);
                }
                SetStatusMessage("Moved item from Inventory to Cart.");
            }

            // Move UI element transform to target container
            Transform targetContainer = GetZoneContainer(targetZone);
            if (targetContainer != null)
            {
                dragItem.transform.SetParent(targetContainer, false);
                dragItem.Setup(item, targetZone, dragItem.DisplayName);
            }

            UpdateUIDisplay();
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

        public bool OnClickUseOnPlayer(UIDragItem dragItem)
        {
            if (_currentShoppingPhase == null || dragItem == null || dragItem.Item == null) return false;

            ItemModel item = dragItem.Item;

            // If consuming directly from Buy zone, check money and deduct BuyPrice
            if (dragItem.CurrentZone == ZoneType.Buy)
            {
                if (item.TryGetCapability<BuyableCapability>(out var buyable) && GamePhaseManager.Instance?.Data != null)
                {
                    if (GamePhaseManager.Instance.Data.Money < buyable.BuyPrice)
                    {
                        SetStatusMessage($"Not enough money! Need ${buyable.BuyPrice}.");
                        return false;
                    }
                    GamePhaseManager.Instance.Data.Money -= (uint)buyable.BuyPrice;
                    SetStatusMessage($"Bought and used item for ${buyable.BuyPrice}!");
                }
            }

            bool success = _currentShoppingPhase.UseOnPlayer(item);
            if (success)
            {
                if (dragItem.CurrentZone == ZoneType.Buy)
                {
                    PopulateBuyZone();
                }
                UpdateUIDisplay();
            }
            return success;
        }

        public bool OnClickUseOnFather(UIDragItem dragItem)
        {
            if (_currentShoppingPhase == null || dragItem == null || dragItem.Item == null) return false;

            ItemModel item = dragItem.Item;

            // If consuming directly from Buy zone, check money and deduct BuyPrice
            if (dragItem.CurrentZone == ZoneType.Buy)
            {
                if (item.TryGetCapability<BuyableCapability>(out var buyable) && GamePhaseManager.Instance?.Data != null)
                {
                    if (GamePhaseManager.Instance.Data.Money < buyable.BuyPrice)
                    {
                        SetStatusMessage($"Not enough money! Need ${buyable.BuyPrice}.");
                        return false;
                    }
                    GamePhaseManager.Instance.Data.Money -= (uint)buyable.BuyPrice;
                    SetStatusMessage($"Bought and used item for ${buyable.BuyPrice}!");
                }
            }

            bool success = _currentShoppingPhase.UseOnFather(item);
            if (success)
            {
                if (dragItem.CurrentZone == ZoneType.Buy)
                {
                    PopulateBuyZone();
                }
                UpdateUIDisplay();
            }
            return success;
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
