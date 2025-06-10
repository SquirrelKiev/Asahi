using System.ComponentModel.DataAnnotations;

namespace Asahi.BotEmoteManagement;

public class InternalCustomEmoteTracking
{
    [Key]
    public required string EmoteKey { get; set; }
    public required ulong EmoteId { get; set; }
    public required bool IsAnimated { get; set; }
    // not sure what the "preferred" hashing algorithm is for files
    public required byte[] Sha256Hash { get; set; }
}