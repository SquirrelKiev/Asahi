namespace Seigen.Database.Models;

public class CachedUserRole
{
    public required ulong GuildId { get; set; }
    public required ulong UserId { get; set; }
    public required ulong RoleId { get; set; }
}