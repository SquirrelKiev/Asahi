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

    /// <summary>
    /// If the user has the feed enabled or disabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// If the feed should be disabled forcibly. overrides <see cref="Enabled"/>.
    /// </summary>
    public bool ForcedDisable { get; set; } = false;
    
    /// <summary>
    /// The reason for a feed being force disabled.
    /// </summary>
    [MaxLength(512)]
    public string DisabledReason { get; set; } = "";
}
