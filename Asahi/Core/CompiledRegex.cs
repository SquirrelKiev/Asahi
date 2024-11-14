using System.Text.RegularExpressions;

namespace Asahi;

public static partial class CompiledRegex
{
    //[GeneratedRegex(@"https?:\/\/(?:www\.)?reddit\.com\/r\/([0-9A-Za-z_]+)(?:\/[a-z]*)?\.json")]
    [GeneratedRegex(@"^reddit:(?<type>.)(?:\/(?<subreddit>[0-9A-Za-z_]+))$")]
    public static partial Regex RedditFeedRegex();

    [GeneratedRegex(@"^[\w-]+$")]
    public static partial Regex IsValidIdRegex();
    
    [GeneratedRegex(@"^https:\/\/(?:\w*.)?discord.com\/channels\/(?<guild>\d*)\/(?<channel>\d*)\/(?<message>\d*)$")]
    public static partial Regex MessageLinkRegex();
    
    [GeneratedRegex(@"```cs\n([\s\S]+?)\n```")]
    public static partial Regex CsharpCodeBlock();

    // https://regexr.com/3dqa0
    [GeneratedRegex(@"^(?:https?:\/\/)?(?:[\da-z\.-]+\.[a-z\.]{2,6}|[\d\.]+)(?:[\/:?=&#]{1}[\da-z\.-]+)*[\/\?]?$", RegexOptions.IgnoreCase)]
    public static partial Regex GenericLinkRegex();

    [GeneratedRegex("""
                    href="(https:\/\/nyaa\.si/view/\d+)"
                    """)]
    public static partial Regex NyaaATagRegex();

    [GeneratedRegex("https:\\/\\/bsky\\.app\\/profile\\/[a-zA-Z0-9:\\.]*\\/rss")]
    public static partial Regex BskyPostRegex();
}
