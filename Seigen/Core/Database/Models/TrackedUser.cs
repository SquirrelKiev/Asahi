using BotBase.Database;

namespace Seigen.Database.Models;

public class TrackedUser : DbModel
{
    public required Trackable Trackable { get; set; }

    public ulong UserId { get; set; }
}