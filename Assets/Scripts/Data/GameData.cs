using UnityEngine;
using System.Collections.Generic;
using TrashCount.Data.Models;

namespace TrashCount.Data
{
    [CreateAssetMenu(fileName="TrashCount", menuName="TrashCountObject/GameData")]
    public class GameData : ScriptableObject
    {
        public uint Money;
        public uint MaxInventorySize;
        public ClockModel DateTime = new();
        public CharacterModel PlayerData = new();
        public CharacterModel FatherData = new();
        
        public List<ItemModel> Inventory;
        
        public void AppendToInventory(ItemModel model)
        {
            if (IsFull)
            {
                Debug.Log("Inventory is full");
                return;
            }
            Inventory.Add(model);
        }
        
        public bool IsFull => Inventory.Count == MaxInventorySize;
    }
}