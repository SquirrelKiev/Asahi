using BotBase.Database;

namespace Seigen.Database.Models;

public class CachedUserRole : DbModel
{
    public required ulong UserId { get; set; }
    public required ulong RoleId { get; set; }
}