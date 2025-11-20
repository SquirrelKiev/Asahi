using System.Diagnostics.Contracts;
using System.Text.RegularExpressions;
using Asahi.Modules.Highlights;

namespace Asahi;

public static partial class CompiledRegex
{
    //[GeneratedRegex(@"https?:\/\/(?:www\.)?reddit\.com\/r\/([0-9A-Za-z_]+)(?:\/[a-z]*)?\.json")]
    [GeneratedRegex(@"^reddit: ?(?<type>.)(?:\/(?<subreddit>[0-9A-Za-z_]+))$")]
    public static partial Regex RedditFeedRegex();
    
    [GeneratedRegex(@"^danbooru: (?<tags>.*)$")]
    public static partial Regex DanbooruFeedRegex();

    [GeneratedRegex(@"^[\w-]+$")]
    public static partial Regex IsValidIdRegex();
    
    [GeneratedRegex(@"^https:\/\/(?:\w*.)?discord.com\/channels\/(?<guild>\d*)\/(?<channel>\d*)\/(?<message>\d*)$")]
    public static partial Regex MessageLinkRegex();
    
    [GeneratedRegex(@"```cs\n([\s\S]+?)\n```")]
    public static partial Regex CsharpCodeBlock();

    [GeneratedRegex("""
                    href="(https:\/\/nyaa\.si/view/\d+)"
                    """)]
    public static partial Regex NyaaATagRegex();

    [GeneratedRegex(@"^https:\/\/bsky\.app\/profile\/[a-zA-Z0-9:\.]*\/rss")]
    public static partial Regex BskyPostRegex();
    
    [GeneratedRegex(@"^https:\/\/openrss\.org\/bsky\.app\/profile\/[a-zA-Z0-9:\.]*")]
    public static partial Regex OpenRssBskyPostRegex();

    [GeneratedRegex(@"^https:\/\/i\.pximg\.net\/.+\/(\d+).+\.([a-z]*)")]
    public static partial Regex ValidPixivDirectImageUrlRegex();

    [GeneratedRegex(@"^https:\/\/(?:www\.)?(?:twitter|x)\.com\/[a-zA-Z0-9_]+\/status\/(\d+)(?:\?s=\d+)?$")]
    public static partial Regex TwitterStatusIdRegex();

    [GeneratedRegex(@"^https:\/\/[a-zA-Z0-9]+\.fanbox.cc")]
    public static partial Regex IsAFanboxLinkRegex();
    
    [GeneratedRegex(@"^https:\/\/[a-zA-Z0-9]+\.lofter.com")]
    public static partial Regex IsALofterLinkRegex();

    [GeneratedRegex(@"^https:\/\/c\.fantia\.jp/uploads/post/file/(\d+)\/")]
    public static partial Regex FantiaPostIdRegex();

    [GeneratedRegex(@"^https:\/\/misskey\.io\/notes\/([a-z0-9]+)\/?$")]
    public static partial Regex MisskeyNoteRegex();

    [Pure]
    public static MessageIdInfo? ParseMessageLink(string messageLink)
    {
        var messageProcessed = MessageLinkRegex().Match(messageLink);

        if (!messageProcessed.Success)
        {
            return null;
        }

        var guildId = ulong.Parse(messageProcessed.Groups["guild"].Value);
        var channelId = ulong.Parse(messageProcessed.Groups["channel"].Value);
        var messageId = ulong.Parse(messageProcessed.Groups["message"].Value);

        return new MessageIdInfo(guildId, channelId, messageId);
    }
}
