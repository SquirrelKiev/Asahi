using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Asahi.Database.Models;

public class TrustedId
{
    [Flags]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TrustedUserPerms
    {
        None = 0,
        WolframPerms = 1,
        TrustedUserEditPerms = 2,
        StatusPerms = 4,
        BotGuildManagementPerms = 8,
    }

    public const int CommentMaxLength = 200;

    [Key]
    public required ulong Id { get; set; }

    [MaxLength(CommentMaxLength)]
    public required string Comment { get; set; }

    public required TrustedUserPerms PermissionFlags { get; set; }

    [JsonIgnore]
    public required BotWideConfig BotWideConfig { get; set; }
}