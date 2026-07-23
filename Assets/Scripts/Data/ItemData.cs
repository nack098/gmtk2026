using UnityEngine;
using System.Collections.Generic;
using TrashCount.Data.Models;
using TrashCount.Data.Generators;

namespace TrashCount.Data
{
    [CreateAssetMenu(fileName="TrashCount", menuName="TrashCountObject/ItemData")]
    public partial class ItemData: ScriptableObject, IEnumGeneratable
    {
        [SerializeField] public Dictionary<string, ItemModel> Items = new();
        
        public void GenerateEnum()
        {
            EnumGenerator.GenerateFromDictionary("ItemState", nameof(ItemData), nameof(Items), Items);
        }
    }
}