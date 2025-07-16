using Microsoft.AspNetCore.Identity;

namespace GameAssetStore.Models
{
    public class User : IdentityUser
    {
        public int Id { get; set; }
    }
}
