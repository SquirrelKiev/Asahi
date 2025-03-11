using System.ComponentModel.DataAnnotations;

namespace Asahi.Database.Models.Rss;

public class FeedListener : DbModel
{
    ///  <remarks>More of a feed source than a feed url. For example, reddit feeds do not use a URL format.</remarks>
    [MaxLength(512)]
    public required string FeedUrl { get; set; }

    [MaxLength(64)]
    public string? FeedTitle { get; set; }

    public required ulong GuildId { get; set; }

    public required ulong ChannelId { get; set; }

    [MaxLength(80)]
    public string? WebhookName { get; set; }
}
