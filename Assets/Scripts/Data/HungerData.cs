using UnityEngine;
using System.Collections.Generic;
using TrashCount.Data.Generators;

namespace TrashCount.Data
{
    [CreateAssetMenu(fileName="TrashCount", menuName="TrashCountObject/HungerData")]
    public partial class HungerData : ScriptableObject, IEnumGeneratable
    {
        [SerializeField] public Dictionary<string, float> DrainValue = new();

        public void GenerateEnum()
        {
            EnumGenerator.GenerateFromDictionary("HungerState", nameof(HungerData), nameof(DrainValue), DrainValue);
        }
    }
}
