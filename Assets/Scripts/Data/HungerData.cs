using System.Collections.Generic;
using UnityEngine;
using TrashCount.Data.Generators;

namespace TrashCount.Data
{
    [CreateAssetMenu(fileName="TrashCount", menuName="TrashCountObject/HungerData")]
    public partial class HungerData : ScriptableObject
    {
        [SerializeField] public Dictionary<string, float> DrainValue = new();
        #if UNITY_EDITOR
        private void OnValidate()
        {
            EnumGenerator.GenerateFromDictionary("HungerState", nameof(HungerData), DrainValue);
        }
        #endif
    }
}
