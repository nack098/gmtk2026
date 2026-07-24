using UnityEngine;
using TrashCount.Data.Models;
using System.Collections.Generic;

namespace TrashCount.Data
{
    [CreateAssetMenu(fileName = "TrashCount", menuName = "TrashCountObject/PlayerData")]
    public class PlayerData : ScriptableObject
    {
        public float Speed;
        public float Healthy;
        public float Hungry;
        public float Stamina;
        public float StaminaRegen;
        public float HungryMultiple;
        public float StaminaToHungryRatio = 1.0f; // Ratio of Hungry consumed per 1 Stamina regenerated
        public float StaminaToHealthyRatio = 1.0f; // Ratio of Healthy consumed per 1 Stamina regenerated when Hungry is 0
        public float CarryStaminaDrainRate = 10f;
        public float CarrySpeedMultiplier = 0.8f;
        public float RummageStaminaCost = 15f;
        public float CartBaseStaminaDrainRate = 5f;
        public float CartWeightStaminaMultiplier = 1.5f;
        public float CartBaseSpeedMultiplier = 0.85f;
        public List<ItemModel> PickUp = new();
    }
}
