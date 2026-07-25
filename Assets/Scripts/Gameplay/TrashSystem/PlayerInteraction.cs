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
        private StarterAssets.StarterAssetsInputs _inputs;

        private void Awake()
        {
            _playStat = GetComponent<Playstat>();
            _inputs = GetComponent<StarterAssets.StarterAssetsInputs>();

            if (holdSocket == null)
            {
                GameObject socketObj = new GameObject("HoldSocket");
                socketObj.transform.SetParent(transform);
                socketObj.transform.localPosition = new Vector3(0f, 1.2f, 0.8f);
                holdSocket = socketObj.transform;
            }

            if (CartSocket == null)
            {
                GameObject socketObj = new GameObject("CartSocket");
                socketObj.transform.SetParent(transform);
                socketObj.transform.localPosition = new Vector3(0f, 0.8f, 1.2f);
                socketObj.transform.localRotation = Quaternion.identity;
                CartSocket = socketObj.transform;
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

            // Always update interactable detection
            DetectInteractable();

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
                HandleInput();
            }
        }

        private float _lastDropTime;

        private void DetectInteractable()
        {
            IInteractable closestInteractable = null;
            float minDistance = float.MaxValue;

            // Dynamic Physics OverlapSphere check (Guarantees detection of dropped or newly enabled items)
            Vector3 searchCenter = transform.position + transform.forward * 0.5f + Vector3.up * 0.8f;
            Collider[] overlapColliders = Physics.OverlapSphere(searchCenter, 2.5f, interactableMask, QueryTriggerInteraction.Collide);

            List<Collider> allCandidates = new List<Collider>(_candidatesInTrigger);
            foreach (var col in overlapColliders)
            {
                if (col != null && col.gameObject != gameObject && !allCandidates.Contains(col))
                {
                    allCandidates.Add(col);
                }
            }

            for (int i = allCandidates.Count - 1; i >= 0; i--)
            {
                var col = allCandidates[i];
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

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
            if (Time.time < _lastDropTime + 0.2f) return;

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

            WorldItem itemToDrop = CarriedItem;
            CarriedItem = null;
            _lastDropTime = Time.time;

            itemToDrop.DropToGround(dropPos);

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
            if (_inputs != null && _inputs.interact)
            {
                _inputs.interact = false;
                return true;
            }
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return false;
        }

        private bool IsConsumeKeyPressed()
        {
            if (_inputs != null && _inputs.consume)
            {
                _inputs.consume = false;
                return true;
            }
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return false;
        }

        // New Input System Action Message Receivers
#if ENABLE_INPUT_SYSTEM
        public void OnInteract(UnityEngine.InputSystem.InputValue value)
        {
            if (_inputs != null) _inputs.interact = value.isPressed;
        }

        public void OnConsume(UnityEngine.InputSystem.InputValue value)
        {
            if (_inputs != null) _inputs.consume = value.isPressed;
        }
#else
        public void OnInteract()
        {
            if (_inputs != null) _inputs.interact = true;
        }

        public void OnConsume()
        {
            if (_inputs != null) _inputs.consume = true;
        }
#endif
    }
}
