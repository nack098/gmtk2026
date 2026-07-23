using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TrashCount.Data;
using TrashCount.Data.Models;

namespace TrashCount.Gameplay
{
    public readonly struct ShopItemEntry
    {
        public readonly ItemState State;
        public readonly ItemModel Item;
        public readonly int BuyPrice;
        public readonly int SellPrice;
        public readonly bool IsBuyable;

        public ShopItemEntry(ItemState state, ItemModel item, int buyPrice, bool isBuyable)
        {
            State = state;
            Item = item;
            BuyPrice = buyPrice;
            SellPrice = item != null ? item.SellPrice : 0;
            IsBuyable = isBuyable;
        }

        public bool TryGetCapability<T>(out T capability) where T : class, IItemCapability
        {
            if (Item != null)
            {
                return Item.TryGetCapability(out capability);
            }
            capability = null;
            return false;
        }
    }

    public class ShopSystem : MonoBehaviour
    {
        [SerializeField] private ItemData _items;
        [SerializeField] private GameData _gameData;

        private List<ShopItemEntry> _masterCatalog = new();
        private List<ShopItemEntry> _buyCatalog = new();
        private Dictionary<ItemState, ShopItemEntry> _catalogLookup = new();
        
        public event Action<ItemState, int> OnItemPurchased;        
        public event Action<ItemState, int, long> OnItemSold;
        public event Action OnTransactionFailed;

        public IReadOnlyList<ShopItemEntry> MasterCatalog => _masterCatalog;
        public IReadOnlyList<ShopItemEntry> BuyCatalog => _buyCatalog;

        private void Awake()
        {
            if (_items == null)
            {
                Debug.LogWarning("[ShopSystem] ItemData reference is missing!", this);
                return;
            }

            if (_gameData == null)
            {
                Debug.LogWarning("[ShopSystem] GameData reference is missing!", this);
                return;
            }

            InitializeCatalogs();
        }

        private void InitializeCatalogs()
        {
            _masterCatalog.Clear();
            _buyCatalog.Clear();
            _catalogLookup.Clear();

            foreach (var kvp in _items.Items)
            {
                if (kvp.Value == null) continue;
                if (!Enum.TryParse<ItemState>(kvp.Key, true, out var state) || state == ItemState.None) continue;

                ItemModel item = kvp.Value;
                bool isBuyable = item.TryGetCapability<BuyableCapability>(out var buyable);
                int buyPrice = isBuyable ? buyable.BuyPrice : 0;
                var entry = new ShopItemEntry(state, item, buyPrice, isBuyable);

                _masterCatalog.Add(entry);
                _catalogLookup[state] = entry;

                if (isBuyable)
                {
                    _buyCatalog.Add(entry);
                }
            }

            Debug.Log($"[ShopSystem] Initialized {_masterCatalog.Count} items ({_buyCatalog.Count} buyable, ALL sellable).");
        }

        public bool TryGetShopEntry(ItemState state, out ShopItemEntry entry)
        {
            return _catalogLookup.TryGetValue(state, out entry);
        }

        public bool TryBuyItem(ItemState state)
        {
            if (!TryGetShopEntry(state, out var entry))
            {
                OnTransactionFailed?.Invoke();
                Debug.LogWarning($"[ShopSystem] Item '{state}' was not found in database!");
                return false;
            }

            if (!entry.IsBuyable)
            {
                OnTransactionFailed?.Invoke();
                Debug.LogWarning($"[ShopSystem] Item '{state}' is not for sale!");
                return false;
            }

            long currentMoney = _gameData.Money;
            long price = entry.BuyPrice;

            if (currentMoney < price)
            {
                OnTransactionFailed?.Invoke();
                Debug.LogWarning("[ShopSystem] Insufficient Funds!");
                return false;
            }

            long resultantMoney = currentMoney - price;
            _gameData.Money = (uint)Math.Max(0, resultantMoney);

            if (price < 0)
            {
                Debug.Log($"[ShopSystem] You were PAID {-price} gold to haul away {state}!");
            }
            else
            {
                Debug.Log($"[ShopSystem] Purchased {state} for {price} gold. Remaining: {_gameData.Money}");
            }
            
            OnItemPurchased?.Invoke(state, entry.BuyPrice);
            
            return true;
        }

        public bool TrySellItem(ItemState state, int quantity = 1)
        {
            if (quantity <= 0)
            {
                OnTransactionFailed?.Invoke();
                Debug.LogWarning("[ShopSystem] Quantity must be greater than zero to sell!");
                return false;
            }

            if (!TryGetShopEntry(state, out var entry))
            {
                OnTransactionFailed?.Invoke();
                Debug.LogWarning($"[ShopSystem] Item '{state}' was not found in database!");
                return false;
            }

            long sellRevenue = (long)entry.SellPrice * quantity;
            long resultantMoney = (long)_gameData.Money + sellRevenue;

            _gameData.Money = (uint)Math.Clamp(resultantMoney, 0, uint.MaxValue);
            
            Debug.Log($"[ShopSystem] Sold {quantity}x {state} for {sellRevenue} gold (Unit: {entry.SellPrice}). Money: {_gameData.Money}");
            OnItemSold?.Invoke(state, quantity, sellRevenue);
            return true;
        }
    }
}