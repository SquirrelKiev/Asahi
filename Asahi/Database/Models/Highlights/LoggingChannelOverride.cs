using Newtonsoft.Json;

namespace Asahi.Database.Models;

public class LoggingChannelOverride
{
    public required ulong OverriddenChannelId { get; set; }
    public required ulong LoggingChannelId { get; set; }

    [JsonIgnore]
    public HighlightBoard HighlightBoard { get; set; } = null!;
}