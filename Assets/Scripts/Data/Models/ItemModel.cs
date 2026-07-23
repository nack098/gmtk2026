using UnityEngine;
using System;
using System.Collections.Generic;

namespace TrashCount.Data.Models
{
    [Serializable]
    public class ItemModel
    {
        public int SellPrice;
        
        [SerializeReference] 
        public List<IItemCapability> capabilities = new();

        public bool TryGetCapability<T>(out T capability) where T : class, IItemCapability
        {
            for (int i = 0; i < capabilities.Count; i++)
            {
                if (capabilities[i] is T match)
                {
                    capability = match;
                    return true;
                }
            }
            capability = null;
            return false;
        }

        public bool HasCapability<T>() where T : class, IItemCapability
        {
            return TryGetCapability<T>(out _);
        }
    }

    public interface IItemCapability { }

    [Serializable]
    public class BuyableCapability : IItemCapability
    {
        public int BuyPrice;
    }

    [Serializable]
    public class EatableCapability : IItemCapability
    {
        public int RestoreAmount;
    }
}