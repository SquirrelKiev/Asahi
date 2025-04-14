using System.ComponentModel.DataAnnotations;

namespace Asahi.Database.Models.Rss;

public class FeedListener : DbModel
{
    ///  <remarks>More of a feed source than a feed url. For example, reddit feeds do not use a URL format.</remarks>
    [MaxLength(512)]
    public required string FeedUrl { get; set; }

    [MaxLength(64)]
    public string? FeedTitle { get; set; }

    public required ulong GuildId { get; init; }

    public required ulong ChannelId { get; set; }

    [MaxLength(80)]
    public string? WebhookName { get; set; }

    public bool Enabled { get; set; } = true;

    public bool ForcedDisable { get; set; } = false;
    
    [MaxLength(512)]
    public string DisabledReason { get; set; } = "";
}
