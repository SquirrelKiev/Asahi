using System.Text.RegularExpressions;

namespace Asahi;

public static partial class CompiledRegex
{
    //[GeneratedRegex(@"https?:\/\/(?:www\.)?reddit\.com\/r\/([0-9A-Za-z_]+)(?:\/[a-z]*)?\.json")]
    [GeneratedRegex(@"reddit:(?<type>.)(?:\/(?<subreddit>[0-9A-Za-z_]+))")]
    public static partial Regex RedditFeedRegex();

    [GeneratedRegex(@"^[\w-]+$")]
    public static partial Regex IsValidId();
    
    [GeneratedRegex(@"https:\/\/(?:canary)?.discord.com\/channels\/[0-9]*\/[0-9]*\/([0-9]*)")]
    public static partial Regex MessageLinkRegex();
    
    [GeneratedRegex(@"```cs\n([\s\S]+?)\n```")]
    public static partial Regex CsharpCodeBlock();
}