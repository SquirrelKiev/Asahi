using System.Text.RegularExpressions;

namespace Asahi;

public static partial class CompiledRegex
{
    //[GeneratedRegex(@"https?:\/\/(?:www\.)?reddit\.com\/r\/([0-9A-Za-z_]+)(?:\/[a-z]*)?\.json")]
    [GeneratedRegex(@"reddit:(?<type>.)(?:\/(?<subreddit>[0-9A-Za-z_]+))")]
    public static partial Regex RedditFeedRegex();
}