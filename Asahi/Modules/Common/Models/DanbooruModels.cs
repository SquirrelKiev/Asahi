using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Asahi.Modules.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum DanbooruRating
{
    [EnumMember(Value = "g")]
    General,
    [EnumMember(Value = "s")]
    Suggestive,
    [EnumMember(Value = "q")]
    Questionable,
    [EnumMember(Value = "e")]
    Explicit
}

// should the indexes here be longs?
public class DanbooruPost
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("uploader_id")]
    public int UploaderId { get; set; }

    [JsonProperty("approver_id")]
    public int? ApproverId { get; set; }

    [JsonProperty("tag_string")]
    public string TagString { get; set; } = null!;

    [JsonProperty("tag_string_general")]
    public string TagStringGeneral { get; set; } = null!;

    [JsonProperty("tag_string_artist")]
    public string TagStringArtist { get; set; } = null!;

    [JsonProperty("tag_string_copyright")]
    public string TagStringCopyright { get; set; } = null!;

    [JsonProperty("tag_string_character")]
    public string TagStringCharacter { get; set; } = null!;

    [JsonProperty("tag_string_meta")]
    public string TagStringMeta { get; set; } = null!;

    [JsonProperty("rating")]
    public DanbooruRating Rating { get; set; }

    [JsonProperty("parent_id")]
    public int? ParentId { get; set; }

    [JsonProperty("source")]
    public string Source { get; set; } = null!;

    [JsonProperty("md5")]
    public string Md5 { get; set; } = null!;

    [JsonProperty("file_url")]
    public string FileUrl { get; set; } = null!;

    [JsonProperty("large_file_url")]
    public string LargeFileUrl { get; set; } = null!;

    [JsonProperty("preview_file_url")]
    public string PreviewFileUrl { get; set; } = null!;

    [JsonProperty("file_ext")]
    public string FileExtension { get; set; } = null!;

    [JsonProperty("file_size")]
    public int FileSize { get; set; }

    [JsonProperty("image_width")]
    public int ImageWidth { get; set; }

    [JsonProperty("image_height")]
    public int ImageHeight { get; set; }

    [JsonProperty("score")]
    public int Score { get; set; }

    [JsonProperty("fav_count")]
    public int FavCount { get; set; }

    [JsonProperty("tag_count_general")]
    public int TagCountGeneral { get; set; }

    [JsonProperty("tag_count_artist")]
    public int TagCountArtist { get; set; }

    [JsonProperty("tag_count_copyright")]
    public int TagCountCopyright { get; set; }

    [JsonProperty("tag_count_character")]
    public int TagCountCharacter { get; set; }

    [JsonProperty("tag_count_meta")]
    public int TagCountMeta { get; set; }

    [JsonProperty("media_asset")]
    public DanbooruMediaAsset MediaAsset { get; set; } = null!;

    [JsonProperty("pixiv_id")]
    public int? PixivId { get; set; }

    [JsonProperty("last_comment_bumped_at")]
    public DateTimeOffset? LastCommentBumpedAt { get; set; }

    [JsonProperty("last_noted_at")]
    public DateTimeOffset? LastNotedAt { get; set; }

    [JsonProperty("has_children")]
    public bool HasChildren { get; set; }

    [JsonProperty("has_visible_children")]
    public bool HasVisibleChildren { get; set; }

    [JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}

public class DanbooruMediaAsset
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty("md5")]
    public string Md5 { get; set; } = null!;

    [JsonProperty("file_ext")]
    public string FileExtension { get; set; } = null!;

    [JsonProperty("file_size")]
    public int FileSize { get; set; }

    [JsonProperty("image_width")]
    public int ImageWidth { get; set; }

    [JsonProperty("image_height")]
    public int ImageHeight { get; set; }

    [JsonProperty("duration")]
    public float? Duration { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = null!;

    [JsonProperty("file_key")]
    public string FileKey { get; set; } = null!;

    [JsonProperty("is_public")]
    public bool IsPublic { get; set; }

    [JsonProperty("pixel_hash")]
    public string PixelHash { get; set; } = null!;

    [JsonProperty("variants")]
    public DanbooruVariant[]? Variants { get; set; }
}

public class DanbooruVariant
{
    [JsonProperty("type")]
    public string Type { get; set; } = null!;

    [JsonProperty("url")]
    public string Url { get; set; } = null!;

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("file_ext")]
    public string FileExt { get; set; } = null!;
}

public class DanbooruVariantWithExtras(DanbooruVariant variant)
{
    public DanbooruVariant Variant { get; } = variant;
    
    public string[]? ExtraUrls { get; init; } = null;
}

public class DanbooruSource
{
    [JsonProperty("page_url")]
    public string? PageUrl { get; set; }

    [JsonProperty("image_urls")]
    public List<string> ImageUrls { get; set; } = [];

    [JsonProperty("artist")]
    public DanbooruSourceArtist Artist { get; set; } = new();

    [JsonProperty("tags")]
    public List<List<string>> Tags { get; set; } = [];

    [JsonProperty("artist_commentary")]
    public DanbooruSourceArtistCommentary ArtistCommentary { get; set; } = new();

    public bool IsMostLikelyUseless(string inputUrl)
    {
        return PageUrl == null || ImageUrls.Count < 1 || ImageUrls[0] == inputUrl;
    }
}

public class DanbooruSourceArtist
{
    [JsonProperty("display_name")]
    public string? DisplayName { get; set; }

    [JsonProperty("username")]
    public string? Username { get; set; }

    [JsonProperty("profile_urls")]
    public List<Uri> ProfileUrls { get; set; } = [];

    [JsonProperty("artists")]
    public List<DanbooruSourceArtistReference> Artists { get; set; } = [];
}

public class DanbooruSourceArtistReference
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}

public class DanbooruSourceArtistCommentary
{
    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("dtext_title")]
    public string? DtextTitle { get; set; }

    [JsonProperty("dtext_description")]
    public string? DtextDescription { get; set; }
}
