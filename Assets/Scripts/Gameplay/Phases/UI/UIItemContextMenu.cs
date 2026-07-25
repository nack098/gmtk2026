using UnityEngine;
using UnityEngine.UI;
using TrashCount.Data.Models;

namespace TrashCount.Gameplay.Phases.UI
{
    public class UIItemContextMenu : MonoBehaviour
    {
        public static UIItemContextMenu Instance { get; private set; }

        [Header("Menu References")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button eatPlayerButton;
        [SerializeField] private Button eatFatherButton;

        private UIDragItem _targetDragItem;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (menuPanel != null) menuPanel.SetActive(false);
        }

        public void ShowContextMenu(UIDragItem dragItem, Vector2 screenPosition)
        {
            if (dragItem == null || dragItem.Item == null) return;

            // Only show context menu if item is eatable
            if (!dragItem.Item.HasCapability<EatableCapability>())
            {
                Debug.Log("[UIItemContextMenu] Item is not eatable.");
                HideContextMenu();
                return;
            }

            _targetDragItem = dragItem;

            if (menuPanel != null)
            {
                menuPanel.transform.position = screenPosition;
                menuPanel.SetActive(true);
            }
        }

        public void HideContextMenu()
        {
            _targetDragItem = null;
            if (menuPanel != null)
            {
                menuPanel.SetActive(false);
            }
        }

        public void OnClickEatPlayer()
        {
            if (_targetDragItem != null && ShoppingPhaseUI.Instance != null)
            {
                bool success = ShoppingPhaseUI.Instance.OnClickUseOnPlayer(_targetDragItem);
                if (success && _targetDragItem.CurrentZone != ZoneType.Buy)
                {
                    RemoveItemFromDataCollections(_targetDragItem);
                    Destroy(_targetDragItem.gameObject);
                }
            }
            HideContextMenu();
        }

        public void OnClickEatFather()
        {
            if (_targetDragItem != null && ShoppingPhaseUI.Instance != null)
            {
                bool success = ShoppingPhaseUI.Instance.OnClickUseOnFather(_targetDragItem);
                if (success && _targetDragItem.CurrentZone != ZoneType.Buy)
                {
                    RemoveItemFromDataCollections(_targetDragItem);
                    Destroy(_targetDragItem.gameObject);
                }
            }
            HideContextMenu();
        }

        private void RemoveItemFromDataCollections(UIDragItem dragItem)
        {
            if (dragItem == null || dragItem.Item == null || GamePhaseManager.Instance == null || GamePhaseManager.Instance.Data == null) return;

            var data = GamePhaseManager.Instance.Data;
            if (dragItem.CurrentZone == ZoneType.Inventory && data.InventoryData != null)
            {
                data.InventoryData.Remove(dragItem.Item);
            }
            else if (dragItem.CurrentZone == ZoneType.Cart && data.CartItemsData != null)
            {
                data.CartItemsData.Remove(dragItem.Item);
            }
        }
    }
}
