using System.ComponentModel.DataAnnotations;

namespace Asahi.Database.Models;

public class GuildConfig
{
    public const int MaxPrefixLength = 8;
    public const string DefaultPrefix = "]";

    [Key]
    public required ulong GuildId { get; set; }

    [MaxLength(MaxPrefixLength)]
    public string Prefix { get; set; } = DefaultPrefix;

    public bool SpoilerBotAutoDeleteOriginal { get; set; } = true;

    public bool SpoilerBotAutoDeleteContextSetting { get; set; } = true;
    
    public bool ShouldSendWelcomeMessage { get; set; } = false;
    public string WelcomeMessageJson { get; set; } = "";
    public ulong WelcomeMessageChannelId { get; set; } = 0ul;

    [MaxLength(100)]
    public string SpoilerReactionEmote { get; set; } = "";

    public BirthdayConfig? DefaultBirthdayConfig { get; set; }
}
