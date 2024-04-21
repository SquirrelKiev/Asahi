using Newtonsoft.Json;

namespace Asahi.Database.Models;

public class HighlightThreshold
{
    /// <remarks>Can be Thread, Channel, Category, or Guild. Will be checked in that order.</remarks>
    public required ulong OverrideId { get; set; }

    public int BaseThreshold { get; set; } = 3;
    public int MaxThreshold { get; set; } = 6;

    public int UniqueUserMessageMaxAgeSeconds { get; set; } = 1800;
    public float UniqueUserMultiplier { get; set; } = 0.25f;

    [JsonIgnore]
    public HighlightBoard HighlightBoard { get; set; } = null!;

    public HighlightThreshold ShallowCopy()
    {
        return (HighlightThreshold)MemberwiseClone();
    }
}

