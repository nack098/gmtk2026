using UnityEngine;
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
        
        public InventoryModel InventoryData = new();
    }
}