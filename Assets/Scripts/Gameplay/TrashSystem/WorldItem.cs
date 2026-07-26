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
        public ItemData Data => itemData;
        public ItemModel Model => itemData != null ? itemData[itemState] : null;
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
            return $"กด E เพื่อยก {itemState}";
        }

        public bool CanInteract(GameObject interactor)
        {
            return !IsCarried && itemState != ItemState.None;
        }

        public void Interact(GameObject interactor)
        {
            if (interactor != null && interactor.TryGetComponent<PlayerInteraction>(out var playerInteraction))
            {
                playerInteraction.PickUpItem(this);
            }
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

        

            transform.position = dropPosition;

            if (_collider != null) _collider.enabled = true;
            if (_rigidbody != null)
            {
                _rigidbody.useGravity = true;
                _rigidbody.isKinematic = false;
            }
        }

        public bool TryConsume(Playstat player)
        {
            if (player == null) return false;

            if (itemData != null)
            {
                ItemModel model = itemData[itemState];
                if (model != null && model.TryGetCapability<EatableCapability>(out var eatable))
                {
                    player.EatFood(eatable.RestoreAmount, eatable.HealthAmount);
                    Debug.Log($"[WorldItem] Player ate {itemState} (Restored Hunger: {eatable.RestoreAmount}, Health: {eatable.HealthAmount}).");
                    Destroy(gameObject);
                    return true;
                }
            }
            else
            {
                Debug.LogWarning($"[WorldItem] Cannot consume {itemState} because ItemData reference is missing on {gameObject.name}.");
            }
            return false;
        }
    }
}
