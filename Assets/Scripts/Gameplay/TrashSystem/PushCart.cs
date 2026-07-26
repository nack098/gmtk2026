using System;
using System.Collections.Generic;
using UnityEngine;
using TrashCount.Gameplay.TrashSystem;

namespace TrashCount.Gameplay.TrashSystem
{
    public class PushCart : MonoBehaviour
    {
        [Header("Cart References")]
        [SerializeField] private Transform handleSocket;
        [SerializeField] private Transform basketContainer;

        [Header("Weight Settings")]
        [SerializeField] private float itemWeightMultiplier;

        public static PushCart Instance { get; private set; }

        public bool IsBeingPushed { get; private set; }
        public PlayerInteraction CurrentPusher { get; private set; }
        public Playstat CurrentPusherStat { get; private set; }

        private List<WorldItem> _itemsInCart = new();
        public List<WorldItem> ItemsInCart => _itemsInCart;
        public float TotalWeight => _itemsInCart.Count * itemWeightMultiplier;
        public int ItemCount => _itemsInCart.Count;

        public event Action<PushCart> OnPushStarted;
        public event Action<PushCart> OnPushStopped;

        private Rigidbody _rigidbody;
        private Transform _originalParent;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _rigidbody = GetComponent<Rigidbody>();

            if (basketContainer == null)
            {
                basketContainer = transform.Find("BasketContainer");
                if (basketContainer == null)
                {
                    GameObject containerObj = new GameObject("BasketContainer");
                    containerObj.transform.SetParent(transform);
                    containerObj.transform.localPosition = Vector3.zero;
                    containerObj.transform.localRotation = Quaternion.identity;
                    basketContainer = containerObj.transform;
                }
            }
        }

        public void StartPushing(PlayerInteraction player, Playstat stat)
        {
            if (IsBeingPushed || player == null) return;

            IsBeingPushed = true;
            CurrentPusher = player;
            CurrentPusherStat = stat;

            _originalParent = transform.parent;

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }

            // Lock physics on all items currently inside basket to prevent bouncing or falling out
            foreach (var item in _itemsInCart)
            {
                if (item != null && item.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // Parent cart directly to Player's CartSocket for perfect 1:1 synchronization
            if (player.CartSocket != null)
            {
                transform.SetParent(player.CartSocket);

                if (handleSocket != null)
                {
                    // Align handleSocket position to CartSocket (0,0,0) in local space
                    Vector3 localHandleOffset = handleSocket.localPosition;
                    transform.localPosition = -localHandleOffset;
                    transform.localRotation = Quaternion.identity;
                }
                else
                {
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                }
            }
            else
            {
                transform.SetParent(player.transform);
                transform.localPosition = new Vector3(0f, 0f, 1.2f);
                transform.localRotation = Quaternion.identity;
            }

            player.SetPushedCart(this);

            if (stat != null)
            {
                stat.IsPushingCart = true;
                stat.CartWeight = TotalWeight;
            }

            OnPushStarted?.Invoke(this);
            Debug.Log($"[PushCart] Player started pushing cart: {gameObject.name} (Items locked inside basket)");
        }

        private float GetGroundSurfaceY(Vector3 worldPos, float defaultY)
        {
            Vector3 rayOrigin = new Vector3(worldPos.x, worldPos.y + 2.0f, worldPos.z);
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 15.0f, ~0, QueryTriggerInteraction.Ignore);

            float highestGroundY = float.MinValue;
            bool foundGround = false;

            foreach (var hit in hits)
            {
                // Skip hits on the Cart itself, its children, or the Player
                if (hit.collider == null) continue;
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
                if (CurrentPusher != null && (hit.collider.transform == CurrentPusher.transform || hit.collider.transform.IsChildOf(CurrentPusher.transform))) continue;

                if (hit.point.y > highestGroundY)
                {
                    highestGroundY = hit.point.y;
                    foundGround = true;
                }
            }

            return foundGround ? highestGroundY : defaultY;
        }

        public void StopPushing()
        {
            if (!IsBeingPushed) return;

            Vector3 currentWorldPos = transform.position;
            Quaternion currentWorldRot = transform.rotation;

            float socketY = (CurrentPusher != null && CurrentPusher.CartSocket != null) 
                ? CurrentPusher.CartSocket.position.y 
                : currentWorldPos.y;

            // Unparent cart back to original parent while preserving world position/rotation
            transform.SetParent(_originalParent);
            transform.position = currentWorldPos;
            transform.rotation = currentWorldRot;

            // Raycast down (filtering out Cart & Player colliders) to find exact GpuJunkyardColliderPool or Terrain surface Y
            float surfaceY = GetGroundSurfaceY(currentWorldPos, socketY);

            // Rule: Y position can be higher than CartSocket (surfaceY >= socketY), but NOT lower than socketY!
            float finalY = (surfaceY >= socketY) ? surfaceY : socketY;
            transform.position = new Vector3(currentWorldPos.x, finalY, currentWorldPos.z);

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            // Restore physics on items inside basket upon release
            foreach (var item in _itemsInCart)
            {
                if (item != null && item.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
            }

            if (CurrentPusherStat != null)
            {
                CurrentPusherStat.IsPushingCart = false;
                CurrentPusherStat.CartWeight = 0f;
            }

            if (CurrentPusher != null)
            {
                CurrentPusher.SetPushedCart(null);
            }

            IsBeingPushed = false;
            CurrentPusher = null;
            CurrentPusherStat = null;

            OnPushStopped?.Invoke(this);
            Debug.Log($"[PushCart] Player stopped pushing cart: {gameObject.name} (Aligned Y to {finalY})");
        }

        private void Update()
        {
            // Safety Check: Auto-teleport cart back onto terrain if it falls below world ground (-5m)
            if (transform.position.y < -5f)
            {
                Vector3 recoverPos = transform.position;
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    recoverPos = player.transform.position + player.transform.forward * 1.5f;
                }
                else if (Camera.main != null)
                {
                    recoverPos = Camera.main.transform.position + Camera.main.transform.forward * 2f;
                }

                float surfaceY = GetGroundSurfaceY(recoverPos, recoverPos.y);
                recoverPos.y = surfaceY + 0.1f;

                transform.position = recoverPos;
                if (_rigidbody != null)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                Debug.LogWarning($"[PushCart] Cart fell below world! Teleported back to safety at {recoverPos}");
            }

            if (IsBeingPushed && CurrentPusher != null)
            {
                // Update weight on Playstat in case item count changes while pushing
                if (CurrentPusherStat != null)
                {
                    CurrentPusherStat.CartWeight = TotalWeight;

                    // Force release if player runs out of Stamina
                    if (CurrentPusherStat.CurrentStamina <= 0f)
                    {
                        Debug.Log("[PushCart] Player out of stamina! Force releasing cart.");
                        StopPushing();
                        return;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryAddItemToCart(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryAddItemToCart(other);
        }

        private void TryAddItemToCart(Collider other)
        {
            if (other == null) return;
            // Rule: Do NOT collect roadside items by collision while pushing!
            if (IsBeingPushed) return;

            if (other.TryGetComponent<WorldItem>(out var worldItem) ||
                other.GetComponentInParent<WorldItem>() is WorldItem parentItem && (worldItem = parentItem) != null)
            {
                if (!worldItem.IsCarried && !_itemsInCart.Contains(worldItem))
                {
                    _itemsInCart.Add(worldItem);
                    worldItem.transform.SetParent(basketContainer);
                    Debug.Log($"[PushCart] Item added to cart: {worldItem.State}. Total items: {_itemsInCart.Count}");
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null) return;
            if (IsBeingPushed) return; // Ignore exit triggers while pushing to lock items inside

            if (other.TryGetComponent<WorldItem>(out var worldItem) ||
                other.GetComponentInParent<WorldItem>() is WorldItem parentItem && (worldItem = parentItem) != null)
            {
                if (_itemsInCart.Contains(worldItem))
                {
                    _itemsInCart.Remove(worldItem);
                    if (!worldItem.IsCarried && worldItem.transform.parent == basketContainer)
                    {
                        worldItem.transform.SetParent(null);
                    }
                    Debug.Log($"[PushCart] Item removed from cart: {worldItem.State}. Total items: {_itemsInCart.Count}");
                }
            }
        }
    }
}
