namespace TrashCount.Data.Models
{
    [System.Serializable]
    public class ClockModel
    {
        public float TotalSeconds;

        public uint Day => (uint)(TotalSeconds / 86400);
        public uint Hour => (uint)((TotalSeconds % 86400) / 3600);
        public uint Minute => (uint)((TotalSeconds % 3600) / 60);
        public float Second => TotalSeconds % 60;

        public void Update(float delta) => TotalSeconds += delta;
    }
}
