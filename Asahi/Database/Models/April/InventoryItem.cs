using System.ComponentModel.DataAnnotations.Schema;

namespace Asahi.Database.Models.April;

public class InventoryItem : DbModel
{
    public ulong UserId { get; set; }

    public ulong GuildId { get; set; }

    public required Guid ItemGuid { get; set; }
    public bool IsEquipped { get; set; } = false;

#nullable disable
    [ForeignKey($"{nameof(UserId)},{nameof(GuildId)}")]
    public UserData UserData { get; set; }
#nullable enable
}
