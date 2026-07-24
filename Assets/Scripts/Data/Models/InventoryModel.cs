using System.Collections.Generic;

namespace TrashCount.Data.Models
{
    [System.Serializable]
    public class InventoryModel
    {
        [UnityEngine.SerializeField] private List<ItemModel> _data = new();
        public uint MaxInventorySize;
        
        public int Count => _data.Count;
        
        public void Remove(ItemModel itemToRemove)
        {
            _data.Remove(itemToRemove);
        }
        
        public void Add(ItemModel model)
        {
            if (IsFull)
            {
                UnityEngine.Debug.Log("Inventory is full");
                return;
            }
            _data.Add(model);
        }
        
        public bool IsFull => _data.Count == MaxInventorySize;
    }
}
