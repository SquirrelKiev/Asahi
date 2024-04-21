using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Asahi.Database.Models;

public class EmoteAlias
{
    [MaxLength(32)]
    public required string EmoteName { get; set; }

    [MaxLength(100)]
    public required string EmoteReplacement { get; set; }

    [JsonIgnore]
    public HighlightBoard HighlightBoard { get; set; } = null!;
}
