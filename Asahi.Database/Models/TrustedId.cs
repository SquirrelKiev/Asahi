using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Asahi.Database.Models;

public class TrustedId
{
    public const int CommentMaxLength = 200;

    [Key]
    public required ulong Id { get; set; }

    [MaxLength(CommentMaxLength)]
    public required string Comment { get; set; }

    public required TrustedUserPerms PermissionFlags { get; set; }

    [JsonIgnore]
    public required BotWideConfig BotWideConfig { get; set; }
}

[Flags]
[JsonConverter(typeof(StringEnumConverter))]
public enum TrustedUserPerms
{
    None = 0,
    WolframPerms = 1 << 0,
    TrustedUserEditPerms = 1 << 1,
    StatusPerms = 1 << 2,
    BotGuildManagementPerms = 1 << 3,
    CodeExecutionPerms = 1 << 4,
    TestCommandNukingPerms = 1 << 5,
    FeedTogglingPerms = 1 << 6,
}
