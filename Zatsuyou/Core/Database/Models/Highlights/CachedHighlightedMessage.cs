using BotBase.Database;

namespace Zatsuyou.Database.Models;

public class CachedHighlightedMessage : DbModel
{
    //public required ulong GuildId { get; set; }

    //public required ulong ChannelId { get; set; }

    public required ulong OriginalMessageId { get; set; }

    public required ulong HighlightMessageId { get; set; }

    public HighlightBoard HighlightBoard { get; set; } = null!;
}
