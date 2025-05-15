using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Database.Models;

[PrimaryKey(nameof(GuildId), nameof(EmoteName))]
public class EmoteAlias
{
    public required ulong GuildId { get; set; }

    [MaxLength(32)]
    public required string EmoteName { get; set; }

    [MaxLength(100)]
    public required string EmoteReplacement { get; set; }
}
