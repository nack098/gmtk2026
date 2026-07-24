using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using TrashCount.Data.Models;

namespace TrashCount.Gameplay.Phases.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [Header("UI Element References")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI itemTitleText;
        [SerializeField] private TextMeshProUGUI priceText;

        public ItemModel Item { get; private set; }
        public ZoneType CurrentZone { get; private set; }

        private CanvasGroup _canvasGroup;
        private Canvas _parentCanvas;
        private Transform _originalParent;
        private Vector3 _originalPosition;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _parentCanvas = GetComponentInParent<Canvas>();
        }

        public void Setup(ItemModel itemModel, ZoneType currentZone)
        {
            Item = itemModel;
            CurrentZone = currentZone;

            if (itemTitleText != null && itemModel != null)
            {
                itemTitleText.text = itemModel.ToString();
            }

            if (priceText != null && itemModel != null)
            {
                if (currentZone == ZoneType.Buy && itemModel.TryGetCapability<BuyableCapability>(out var buyable))
                {
                    priceText.text = $"${buyable.BuyPrice}";
                }
                else
                {
                    priceText.text = $"${itemModel.SellPrice}";
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _originalParent = transform.parent;
            _originalPosition = transform.position;

            // Bring to top of Canvas during drag
            if (_parentCanvas != null)
            {
                transform.SetParent(_parentCanvas.transform, true);
            }

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0.6f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1.0f;

            // If not dropped into a valid drop zone, return to original parent & position
            if (transform.parent == _parentCanvas.transform)
            {
                transform.SetParent(_originalParent, true);
                transform.position = _originalPosition;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Right click context menu for eating
                if (UIItemContextMenu.Instance != null && Item != null)
                {
                    UIItemContextMenu.Instance.ShowContextMenu(this, eventData.position);
                }
            }
        }
    }
}
