using System.ComponentModel.DataAnnotations;
using BotBase.Database;

namespace Asahi.Database.Models.Rss;

public class FeedListener : DbModel
{
    [MaxLength(512)]
    public required string FeedUrl { get; set; }

    [MaxLength(64)]
    public string? FeedTitle { get; set; }

    public required ulong GuildId { get; set; }

    public required ulong ChannelId { get; set; }
}