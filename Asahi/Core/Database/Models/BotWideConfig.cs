using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Asahi.Database.Models;

public class BotWideConfig
{
    [Key]
    public bool DumbKey { get; set; } = true;

    public bool ShouldHaveActivity { get; set; } = false;
    public UserStatus UserStatus { get; set; } = UserStatus.Online;
    public ActivityType ActivityType { get; set; } = ActivityType.CustomStatus;
    [MaxLength(128)]
    public string BotActivity { get; set; } = "Doing nothing.";
    [MaxLength(128)]
    public string ActivityStreamingUrl { get; set; } = "https://www.twitch.tv/jerma985";

    public ICollection<TrustedId> TrustedIds { get; set; } = [];
}

public class TrustedId
{
    public const int CommentMaxLength = 200;

    public TrustedId()
    {

    }

    [SetsRequiredMembers]
    public TrustedId(ulong id, string comment)
    {
        Id = id;
        Comment = comment;
    }

    [Key]
    public required ulong Id { get; set; }

    [MaxLength(CommentMaxLength)]
    public required string Comment { get; set; }
}