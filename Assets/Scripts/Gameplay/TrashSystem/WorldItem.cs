using UnityEngine;
using TrashCount.Data;
using TrashCount.Data.Models;
using TrashCount.Gameplay.Abstracts;

namespace TrashCount.Gameplay.TrashSystem
{
    public class WorldItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemState itemState = ItemState.None;
        [SerializeField] private ItemData itemData;

        public ItemState State => itemState;
        public bool IsCarried { get; private set; }

        private Collider _collider;
        private Rigidbody _rigidbody;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        public void Initialize(ItemState state, ItemData data)
        {
            itemState = state;
            itemData = data;
        }

        public string GetInteractPrompt()
        {
            if (IsCarried)
            {
                return $"กด E เพื่อวาง {itemState} | กด F เพื่อกิน";
            }
            return $"กด E เพื่ออุ้ม/ยก {itemState}";
        }

        public bool CanInteract(GameObject interactor)
        {
            return itemState != ItemState.None;
        }

        public void Interact(GameObject interactor)
        {
            // Handled via PlayerInteraction carrying system
        }

        public void PickUpToSocket(Transform holdSocket)
        {
            IsCarried = true;
            transform.SetParent(holdSocket);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            if (_collider != null) _collider.enabled = false;
            if (_rigidbody != null) _rigidbody.isKinematic = true;
        }

        public void DropToGround(Vector3 dropPosition)
        {
            IsCarried = false;
            transform.SetParent(null);

            // Raycast down to find ground surface safely
            if (Physics.Raycast(dropPosition + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3.0f))
            {
                dropPosition.y = hit.point.y + 0.15f; // Place slightly above hit ground point
            }

            transform.position = dropPosition;

            if (_collider != null) _collider.enabled = true;
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }
        }

        public bool TryConsume(Playstat player)
        {
            if (itemData != null)
            {
                ItemModel model = itemData[itemState];
                if (model != null && model.TryGetCapability<EatableCapability>(out var eatable))
                {
                    player.EatFood(eatable.RestoreAmount);
                    Debug.Log($"[WorldItem] Player ate {itemState} and restored {eatable.RestoreAmount} hunger.");
                    Destroy(gameObject);
                    return true;
                }
            }
            return false;
        }
    }
}
