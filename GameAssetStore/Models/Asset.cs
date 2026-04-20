namespace GameAssetStore.Models
{
    public class Asset
    {
        public int Id { get; set; }
        public string OwnerId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string? Url { get; set; }
        public float Price { get; set; }
        public string? ImageUrl { get; set; }

    }
}
