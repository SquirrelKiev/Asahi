using Newtonsoft.Json;
using Refit;

namespace Asahi.Modules;

public interface IFxTwitterApi
{
    [Get($"/2/status/{{{nameof(statusId)}}}")]
    Task<FxTwitterStatusResponse> GetStatusInfo(ulong statusId);
}

public record FxTwitterStatusResponse
{
    [JsonProperty("code")] public int Code { get; init; }
    [JsonProperty("message")] public required string Message { get; init; }

    [JsonProperty("tweet")] public FxTwitterTweet? Tweet { get; init; }

    public record FxTwitterTweet
    {
        [JsonProperty("media")] public FxTwitterMediaOptions Media { get; init; }

        public record FxTwitterMediaOptions
        {
            // public FxTwitterMediaEntry[] All { get; init; }

            [JsonProperty("photos")] public required FxTwitterMediaEntry[] Photos { get; init; }

            public record FxTwitterMediaEntry
            {
                [JsonProperty("type")] public string Type { get; init; }
                [JsonProperty("url")] public string Url { get; init; }
                [JsonProperty("width")] public int Width { get; init; }

                [JsonProperty("height")] public int Height { get; init; }
                // public string AltText { get; init; }
            }
        }
    }
}
