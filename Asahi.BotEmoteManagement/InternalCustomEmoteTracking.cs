using System.ComponentModel.DataAnnotations;

namespace Asahi.BotEmoteManagement;

public class InternalCustomEmoteTracking
{
    [Key]
    public required string EmoteKey { get; set; }
    public required ulong EmoteId { get; set; }
    public required bool IsAnimated { get; set; }
    public required byte[] EmoteDataIdentifier { get; set; }
}