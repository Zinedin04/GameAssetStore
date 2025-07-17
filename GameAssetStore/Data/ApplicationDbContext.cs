using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GameAssetStore.Models;

namespace GameAssetStore.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<GameAssetStore.Models.Asset> Asset { get; set; } = default!;
        public DbSet<GameAssetStore.Models.Store> Store { get; set; } = default!;
        public DbSet<GameAssetStore.Models.User> User { get; set; } = default!;
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Asset>().ToTable("Asset");
            builder.Entity<Store>().ToTable("Store");
            builder.Entity<User>().ToTable("User");
        }
    }
}
