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
                ShoppingPhaseUI.Instance.OnClickUseOnPlayer(_targetDragItem.Item);
            }
            HideContextMenu();
        }

        public void OnClickEatFather()
        {
            if (_targetDragItem != null && ShoppingPhaseUI.Instance != null)
            {
                ShoppingPhaseUI.Instance.OnClickUseOnFather(_targetDragItem.Item);
            }
            HideContextMenu();
        }
    }
}
