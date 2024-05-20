using System.ComponentModel;
using Discord.Interactions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Asahi.Database.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum EmbedColorSource
{
    [ChoiceDisplay("Always use the fallback color.")]
    [Description("Always use the fallback color.")]
    AlwaysUseFallbackColor,
    [ChoiceDisplay("Use the user's role color.")]
    [Description("Use the user's role color.")]
    UsersRoleColor,
    [ChoiceDisplay("Use the user's banner color.")]
    [Description("Use the user's banner color.")]
    UsersBannerColor,
    [ChoiceDisplay("Use the user's accent color.")]
    [Description("Use the user's accent color.")]
    UsersAccentColor
}

[JsonConverter(typeof(StringEnumConverter))]
public enum AutoReactEmoteChoicePreference
{
    [ChoiceDisplay("Attempt to react with every emote, from most reactions to least, otherwise, fallback.")]
    [Description("Attempt to react with every emote, from most reactions to least, otherwise, fallback.")]
    ReactionsDescendingPopularity = 0
}


public class HighlightBoard
{
    public const int MaxNameLength = 32;

    [MaxLength(MaxNameLength)]
    public required string Name { get; set; }

    public required ulong GuildId { get; set; }

    public required ulong LoggingChannelId { get; set; }

    public bool FilterSelfReactions { get; set; } = false;

    public bool FilteredChannelsIsBlockList { get; set; } = true;
    public List<ulong> FilteredChannels { get; set; } = [];

    public ulong HighlightsMuteRole { get; set; } = 0ul;

    // linq2db wasn't happy with something key related with CachedHighlightedMessage and wouldn't let me do anything so no timespans. this will do
    public int MaxMessageAgeSeconds { get; set; } = 28800;

    public EmbedColorSource EmbedColorSource { get; set; } = EmbedColorSource.UsersRoleColor;

    /// <remarks>0 means no embed color.</remarks>>
    public uint FallbackEmbedColor { get; set; } = 0u;

    public int AutoReactMaxAttempts { get; set; } = 1;
    public int AutoReactMaxReactions { get; set; } = 1;

    public AutoReactEmoteChoicePreference AutoReactEmoteChoicePreference { get; set; } = AutoReactEmoteChoicePreference.ReactionsDescendingPopularity;

    /// <remarks>an empty string means don't use the fallback.</remarks>
    [MaxLength(100)]
    public string AutoReactFallbackEmoji { get; set; } = "\ud83d\ude2d"; // decodes to 😭

    public ICollection<SpoilerChannel> SpoilerChannels { get; set; } = new List<SpoilerChannel>();
    public ICollection<LoggingChannelOverride> LoggingChannelOverrides { get; set; } = new List<LoggingChannelOverride>();

    [JsonIgnore]
    public ICollection<CachedHighlightedMessage> HighlightedMessages { get; } = new List<CachedHighlightedMessage>();

    public ICollection<HighlightThreshold> Thresholds { get; set; } = new List<HighlightThreshold>();
}