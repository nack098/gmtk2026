using UnityEngine;
using System.Collections.Generic;
using TrashCount.Data.Generators;
using TrashCount.Data.Models;

namespace TrashCount.Data
{
    [CreateAssetMenu(fileName="TrashCount", menuName="TrashCountObject/HungerData")]
    public partial class HungerData : ScriptableObject, IEnumGeneratable
    {
        [SerializeField] public Dictionary<string, HungerModel> Values = new();

        public void GenerateEnum()
        {
            EnumGenerator.GenerateFromDictionary("HungerState", nameof(HungerData), nameof(Values), Values);
        }
    }
}
