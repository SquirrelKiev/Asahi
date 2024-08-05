using System.ComponentModel.DataAnnotations;

namespace Asahi.Database.Models.Rss;

public class FeedListener : DbModel
{
    [MaxLength(512)]
    public required string FeedUrl { get; set; }

    [MaxLength(64)]
    public string? FeedTitle { get; set; }

    public required ulong GuildId { get; set; }

    public required ulong ChannelId { get; set; }

    [MaxLength(80)]
    public string? WebhookName { get; set; }
}
