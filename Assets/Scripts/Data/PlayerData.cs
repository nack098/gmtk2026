using UnityEngine;

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
    }
}
