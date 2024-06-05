using System.ComponentModel.DataAnnotations;

namespace Asahi.Database.Models;

// not sure why you'd want multiple, but I want the db to be able to support it such the need comes up
// overengineered I know
// edit: maybe celebrating general anniversaries
//       "my discord account was created xyz date woo" type thing
public class BirthdayConfig
{
    public const int MaxStringLength = 200;
    public const int MaxNameLength = 32;

    [MaxLength(MaxNameLength)]
    public required string Name { get; set; }

    public required ulong GuildId { get; set; }

    public ulong BirthdayRole { get; set; } = 0ul;

    // Denied roles takes priority.
    public List<ulong> AllowedRoles { get; set; } = [];
    public List<ulong> DeniedRoles { get; set; } = [];

    /// <summary>
    /// How long users are allowed to edit their birthday for once they set it. Set to zero for infinite time.
    /// </summary>
    public int EditWindowSeconds { get; set; } = 1800; // 30 minutes

    [MaxLength(MaxNameLength)]
    public string DisplayName { get; set; } = "birthday";

    [MaxLength(MaxStringLength)]
    public string EmbedTitleText { get; set; } = $"{UsernamePlaceholder}, your birthday has been set!";

    [MaxLength(MaxStringLength)]
    public string EmbedDescriptionText { get; set; } = "You will receive a hoisted birthday role when the day arrives!";

    [MaxLength(MaxStringLength)]
    public string EmbedFooterText { get; set; } = "To prevent abuse, you can change your birthday date for the next 30 minutes. " +
                                             "To change it after that, ask the mods.";

    [MaxLength(MaxStringLength)]
    public string DeniedForReasonEditWindowText { get; set; } = "To prevent abuse, you're no longer allowed to change your set birthday. If you still wish to, please ask the mods.";
    [MaxLength(MaxStringLength)]
    public string DeniedForReasonPermissionsText { get; set; } = "You do not have the correct permissions to set/change your birthday.";

    public EmbedColorSource EmbedColorSource { get; set; } = EmbedColorSource.BotsRoleColor;

    /// <remarks>0 means no embed color.</remarks>>
    public uint FallbackEmbedColor { get; set; } = 0u;

    public const string UsernamePlaceholder = "{displayname}";
}