using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asahi.Database.Models;

public class CachedHighlightedMessage : DbModel
{
    public const int LatestVersion = 1;
    
    /// <summary>
    /// For statistics
    /// </summary>
    public int Version { get; set; } = LatestVersion;

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
    /// The unique reaction count (unique users) of the highlighted message - used for statistics
    /// </summary>
    public required int TotalUniqueReactions { get; set; }

    /// <summary>
    /// A list of cached message reactions - used for statistics
    /// </summary>
    public ICollection<CachedMessageReaction> CachedMessageReactions { get; set; } = new List<CachedMessageReaction>();

    /// <summary>
    /// The IDs of the messages sent by the bot in the highlights channel
    /// </summary>
    public required List<ulong> HighlightMessageIds { get; set; }
    
    [MaxLength(HighlightBoard.MaxNameLength)]
    public string HighlightBoardName { get; set; } = null!;
    
    public ulong HighlightBoardGuildId { get; set; }
    
    [ForeignKey($"{nameof(HighlightBoardGuildId)},{nameof(HighlightBoardName)}")]
    public HighlightBoard HighlightBoard { get; set; } = null!;

    public void UpdateReactions(Dictionary<IEmote, HashSet<ulong>> emoteUserMap)
    {
        var reactionLookup = CachedMessageReactions.ToDictionary(
            r => (r.EmoteId, r.EmoteName),
            r => r
        );

        foreach (var (emote, users) in emoteUserMap)
        {
            var emoteName = emote.Name;
            ulong emoteId = 0ul;
            bool isAnimated = false;
            if (emote is Emote customEmote)
            {
                emoteId = customEmote.Id;
                isAnimated = customEmote.Animated;
            }
            var userCount = users.Count;

            if (reactionLookup.TryGetValue((emoteId, emoteName), out var existingReaction))
            {
                if (existingReaction.Count != userCount)
                {
                    existingReaction.Count = userCount;
                }
                
                reactionLookup.Remove((emoteId, emoteName));
            }
            else
            {
                CachedMessageReactions.Add(new CachedMessageReaction
                {
                    EmoteName = emoteName,
                    EmoteId = emoteId,
                    Count = userCount,
                    IsAnimated = isAnimated,
                    HighlightedMessage = this
                });
            }
        }

        if (reactionLookup.Count > 0)
        {
            var reactionsToRemove = CachedMessageReactions
                .Where(r => reactionLookup.ContainsKey((r.EmoteId, r.EmoteName)))
                .ToList();
            
            foreach (var reaction in reactionsToRemove)
            {
                CachedMessageReactions.Remove(reaction);
            }
        }
    }
}
