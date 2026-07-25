using System.Collections.Generic;
using UnityEngine;
using TrashCount.Gameplay.Abstracts;

namespace TrashCount.Gameplay.TrashSystem
{
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private Collider interactionTrigger;
        [SerializeField] private LayerMask interactableMask = ~0; // Default to all layers
        [SerializeField] private KeyCode interactKey ;
        [SerializeField] private KeyCode consumeKey ;

        [Header("Carrying Socket Settings")]
        [SerializeField] private Transform holdSocket;
        public Transform CartSocket;
        public IInteractable CurrentInteractable { get; private set; }
        public WorldItem CarriedItem { get; private set; }
        public bool IsCarrying => CarriedItem != null;

        public PushCart CurrentPushedCart { get; private set; }
        public bool IsPushingCart => CurrentPushedCart != null;

        private List<Collider> _candidatesInTrigger = new List<Collider>();
        private Playstat _playStat;

        private void Awake()
        {
            _playStat = GetComponent<Playstat>();

            if (holdSocket == null)
            {
                GameObject socketObj = new GameObject("HoldSocket");
                socketObj.transform.SetParent(transform);
                socketObj.transform.localPosition = new Vector3(0f, 1.2f, 0.8f);
                holdSocket = socketObj.transform;
            }
        }

        public void SetPushedCart(PushCart cart)
        {
            CurrentPushedCart = cart;
            if (_playStat != null)
            {
                _playStat.IsPushingCart = CurrentPushedCart != null;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || other.gameObject == gameObject) return;
            if (((1 << other.gameObject.layer) & interactableMask) == 0) return;

            // Check for IInteractable (supports WorldItem, PushCartHandle, TrashContainer)
            if (other.TryGetComponent<IInteractable>(out var interactable) ||
                other.GetComponentInParent<IInteractable>() is IInteractable parentInteractable)
            {
                if (!_candidatesInTrigger.Contains(other))
                {
                    _candidatesInTrigger.Add(other);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other != null && _candidatesInTrigger.Contains(other))
            {
                _candidatesInTrigger.Remove(other);
            }
        }

        private void Update()
        {
            // Force release/drop if stamina reaches 0
            if (_playStat != null && _playStat.CurrentStamina <= 0f)
            {
                if (IsPushingCart)
                {
                    Debug.Log("[PlayerInteraction] Out of stamina! Force releasing cart.");
                    ReleaseCart();
                    return;
                }
                else if (IsCarrying)
                {
                    Debug.Log("[PlayerInteraction] Out of stamina! Force dropping item.");
                    DropCarriedItem();
                    return;
                }
            }

            if (IsPushingCart)
            {
                HandlePushingCartInput();
            }
            else if (IsCarrying)
            {
                HandleCarryingInput();
            }
            else
            {
                DetectInteractable();
                HandleInput();
            }
        }

        private void DetectInteractable()
        {
            IInteractable closestInteractable = null;
            float minDistance = float.MaxValue;

            for (int i = _candidatesInTrigger.Count - 1; i >= 0; i--)
            {
                var col = _candidatesInTrigger[i];
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy)
                {
                    _candidatesInTrigger.RemoveAt(i);
                    continue;
                }

                if (col.TryGetComponent<IInteractable>(out var interactable) ||
                    col.GetComponentInParent<IInteractable>() is IInteractable parentInteractable && (interactable = parentInteractable) != null)
                {
                    if (interactable.CanInteract(gameObject))
                    {
                        float dist = Vector3.Distance(transform.position, col.transform.position);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            closestInteractable = interactable;
                        }
                    }
                }
            }

            CurrentInteractable = closestInteractable;
        }

        private void HandleInput()
        {
            if (CurrentInteractable != null && IsInteractKeyPressed())
            {
                CurrentInteractable.Interact(gameObject);
            }
        }

        private void HandleCarryingInput()
        {
            if (IsInteractKeyPressed())
            {
                DropCarriedItem();
            }
            else if (IsConsumeKeyPressed())
            {
                ConsumeCarriedItem();
            }
        }

        private void HandlePushingCartInput()
        {
            if (IsInteractKeyPressed())
            {
                ReleaseCart();
            }
        }

        public void ReleaseCart()
        {
            if (CurrentPushedCart != null)
            {
                CurrentPushedCart.StopPushing();
                SetPushedCart(null);
            }
        }

        public void PickUpItem(WorldItem worldItem)
        {
            if (worldItem == null) return;

            CarriedItem = worldItem;
            CarriedItem.PickUpToSocket(holdSocket);

            if (_playStat != null)
            {
                _playStat.IsCarryingItem = true;
            }

            Debug.Log($"[PlayerInteraction] Picked up {worldItem.State}");
        }

        public void DropCarriedItem()
        {
            if (CarriedItem == null) return;

            Vector3 dropPos = transform.position + transform.forward * 0.8f;
            dropPos.y = holdSocket != null ? holdSocket.position.y : transform.position.y + 1.2f;

            CarriedItem.DropToGround(dropPos);

            CarriedItem = null;
            if (_playStat != null)
            {
                _playStat.IsCarryingItem = false;
            }
        }

        public void ConsumeCarriedItem()
        {
            if (CarriedItem == null) return;

            if (_playStat != null && CarriedItem.TryConsume(_playStat))
            {
                CarriedItem = null;
                if (_playStat != null)
                {
                    _playStat.IsCarryingItem = false;
                }
            }
            else
            {
                Debug.Log($"[PlayerInteraction] Cannot consume {CarriedItem.State} (Not eatable).");
            }
        }

        private bool IsInteractKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                return true;
            }
            return false;
#else
            return Input.GetKeyDown(interactKey);
#endif
        }

        private bool IsConsumeKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
            {
                return true;
            }
            return false;
#else
            return Input.GetKeyDown(consumeKey);
#endif
        }

        // New Input System Action Message Receivers
        public void OnInteract()
        {
            if (IsPushingCart)
            {
                ReleaseCart();
            }
            else if (IsCarrying)
            {
                DropCarriedItem();
            }
            else if (CurrentInteractable != null)
            {
                CurrentInteractable.Interact(gameObject);
            }
        }

        public void OnConsume()
        {
            if (IsCarrying)
            {
                ConsumeCarriedItem();
            }
        }
    }
}
