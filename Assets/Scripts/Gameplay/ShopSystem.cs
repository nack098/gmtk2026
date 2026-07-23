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

        public ShopItemEntry(ItemState state, ItemModel item, int buyPrice)
        {
            State = state;
            Item = item;
            BuyPrice = buyPrice;
        }
    }

    public class ShopSystem : MonoBehaviour
    {
        [SerializeField] private ItemData _items;
        [SerializeField] private GameData _gameData;
        
        private List<ShopItemEntry> _shopCatalog = new();
        private Dictionary<ItemState, ShopItemEntry> _shopCatalogLookup = new();

        public IReadOnlyList<ShopItemEntry> ShopCatalog => _shopCatalog;

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

            _shopCatalog = _items.Items
                .Where(kvp => kvp.Value != null 
                           && kvp.Value.HasCapability<BuyableCapability>() 
                           && Enum.TryParse<ItemState>(kvp.Key, true, out var state) 
                           && state != ItemState.None)
                .Select(kvp => 
                {
                    Enum.TryParse<ItemState>(kvp.Key, true, out var state);
                    kvp.Value.TryGetCapability<BuyableCapability>(out var buyable);
                    return new ShopItemEntry(state, kvp.Value, buyable.BuyPrice);
                })
                .ToList();

            _shopCatalogLookup = _shopCatalog.ToDictionary(entry => entry.State, entry => entry);

            Debug.Log($"[ShopSystem] Successfully cached {_shopCatalog.Count} items into the strongly-typed shop catalog.");
        }

        public bool TryGetShopEntry(ItemState state, out ShopItemEntry entry)
        {
            return _shopCatalogLookup.TryGetValue(state, out entry);
        }

        public bool TryBuyItem(ItemState state)
        {
            if (!TryGetShopEntry(state, out var shopEntry))
            {
                Debug.LogWarning($"[ShopSystem] Item '{state}' is not for sale in this shop!");
                return false;
            }

            long currentMoney = _gameData.Money;
            long price = shopEntry.BuyPrice;

            if (currentMoney < price)
            {
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
                Debug.Log($"[ShopSystem] Purchased {state} for {price} gold.");
            }
            
            return true;
        }
    }
}