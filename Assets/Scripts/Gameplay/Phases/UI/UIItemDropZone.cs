using UnityEngine;
using UnityEngine.EventSystems;

namespace TrashCount.Gameplay.Phases.UI
{
    public enum ZoneType
    {
        Cart,
        Inventory,
        Buy,
        Sell
    }

    public class UIItemDropZone : MonoBehaviour, IDropHandler
    {
        [Header("Zone Configuration")]
        [SerializeField] private ZoneType zoneType;

        public ZoneType Zone => zoneType;

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag != null && eventData.pointerDrag.TryGetComponent<UIDragItem>(out var dragItem))
            {
                Debug.Log($"[UIItemDropZone] Item {dragItem.Item?.SellPrice} dropped into zone: {zoneType}");
                
                if (ShoppingPhaseUI.Instance != null)
                {
                    ShoppingPhaseUI.Instance.HandleItemDrop(dragItem, zoneType);
                }
            }
        }
    }
}
