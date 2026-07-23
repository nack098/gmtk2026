using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TrashCount.Data;
using TrashCount.Data.Models;
using TrashCount.Gameplay.Distributions;

namespace TrashCount.Gameplay
{
    public readonly struct DropItemEntry
    {
        public readonly ItemState State;
        public readonly ItemModel Item;
        public readonly uint DropWeight;

        public DropItemEntry(ItemState state, ItemModel item, uint dropWeight)
        {
            State = state;
            Item = item;
            DropWeight = dropWeight;
        }
    }
    
    public class RandomDropSystem : MonoBehaviour
    {
        [SerializeField] private ItemData _items;

        [Header("Drop Strategy Capability")]
        [SerializeReference] 
        private IDropDistribution _distribution = new SigmoidGaussianDistribution();

        private List<DropItemEntry> _dropCatalog = new();
        private uint _totalWeight;

        public IReadOnlyList<DropItemEntry> DropCatalog => _dropCatalog;
        public uint TotalWeight => _totalWeight;

        private void Awake()
        {
            if (_items == null)
            {
                Debug.LogWarning("[RandomDropSystem] ItemData reference is missing!", this);
                return;
            }

            _dropCatalog = _items.Items
                .Where(kvp => kvp.Value != null 
                           && kvp.Value.HasCapability<DropableCapability>()
                           && Enum.TryParse<ItemState>(kvp.Key, true, out var state)
                           && state != ItemState.None)
                .Select(kvp =>
                {
                    Enum.TryParse<ItemState>(kvp.Key, true, out var state);
                    kvp.Value.TryGetCapability<DropableCapability>(out var dropable);
                    return new DropItemEntry(state, kvp.Value, dropable.DropChance);
                })
                .Where(entry => entry.DropWeight > 0)
                .ToList();

            _totalWeight = (uint)_dropCatalog.Sum(entry => (long)entry.DropWeight);
        }

        public bool TryGetRandomDrop(out ItemState dropState, out ItemModel droppedItem)
        {
            float sample = _distribution != null ? _distribution.Sample() : Takayama.Math.Random.Value();
            return EvaluateDropFromNormalizedSample(sample, out dropState, out droppedItem);
        }

        public bool TryGetRandomDrop(Func<float> customRandomFunc, out ItemState dropState, out ItemModel droppedItem)
        {
            float sampledNormalized = customRandomFunc != null 
                ? customRandomFunc.Invoke() 
                : (_distribution != null ? _distribution.Sample() : Takayama.Math.Random.Value());

            return EvaluateDropFromNormalizedSample(sampledNormalized, out dropState, out droppedItem);
        }

        private bool EvaluateDropFromNormalizedSample(float sampledNormalized, out ItemState dropState, out ItemModel droppedItem)
        {
            dropState = ItemState.None;
            droppedItem = null;

            if (_dropCatalog.Count == 0 || _totalWeight == 0) return false;

            sampledNormalized = Mathf.Clamp01(sampledNormalized);
            float targetWeight = sampledNormalized * _totalWeight;

            uint accumulatedWeight = 0;
            for (int i = 0; i < _dropCatalog.Count; i++)
            {
                accumulatedWeight += _dropCatalog[i].DropWeight;
                if (targetWeight <= accumulatedWeight)
                {
                    dropState = _dropCatalog[i].State;
                    droppedItem = _dropCatalog[i].Item;
                    return true;
                }
            }

            var fallback = _dropCatalog[_dropCatalog.Count - 1];
            dropState = fallback.State;
            droppedItem = fallback.Item;
            return true;
        }
    }
}