using System;
using System.Collections.Generic;
using UnityEngine;
using TrashCount.Data;

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

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _rigidbody = GetComponent<Rigidbody>();
        }

        public void StartPushing(PlayerInteraction player, Playstat stat)
        {
            if (IsBeingPushed) return;

            IsBeingPushed = true;
            CurrentPusher = player;
            CurrentPusherStat = stat;

            if (player != null)
            {
                player.SetPushedCart(this);
            }

            if (stat != null)
            {
                stat.IsPushingCart = true;
                stat.CartWeight = TotalWeight;
            }

            OnPushStarted?.Invoke(this);
            Debug.Log($"[PushCart] Player started pushing cart: {gameObject.name}");
        }

        public void StopPushing()
        {
            if (!IsBeingPushed) return;

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
            Debug.Log($"[PushCart] Player stopped pushing cart: {gameObject.name}");
        }

        private void Update()
        {
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

                // Position cart in front of player
                Vector3 targetPosition = CurrentPusher.transform.position + CurrentPusher.transform.forward * 1.5f;
                targetPosition.y = transform.position.y; // Keep ground height

                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
                transform.rotation = Quaternion.Slerp(transform.rotation, CurrentPusher.transform.rotation, Time.deltaTime * 10f);
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
