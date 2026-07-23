using UnityEngine;
using System.Collections.Generic;
using TrashCount.Data.Models;

namespace TrashCount.Data
{
    [CreateAssetMenu(fileName="TrashCount", menuName="TrashCountObject/PlayerData")]
    public class PlayerData : ScriptableObject
    {
        public float Speed;
        public List<ItemModel> PickUp = new();
    }
}
