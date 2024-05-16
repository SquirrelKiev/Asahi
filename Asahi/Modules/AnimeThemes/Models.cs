using System.Runtime.Serialization;
using Humanizer;
using Newtonsoft.Json;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

namespace Asahi.Modules.AnimeThemes;


public class SearchResponse
{
    public SearchResponsePartTwo search;
}

public class SearchResponsePartTwo
{
    public AnimeResource[] anime;
}

public class AnimeResource
{
    /// <summary>
    /// The primary key of the resource
    /// </summary>
    public int id;

    /// <summary>
    /// The primary title of the anime
    /// </summary>

    public string name;

    /// <summary>
    /// The URL slug & route key of the resource
    /// </summary>
    public string? slug;

    /// <summary>
    /// The premiere year of the anime
    /// </summary>
    public int? year;

    /// <summary>
    /// The premiere season of the anime [Winter, Spring, Summer, Fall]
    /// </summary>
    public SeasonEnum? season;

    /// <summary>
    /// The media format of the anime [Unknown, TV, TV Short, OVA, Movie, Special, ONA]
    /// </summary>
    public MediaFormatEnum? media_format;

    /// <summary>
    /// The brief summary of the anime
    /// </summary>
    public string? synopsis;

    /// <summary>
    /// The date that the resource was created
    /// </summary>
    public DateTime? created_at;

    /// <summary>
    /// The date that the resource was last modified
    /// </summary>
    public DateTime? updated_at;

    /// <summary>
    /// The date that the resource was deleted
    /// </summary>
    public DateTime? deleted_at;

    public enum SeasonEnum
    {
        Winter,
        Spring,
        Summer,
        Fall
    }

    public enum MediaFormatEnum
    {
        Unknown,
        TV,
        [EnumMember(Value = "TV Short")]
        TV_Short,
        OVA,
        Movie,
        Special,
        ONA
    }

    // ReSharper disable once IdentifierTypo
    public AnimeThemeResource[]? animethemes;
    public ImageResource[]? images;
}

/// <summary>
/// Represents an image resource with metadata about its storage and usage.
/// </summary>
/// <summary>
/// Represents an image resource with metadata about its storage and usage.
/// </summary>
public class ImageResource
{
    /// <summary>
    /// The primary key of the resource.
    /// </summary>
    /// <value>The unique identifier for the resource.</value>
    public int id;

    /// <summary>
    /// The path of the file in storage.
    /// </summary>
    /// <value>The file path in the storage system.</value>
    public string path;

    /// <summary>
    /// The size of the file in storage in Bytes.
    /// </summary>
    /// <value>The size of the file in bytes.</value>
    public int size;

    /// <summary>
    /// The media type of the file in storage.
    /// </summary>
    /// <value>The MIME type of the file.</value>
    public string mimetype;

    /// <summary>
    /// The component that the resource is intended for.
    /// </summary>
    /// <value>The intended use of the resource, such as Small Cover, Large Cover, or Grill.</value>
    public Facet? facet;

    /// <summary>
    /// The URL to stream the file from storage.
    /// </summary>
    /// <value>The streaming URL for the file.</value>
    public string link;

    /// <summary>
    /// The date that the resource was created.
    /// </summary>
    /// <value>The creation date of the resource.</value>
    public DateTime created_at;

    /// <summary>
    /// The date that the resource was last modified.
    /// </summary>
    /// <value>The last modification date of the resource.</value>
    public DateTime updated_at;

    /// <summary>
    /// The date that the resource was deleted.
    /// </summary>
    /// <value>The deletion date of the resource.</value>
    public DateTime? deleted_at;

    /// <summary>
    /// Specifies the intended use of the image resource.
    /// </summary>
    public enum Facet
    {
        /// <summary>
        /// Small cover image.
        /// </summary>
        [EnumMember(Value = "Small Cover")]
        SmallCover,

        /// <summary>
        /// Large cover image.
        /// </summary>
        [EnumMember(Value = "Large Cover")]
        LargeCover,

        /// <summary>
        /// Grill image.
        /// </summary>
        Grill
    }
}

public class AnimeThemeResource
{
    /// <summary>
    /// The primary key of the resource
    /// </summary>
    public int id;

    /// <summary>
    /// The type of the sequence [OP, ED]
    /// </summary>
    public ThemeTypeEnum? type;

    /// <summary>
    /// The numeric ordering of the theme
    /// </summary>
    public int? sequence;

    /// <summary>
    /// The URL slug & route key of the resource
    /// </summary>
    public string? slug;

    /// <summary>
    /// The date that the resource was created
    /// </summary>
    public DateTime? created_at;

    /// <summary>
    /// The date that the resource was last modified
    /// </summary>
    public DateTime? updated_at;

    /// <summary>
    /// The date that the resource was deleted
    /// </summary>
    public DateTime? deleted_at;

    public enum ThemeTypeEnum
    {
        OP,
        ED
    }

    // includes
    [JsonProperty("animethemeentries")]
    public AnimeThemeEntryResource[]? animeThemeEntries;

    public SongResource? song;

    public override string ToString()
    {
        List<string> labels = [];
        if (animeThemeEntries != null)
        {
            if (animeThemeEntries.All(x => x.nsfw.GetValueOrDefault()))
            {
                labels.Add("NSFW");
            }
            else if (animeThemeEntries.Any(x => x.nsfw.GetValueOrDefault()))
            {
                labels.Add("May contain NSFW");
            }

            if (animeThemeEntries.All(x => x.spoiler.GetValueOrDefault()))
            {
                labels.Add("spoilers");
            }
            else if (animeThemeEntries.Any(x => x.spoiler.GetValueOrDefault()))
            {
                labels.Add("may contain spoilers");
            }
        }

        var warnings = "";
        if (labels.Count != 0)
        {
            warnings = $"({labels.Humanize()}) ";
        }

        return $"{warnings}{slug}{(song != null ? $" - {song.title}{(song.artists.Length != 0 ? $" by {song.artists
            .Select(y => y.artistsong?.character != null ? $"{y.artistsong.character} (CV: {y.name})" : y.name).Humanize()}" : "")}" : "")}";
    }
}

public class AnimeThemeResourceComparer : IComparer<AnimeThemeResource>
{
    public int Compare(AnimeThemeResource? x, AnimeThemeResource? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        if (x.type != y.type)
        {
            if (x.type == null) return -1;
            if (y.type == null) return 1;
            return x.type.Value.CompareTo(y.type.Value);
        }

        if (x.sequence != y.sequence)
        {
            if (x.sequence == null) return -1;
            if (y.sequence == null) return 1;
            return x.sequence.Value.CompareTo(y.sequence.Value);
        }

        return 0;
    }
}


public class SongResource
{
    /// <summary>
    /// The primary key of the resource
    /// </summary>
    public int id;

    /// <summary>
    /// The name of the composition
    /// </summary>
    public string title;

    /// <summary>
    /// The date that the resource was created
    /// </summary>
    public DateTime? created_at;

    /// <summary>
    /// The date that the resource was last modified
    /// </summary>
    public DateTime? updated_at;

    /// <summary>
    /// The date that the resource was deleted
    /// </summary>
    public DateTime? deleted_at;

    // includes
    public SongArtistResource[]? artists;
}

public class SongArtistResource
{
    /// <summary>
    /// The primary key of the resource
    /// </summary>
    public int id;

    /// <summary>
    /// The primary title of the artist
    /// </summary>
    public string name;

    /// <summary>
    /// The URL slug & route key of the resource
    /// </summary>
    public string slug;

    public SongArtistAsResource? artistsong;

    /// <summary>
    /// The date that the resource was created
    /// </summary>
    public DateTime? created_at;

    /// <summary>
    /// The date that the resource was last modified
    /// </summary>
    public DateTime? updated_at;

    /// <summary>
    /// The date that the resource was deleted
    /// </summary>
    public DateTime? deleted_at;
}

public class SongArtistAsResource
{
    [JsonProperty("as")]
    public string? character;
}

public class AnimeThemeEntryResource
{
    /// <summary>
    /// The primary key of the resource
    /// </summary>
    public int id;

    /// <summary>
    /// The version number of the theme
    /// </summary>
    public int? version;

    /// <summary>
    /// The episodes that the theme is used for
    /// </summary>
    public string? episodes;

    /// <summary>
    /// Is not safe for work content included?
    /// </summary>
    public bool? nsfw;

    /// <summary>
    /// Is content included that may spoil the viewer?
    /// </summary>
    public bool? spoiler;

    /// <summary>
    /// Any additional information for this sequence
    /// </summary>
    public string? notes;

    /// <summary>
    /// The date that the resource was created
    /// </summary>
    public DateTime? created_at;

    /// <summary>
    /// The date that the resource was last modified
    /// </summary>
    public DateTime? updated_at;

    /// <summary>
    /// The date that the resource was deleted
    /// </summary>
    public DateTime? deleted_at;

    // includes
    public VideoResource[]? videos;

    public override string ToString()
    {
        List<string> labels = [];

        if (nsfw.HasValue && nsfw.Value)
        {
            labels.Add("NSFW");
        }

        if (spoiler.HasValue && spoiler.Value)
        {
            labels.Add("Spoiler");
        }

        var warnings = "";

        if (labels.Count != 0)
        {
            warnings = $"({labels.Humanize()}) ";
        }

        return $"{warnings}v{(version.HasValue ? version.Value : "1")} - episodes {episodes}";
    }
}

public class VideoResource
{
    /// <summary>
    /// The primary key of the resource
    /// </summary>
    public int id;

    /// <summary>
    /// The basename of the file in storage
    /// </summary>
    public string? basename;

    /// <summary>
    /// The filename of the file in storage
    /// </summary>
    public string? filename;

    /// <summary>
    /// The path of the file in storage
    /// </summary>
    public string? path;

    /// <summary>
    /// The size of the file in storage in Bytes
    /// </summary>
    public int? size;

    /// <summary>
    /// The media type of the file in storage
    /// </summary>
    public string? mimetype;

    /// <summary>
    /// The frame height of the file in storage
    /// </summary>
    public int? resolution;

    /// <summary>
    /// Is the video creditless?
    /// </summary>
    public bool? nc;

    /// <summary>
    /// Does the video include subtitles of dialogue?
    /// </summary>
    public bool? subbed;

    /// <summary>
    /// Does the video include subtitles of song lyrics?
    /// </summary>
    public bool? lyrics;

    /// <summary>
    /// Is the video an uncensored version of a censored sequence?
    /// </summary>
    public bool? uncen;

    /// <summary>
    /// Where did this video come from? [WEB, RAW, BD, DVD, VHS, LD]
    /// </summary>
    public SourceEnum? source;

    /// <summary>
    /// The degree to which the sequence and episode content overlap [None, Transition, Over]
    /// </summary>
    public OverlapEnum? overlap;

    /// <summary>
    /// The attributes used to distinguish the file within the context of a theme
    /// </summary>
    public string? tags;

    /// <summary>
    /// The URL to stream the file from storage
    /// </summary>
    public string? link;

    /// <summary>
    /// The number of views recorded for the resource
    /// </summary>
    public int? views_count;

    /// <summary>
    /// The date that the resource was created
    /// </summary>
    public DateTime? created_at;

    /// <summary>
    /// The date that the resource was last modified
    /// </summary>
    public DateTime? updated_at;

    /// <summary>
    /// The date that the resource was deleted
    /// </summary>
    public DateTime? deleted_at;

    public override string ToString()
    {
        // Initialize a list to hold the parts of the string representation
        List<string> parts =
        [
            $"{resolution}p",
            $"{source}"
        ];

        // Add optional fields only if they are not the default value
        if (nc.HasValue && nc.Value)
        {
            parts.Add("No Credits");
        }
        if (subbed.HasValue && subbed.Value)
        {
            parts.Add("Subbed");
        }
        if (lyrics.HasValue && lyrics.Value)
        {
            parts.Add("Lyrics");
        }
        if (uncen.HasValue && uncen.Value)
        {
            parts.Add("Uncensored");
        }
        if (overlap.HasValue && overlap.Value != OverlapEnum.None)
        {
            parts.Add($"{overlap}");
        }

        // Join the parts into a single string with commas separating the parts
        return string.Join(", ", parts);
    }


    public enum SourceEnum
    {
        WEB,
        RAW,
        BD,
        DVD,
        VHS,
        LD
    }

    public enum OverlapEnum
    {
        None,
        Transition,
        Over
    }
}