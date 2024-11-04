namespace Asahi.Database.Models;

public class CachedHighlightedMessage : DbModel
{
    /// <summary>
    /// For statistics
    /// </summary>
    public int Version { get; set; } = 2;

    public required ulong OriginalMessageChannelId { get; set; }

    public required ulong OriginalMessageId { get; set; }

    /// <summary>
    /// The date and time the highlighted message was sent - used for statistics
    /// </summary>
    public required DateTime HighlightedMessageSendDate { get; set; }

    /// <summary>
    ///  The author of the message that got highlighted - used for statistics
    /// </summary>
    public required ulong AuthorId { get; set; }

    /// <summary>
    /// The author of the message the highlighted message was replying to - used for statistics
    /// </summary>
    public required ulong? AssistAuthorId { get; set; }

    /// <summary>
    /// The reaction count of the highlighted message - used for statistics
    /// </summary>
    public required int TotalUniqueReactions { get; set; }
    
    /// <summary>
    /// A list of cached message reactions - used for statistics
    /// </summary>
    public List<CachedMessageReaction> CachedMessageReactions { get; set; }
    
    /// <summary>
    /// The IDs of the messages sent by the bot in the highlights channel
    /// </summary>
    public required List<ulong> HighlightMessageIds { get; set; }

    public HighlightBoard HighlightBoard { get; set; } = null!;
}
