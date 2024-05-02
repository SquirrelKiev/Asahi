using BotBase.Database;

namespace Asahi.Database.Models;

public class CachedHighlightedMessage : DbModel
{
    public required ulong OriginalMessageChannelId { get; set; }

    public required ulong OriginalMessageId { get; set; }

    public required List<ulong> HighlightMessageIds { get; set; }

    public HighlightBoard HighlightBoard { get; set; } = null!;
}
