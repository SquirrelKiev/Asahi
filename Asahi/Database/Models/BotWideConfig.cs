using System.ComponentModel.DataAnnotations;

namespace Asahi.Database.Models;

public class BotWideConfig
{
    [Key]
    public required ulong BotId { get; set; }

    public bool ShouldHaveActivity { get; set; } = false;
    public UserStatus UserStatus { get; set; } = UserStatus.Online;
    public ActivityType ActivityType { get; set; } = ActivityType.CustomStatus;

    //[MaxLength(128)]
    public string[] BotActivities { get; set; } = ["Doing nothing."];
    [MaxLength(128)]
    public string ActivityStreamingUrl { get; set; } = "https://www.twitch.tv/jerma985";

    public ICollection<TrustedId> TrustedIds { get; } = new List<TrustedId>();
}

