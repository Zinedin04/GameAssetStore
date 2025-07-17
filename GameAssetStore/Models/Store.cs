namespace GameAssetStore.Models
{
    public class Store
    {
        public int Id { get; set; }
        public int AssetId { get; set; }
        public Asset Asset { get; set; }
        public int NumberOfAssets { get; set; }
        
    }
}
