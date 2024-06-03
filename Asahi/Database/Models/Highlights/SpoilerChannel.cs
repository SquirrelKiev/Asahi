using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Asahi.Database.Models;

public class SpoilerChannel
{
    public const int MaxContextLength = 200;

    public ulong ChannelId { get; set; }

    [MaxLength(MaxContextLength)]
    public string SpoilerContext { get; set; }= "";

    [JsonIgnore]
    public HighlightBoard HighlightBoard { get; set; } = null!;
}