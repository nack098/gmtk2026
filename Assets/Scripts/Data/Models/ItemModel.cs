namespace TrashCount.Data.Models
{
    [System.Serializable]
    public class ItemModel
    {
        [UnityEngine.SerializeReference] public ItemBaseModel Value;
        
        public ItemModel(ItemBaseModel @value)
        {
            Value = @value;
        }
    }
    
    [System.Serializable]
    public abstract class ItemBaseModel
    {
        
    }
    
    [System.Serializable]
    public class Eatable: ItemBaseModel
    {
        public int Restore;
    }
}