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
            #if UNITY_EDITOR
            EnumGenerator.GenerateFromDictionary("HungerState", nameof(HungerData), nameof(Values), Values);
            #endif
        }
    }
}
