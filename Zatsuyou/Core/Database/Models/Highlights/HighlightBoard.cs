using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Zatsuyou.Database.Models;

public class HighlightBoard
{
    public const int MaxNameLength = 32;

    [MaxLength(MaxNameLength)]
    public required string Name { get; set; }

    public required ulong GuildId { get; set; }

    public required ulong LoggingChannelId { get; set; }

    public uint Threshold { get; set; } = 3;

    public bool FilteredChannelsIsBlockList { get; set; } = true;

    public List<ulong> FilteredChannels { get; set; } = [];

    // linq2db wasn't happy with something key related with CachedHighlightedMessage and wouldn't let me do anything so no timespans. this will do
    public uint MaxMessageAgeSeconds { get; set; } = 0;

    [JsonIgnore]
    public ICollection<CachedHighlightedMessage> HighlightedMessages { get; } = new List<CachedHighlightedMessage>();
}