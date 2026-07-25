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
        
        public InventoryModel InventoryData = new();
        public List<ItemModel> CartItemsData = new();

        public void CopyFrom(GameData source)
        {
            if (source == null) return;

            Money = source.Money;
            MaxInventorySize = source.MaxInventorySize;

            if (source.DateTime != null)
            {
                if (DateTime == null) DateTime = new ClockModel();
                DateTime.Day = source.DateTime.Day;
                DateTime.TotalSeconds = source.DateTime.TotalSeconds;
            }

            if (source.PlayerData != null)
            {
                if (PlayerData == null) PlayerData = new CharacterModel();
                PlayerData.Healthy = source.PlayerData.Healthy;
                PlayerData.Hunger = source.PlayerData.Hunger;
                PlayerData.IsSick = source.PlayerData.IsSick;
            }

            if (source.FatherData != null)
            {
                if (FatherData == null) FatherData = new CharacterModel();
                FatherData.Healthy = source.FatherData.Healthy;
                FatherData.Hunger = source.FatherData.Hunger;
                FatherData.IsSick = source.FatherData.IsSick;
            }

            if (source.InventoryData != null)
            {
                if (InventoryData == null) InventoryData = new InventoryModel();
                InventoryData.MaxInventorySize = source.InventoryData.MaxInventorySize;
                InventoryData.Clear();
                if (source.InventoryData.Items != null)
                {
                    foreach (var item in source.InventoryData.Items)
                    {
                        if (item != null) InventoryData.Add(item);
                    }
                }
            }

            CartItemsData = new List<ItemModel>();
            if (source.CartItemsData != null)
            {
                foreach (var item in source.CartItemsData)
                {
                    if (item != null) CartItemsData.Add(item);
                }
            }
        }
    }
}