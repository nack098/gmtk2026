using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrashCount.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class ShopInventoryController : MonoBehaviour
    {
        [Header("Item Template")]
        [SerializeField] private VisualTreeAsset itemRowTemplate;

        // UI References
        private VisualElement _root;
        private ScrollView _shopList;
        private ScrollView _cartList;
        private ScrollView _inventoryList;
        private VisualElement _shopSection;
        private VisualElement _shopDropOverlay;

        // Drag Tracking
        private VisualElement _draggedElement;
        private Vector2 _dragStartPosition;

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;

            // Query Elements
            _shopList = _root.Q<ScrollView>("shop-item-list");
            _cartList = _root.Q<ScrollView>("cart-item-list");
            _inventoryList = _root.Q<ScrollView>("inventory-item-list");
            _shopSection = _root.Q<VisualElement>("section-shop");
            _shopDropOverlay = _root.Q<VisualElement>("shop-drop-overlay");

            PopulateDummyData();
        }

        #region Item Generation
        public void AddItemToContainer(ScrollView container, string itemName, int price, Sprite icon = null)
        {
            if (itemRowTemplate == null)
            {
                Debug.LogError("[ShopInventoryController] Item Row Template (UXML) is missing!");
                return;
            }

            TemplateContainer rowInstance = itemRowTemplate.Instantiate();
            VisualElement row = rowInstance.Q<VisualElement>(className: "item-row");
            if (row == null) return;

            Label nameLabel = row.Q<Label>("item-name");
            Label priceLabel = row.Q<Label>("item-price");
            VisualElement iconElem = row.Q<VisualElement>("item-icon");

            if (nameLabel != null) nameLabel.text = itemName;
            if (priceLabel != null) priceLabel.text = $"{price:N0}$";
            if (icon != null && iconElem != null) iconElem.style.backgroundImage = new StyleBackground(icon);

            RegisterDragEvents(row);
            container.Add(row);
        }

        private void PopulateDummyData()
        {
            if (_shopList == null || _inventoryList == null) return;

            _shopList.Clear();
            _inventoryList.Clear();
            if (_cartList != null) _cartList.Clear();

            for (int i = 0; i < 5; i++)
            {
                AddItemToContainer(_shopList, $"Canned Food #{i + 1}", 150 * (i + 1));
            }

            AddItemToContainer(_inventoryList, "Fresh Apple", 1000);
            AddItemToContainer(_inventoryList, "Bandage", 250);
            AddItemToContainer(_inventoryList, "Scrap Metal", 50);
        }
        #endregion

        #region Drag and Drop & Overlay Logic
        private void RegisterDragEvents(VisualElement item)
        {
            item.RegisterCallback<PointerDownEvent>(OnPointerDown);
            item.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            item.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            _draggedElement = evt.currentTarget as VisualElement;
            if (_draggedElement == null) return;

            _dragStartPosition = evt.position;
            _draggedElement.CapturePointer(evt.pointerId);
            _draggedElement.style.opacity = 0.6f;

            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_draggedElement == null || !_draggedElement.HasPointerCapture(evt.pointerId)) return;

            // Unity 6 safe StyleTranslate assignment
            Vector2 delta = (Vector2)evt.position - _dragStartPosition;
            _draggedElement.style.translate = new StyleTranslate(new Translate(
                new Length(delta.x, LengthUnit.Pixel),
                new Length(delta.y, LengthUnit.Pixel)
            ));

            // Show/Hide overlay when entering shop bounds
            if (_shopSection != null && _shopDropOverlay != null)
            {
                if (_shopSection.worldBound.Contains(evt.position))
                {
                    _shopDropOverlay.RemoveFromClassList("hidden");
                }
                else
                {
                    _shopDropOverlay.AddToClassList("hidden");
                }
            }

            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_draggedElement == null || !_draggedElement.HasPointerCapture(evt.pointerId)) return;

            _draggedElement.ReleasePointer(evt.pointerId);

            // Reset translation & opacity
            _draggedElement.style.translate = new StyleTranslate(StyleKeyword.Null);
            _draggedElement.style.opacity = 1.0f;

            // Hide overlay
            if (_shopDropOverlay != null)
            {
                _shopDropOverlay.AddToClassList("hidden");
            }

            // Execute Sell if inside Shop area
            if (_shopSection != null && _shopSection.worldBound.Contains(evt.position))
            {
                OnItemSold(_draggedElement);
            }

            _draggedElement = null;
            evt.StopPropagation();
        }

        private void OnItemSold(VisualElement item)
        {
            Label nameLabel = item.Q<Label>("item-name");
            Label priceLabel = item.Q<Label>("item-price");

            string itemName = nameLabel != null ? nameLabel.text : "Unknown Item";
            string priceStr = priceLabel != null ? priceLabel.text : "0$";

            Debug.Log($"[SHOP] Sold {itemName} for {priceStr}!");

            item.RemoveFromHierarchy();
        }
        #endregion
    }
}
