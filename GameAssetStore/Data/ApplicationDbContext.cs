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
    }
}
