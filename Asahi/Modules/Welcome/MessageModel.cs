using Newtonsoft.Json;

namespace Asahi.Modules.Welcome;

public class MessageModel
{
    [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
    public string? Content { get; set; }

    [JsonProperty("username", NullValueHandling = NullValueHandling.Ignore)]
    public string? Username { get; set; }

    [JsonProperty("avatar_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? AvatarUrl { get; set; }

    [JsonProperty("tts", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Tts { get; set; }

    [JsonProperty("embeds", NullValueHandling = NullValueHandling.Ignore)]
    public RichEmbedModel[]? Embeds { get; set; }
    
    [JsonProperty("allowed_mentions", NullValueHandling = NullValueHandling.Ignore)]
    public AllowedMentionsModel? AllowedMentions { get; set; }
    
    [JsonProperty("flags", NullValueHandling = NullValueHandling.Ignore)]
    public MessageFlags MessageFlags { get; set; }
}

public class AllowedMentionsModel
{
    [JsonProperty("parse")]
    public string[] Parse { get; set; } = [];

    [JsonProperty("roles")]
    public List<ulong> Roles { get; set; } = [];

    [JsonProperty("users")]
    public List<ulong> Users { get; set; } = [];

    [JsonProperty("replied_user")]
    public bool MentionRepliedUser { get; set; } = false;
}

public static class AllowedMentionsModelExtensions
{
    public static AllowedMentions ToAllowedMentions(this AllowedMentionsModel? thing)
    {
        if (thing == null)
        {
            return new AllowedMentions()
            {
                AllowedTypes = AllowedMentionTypes.Users
            };
        }
        
        var allowedMentionTypes = AllowedMentionTypes.None;

        foreach (var parse in thing.Parse)
        {
            if (parse == "roles")
            {
                allowedMentionTypes |= AllowedMentionTypes.Roles;
            }
            else if (parse == "users")
            {
                allowedMentionTypes |= AllowedMentionTypes.Users;
            }
            else if (parse == "everyone")
            {
                allowedMentionTypes |= AllowedMentionTypes.Everyone;
            }
        }

        var allowedMentions = new AllowedMentions(allowedMentionTypes);
        
        allowedMentions.RoleIds = thing.Roles;
        allowedMentions.UserIds = thing.Users;
        allowedMentions.MentionRepliedUser = thing.MentionRepliedUser;

        return allowedMentions;
    }
}

public class RichEmbedModel
{
    [JsonProperty("author", NullValueHandling = NullValueHandling.Ignore)]
    public AuthorModel? Author { get; set; }

    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }

    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public string? Url { get; set; }

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)]
    public uint? Color { get; set; }

    [JsonProperty("fields", NullValueHandling = NullValueHandling.Ignore)]
    public FieldModel[]? Fields { get; set; }

    [JsonProperty("thumbnail", NullValueHandling = NullValueHandling.Ignore)]
    public ImageModel? Thumbnail { get; set; }

    [JsonProperty("image", NullValueHandling = NullValueHandling.Ignore)]
    public ImageModel? Image { get; set; }

    [JsonProperty("footer", NullValueHandling = NullValueHandling.Ignore)]
    public FooterModel? Footer { get; set; }
    
    [JsonProperty("timestamp", NullValueHandling = NullValueHandling.Ignore)]
    public DateTimeOffset? Timestamp { get; set; }

    public EmbedBuilder ToEmbedBuilder()
    {
        var eb = new EmbedBuilder();
        
        eb.Author = Author?.ToAuthorBuilder();
        eb.Title = Title;
        eb.Url = Url;
        eb.Description = Description;
        eb.Color = Color == null ? null : new Color(Color.Value);
        eb.Fields = Fields?.Select(x => x.ToFieldBuilder()).ToList() ?? [];
        eb.ThumbnailUrl = Thumbnail?.Url;
        eb.ImageUrl = Image?.Url;
        eb.Footer = Footer?.ToFooterBuilder();
        eb.Timestamp = Timestamp;

        return eb;
    }
}

public class AuthorModel
{
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public string? Url { get; set; }

    [JsonProperty("icon_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? IconUrl { get; set; }

    public EmbedAuthorBuilder ToAuthorBuilder()
    {
        var eab = new EmbedAuthorBuilder();
        
        eab.Name = Name;
        eab.Url = Url;
        eab.IconUrl = IconUrl;

        return eab;
    }
}

public class FieldModel
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("value")]
    public string Value { get; set; } = "";

    [JsonProperty("inline")]
    public bool Inline { get; set; } = false;

    public EmbedFieldBuilder ToFieldBuilder()
    {
        var fb = new EmbedFieldBuilder();
        
        fb.Name = Name;
        fb.Value = Value;
        fb.IsInline = Inline;

        return fb;
    }
}

public class FooterModel
{
    [JsonProperty("text")]
    public string Text { get; set; } = "";

    [JsonProperty("icon_url")]
    public string? IconUrl { get; set; }

    public EmbedFooterBuilder ToFooterBuilder()
    {
        var efb = new EmbedFooterBuilder();
        
        efb.Text = Text;
        efb.IconUrl = IconUrl;

        return efb;
    }
}

public class ImageModel
{
    [JsonProperty("url")]
    public string Url { get; set; }
}
