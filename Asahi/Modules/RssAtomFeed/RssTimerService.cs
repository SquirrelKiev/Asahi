using Asahi.Database;
using Asahi.Database.Models.Rss;
using Asahi.Modules.RssAtomFeed.Models;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using Discord.Webhook;
using Discord.WebSocket;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Asahi.Modules.RssAtomFeed;

[Inject(ServiceLifetime.Singleton)]
public class RssTimerService(IHttpClientFactory clientFactory, DbService dbService, DiscordSocketClient client, ILogger<RssTimerService> logger,
    BotConfig config, IRedditApi redditApi)
{
    public enum FeedHandler
    {
        RssAtom,
        Danbooru,
        Reddit
    }

    public Task? timerTask;

    private readonly Dictionary<int, HashSet<int>> hashedSeenArticles = [];

    public void StartBackgroundTask(CancellationToken token)
    {
        timerTask ??= Task.Run(() => TimerTask(token), token);
    }

    /// <remarks>Should only be one of these running!</remarks>
    private async Task TimerTask(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogTrace("RSS timer task started");
            try
            {
                await PollFeeds();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in TimerTask! {message}", ex.Message);
            }
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await PollFeeds();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled exception in TimerTask! {message}", ex.Message);
                }
            }
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            logger.LogCritical(e, "Unhandled exception in TimerTask! Except much worse because this was outside of the loop!!");
        }
    }

    public async Task PollFeeds()
    {
        await using var context = dbService.GetDbContext();

        var feeds = await context.RssFeedListeners.ToArrayAsync();

        var feedsGrouped = feeds.GroupBy(x => x.FeedUrl);
        using var http = clientFactory.CreateClient();
        http.MaxResponseContentBufferSize = 8000000;

        var channelsToPurgeFeedsOf = new List<ulong>();

        foreach (var feedGroup in feedsGrouped)
        {
            try
            {
                var url = feedGroup.Key;
                // doing hashes for memory reasons
                var urlHash = url.GetHashCode(StringComparison.OrdinalIgnoreCase);


                var unseenUrl = false;
                //logger.LogTrace("processing {url}", url);
                if (!hashedSeenArticles.TryGetValue(urlHash, out var seenArticles))
                {
                    logger.LogTrace("never seen the url {url} before", url);
                    unseenUrl = true;
                    seenArticles = [];
                    hashedSeenArticles.Add(urlHash, seenArticles);
                }

                string? reqContent = null;
                if (url.StartsWith("http://") || url.StartsWith("https://"))
                {
                    using HttpResponseMessage req = await http.GetAsync(url);
                    reqContent = await req.Content.ReadAsStringAsync();
                }

                var processedArticles = new HashSet<int>();

                IEmbedGenerator? embedGenerator;
                try
                {
                    var thing = await TryGetEmbedGeneratorForFeed(url, reqContent);
                    if (!thing.isSuccess)
                        continue;

                    embedGenerator = thing.embedGenerator;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to process feed {feedUrl}.", url);
                    continue;
                }

                //logger.LogTrace("seen articles is {seen}", string.Join(',', seenArticles.Select(x => x.ToString())));

                foreach (var feedListener in feedGroup)
                {
                    DiscordWebhookClient? webhookClient = null;
                    try
                    {
                        var guild = client.GetGuild(feedListener.GuildId);

                        if (guild.GetChannel(feedListener.ChannelId) is not ITextChannel channel)
                        {
                            logger.LogDebug("unknown channel {channel}, added to purge queue", feedListener.ChannelId);
                            channelsToPurgeFeedsOf.Add(feedListener.ChannelId);
                            continue;
                        }

                        var messages = embedGenerator!.GenerateFeedItemMessages(feedListener, seenArticles,
                                processedArticles,
                                QuotingHelpers.GetUserRoleColorWithFallback(guild.CurrentUser, Color.Default),
                                !unseenUrl)
                            .ToArray();

                        if (messages.All(x => x.embeds is { Length: > 0 } && x.embeds[0].Timestamp.HasValue))
                        {
                            messages = messages.OrderByDescending(x => x.embeds![0].Timestamp.HasValue).ToArray();
                        }

                        if (messages.Length == 0)
                            continue;

                        // this may look wasteful (only taking the top 10) but im trying to avoid some feed with like 100 new contents ruining the rate limits
                        // Also doing this after the ToArray so that the reads are marked correctly
                        var threadChannel = channel as SocketThreadChannel;
                        if (feedListener.WebhookName != null)
                        {
                            var webhookCh = threadChannel != null
                                ? threadChannel.ParentChannel as IIntegrationChannel
                                : channel;
                            if (webhookCh != null)
                            {
                                var webhook =
                                    await webhookCh.GetOrCreateWebhookAsync(feedListener.WebhookName,
                                        client.CurrentUser);

                                webhookClient = new DiscordWebhookClient(webhook.Id, webhook.Token, BotService.WebhookRestConfig);
                                webhookClient.Log += msg => BotService.Client_Log(logger, msg);
                            }
                        }

                        foreach (var message in messages.Take(10))
                        {
                            if (webhookClient != null)
                            {
                                await webhookClient.SendMessageAsync(message.body, embeds: message.embeds,
                                    components: message.components, threadId: threadChannel?.Id);
                            }
                            else
                            {
                                await channel.SendMessageAsync(message.body, embeds: message.embeds,
                                    components: message.components);
                            }
                        }

                        //foreach (var feedItem in feedsArray)
                        //{
                        //    if (unseenUrl || embeds.Count >= 10 ||
                        //        seenArticles.Contains(feedItem.Id.GetHashCode(StringComparison.Ordinal))) continue;

                        //    embeds.Add(GenerateFeedItemEmbed(feedItem, feed, feedListener, QuotingHelpers.GetUserRoleColorWithFallback(guild.CurrentUser, Color.Default)));
                        //}
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to send feed {url} to guild {guildId}, channel {channelId}",
                            feedGroup.Key, feedListener.GuildId, feedListener.ChannelId);
                    }
                    finally
                    {
                        webhookClient?.Dispose();
                    }
                }

                seenArticles.Clear();
                foreach (var article in processedArticles)
                {
                    seenArticles.Add(article);
                }

                //logger.LogTrace("seen articles is now {seen}",
                //    string.Join(',', seenArticles.Select(x => x.ToString())));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch feed {url}", feedGroup.Key);
            }
        }


        foreach (var channelId in channelsToPurgeFeedsOf)
        {
            await context.RssFeedListeners.Where(x => x.ChannelId == channelId).ExecuteDeleteAsync();
        }
    }

    public async Task<(bool isSuccess, IEmbedGenerator? embedGenerator)> TryGetEmbedGeneratorForFeed(string url, string? reqContent)
    {
        var feedHandler = FeedHandlerForUrl(url);
        IEmbedGenerator? embedGenerator;
        switch (feedHandler)
        {
            case FeedHandler.RssAtom:
                {
                    var feed = FeedReader.ReadFromString(reqContent);

                    if (!ValidateFeed(feed))
                    {
                        embedGenerator = null;
                        return (false, embedGenerator);
                    }

                    IEnumerable<FeedItem> feedsEnumerable = feed.Items;
                    var feedsArray = feedsEnumerable.ToArray();

                    embedGenerator = new RssFeedMessageGenerator(feed, feedsArray);
                    break;
                }
            case FeedHandler.Danbooru:
                {
                    var posts = JsonConvert.DeserializeObject<DanbooruPost[]>(reqContent!);

                    if (posts == null)
                    {
                        embedGenerator = null;
                        return (false, embedGenerator);
                    }

                    embedGenerator = new DanbooruMessageGenerator(posts, config);
                    break;
                }
            case FeedHandler.Reddit:
                {
                    var regex = CompiledRegex.RedditFeedRegex().Match(url);

                    var feedType = regex.Groups["type"].Value;

                    if (feedType != "r")
                    {
                        embedGenerator = null;
                        return (false, embedGenerator);
                    }

                    var subreddit = regex.Groups["subreddit"].Value;

                    var posts = await redditApi.GetSubredditPosts(subreddit);

                    embedGenerator = new RedditMessageGenerator(posts.Data.Children);
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException();
        }

        return (true, embedGenerator);
    }

    public static FeedHandler FeedHandlerForUrl(string url)
    {
        if (url.StartsWith("https://danbooru.donmai.us/posts.json"))
            return FeedHandler.Danbooru;
        if (CompiledRegex.RedditFeedRegex().IsMatch(url))
            return FeedHandler.Reddit;

        return FeedHandler.RssAtom;
    }

    public static bool ValidateFeed(Feed? feed)
    {
        return feed?.Type != FeedType.Unknown;
    }
}

public class RedditMessageGenerator(List<PostChild> posts) : IEmbedGenerator
{
    public IEnumerable<MessageContents> GenerateFeedItemMessages(FeedListener feedListener, HashSet<int> seenArticles, HashSet<int> processedArticles,
        Color embedColor, bool shouldCreateEmbeds)
    {
        foreach (var post in posts.Select(x => x.Data))
        {
            processedArticles.Add(post.Id.GetHashCode());

            if (seenArticles.Contains(post.Id.GetHashCode())) continue;
            if (!shouldCreateEmbeds) continue;

            yield return GenerateFeedItemMessage(feedListener, post, embedColor);
        }
    }

    private MessageContents GenerateFeedItemMessage(FeedListener feedListener, Post post, Color embedColor)
    {
        // TODO: this is uber lazy, don't do this
        if (post.Spoiler)
            return new MessageContents($"|| https://www.rxddit.com{post.Permalink} ||");
        else
            return new MessageContents($"https://www.rxddit.com{post.Permalink}");

        //var eb = new EmbedBuilder();

        //eb.WithColor(embedColor);

        //var footer = new EmbedFooterBuilder();
        //// TODO: Customisable per feed :chatting:
        //footer.WithIconUrl("https://www.redditstatic.com/icon.png");
        //if (!string.IsNullOrWhiteSpace(feedListener?.FeedTitle))
        //{
        //    footer.WithText($"{feedListener.FeedTitle}");
        //}

        //eb.WithFooter(footer);
        //eb.WithTimestamp(DateTimeOffset.FromUnixTimeSeconds(post.CreatedUtc));

        //return new MessageContents();
    }
}

public class DanbooruMessageGenerator(DanbooruPost[] posts, BotConfig config) : IEmbedGenerator
{
    private static readonly HashSet<string> KnownImageExtensions = ["jpg", "jpeg", "png", "gif", "bmp", "webp"];

    public IEnumerable<MessageContents> GenerateFeedItemMessages(FeedListener feedListener, HashSet<int> seenArticles, HashSet<int> processedArticles,
        Color embedColor, bool shouldCreateEmbeds)
    {
        foreach (var post in posts)
        {
            processedArticles.Add(post.Id);

            if (seenArticles.Contains(post.Id)) continue;
            if (!shouldCreateEmbeds) continue;

            yield return GenerateFeedItemMessage(feedListener, post, embedColor);
        }
    }

    private MessageContents GenerateFeedItemMessage(FeedListener? feedListener, DanbooruPost post, Color embedColor)
    {
        var eb = new EmbedBuilder();

        eb.WithColor(embedColor);
        if (!string.IsNullOrWhiteSpace(post.TagStringArtist))
        {
            eb.WithAuthor(post.TagStringArtist.Split(' ').Humanize());
        }

        var footer = new EmbedFooterBuilder();
        footer.WithIconUrl("https://danbooru.donmai.us/packs/static/danbooru-logo-128x128-ea111b6658173e847734.png");
        if (!string.IsNullOrWhiteSpace(feedListener?.FeedTitle))
        {
            footer.WithText($"{feedListener.FeedTitle} • Rating: {post.Rating}");
        }

        eb.WithFooter(footer);
        eb.WithTimestamp(post.CreatedAt);

        eb.WithTitle(!string.IsNullOrWhiteSpace(post.TagStringCharacter)
            ? post.TagStringCharacter.Split(' ').Select(x => x.Titleize()).Humanize()
            : "Danbooru");

        eb.WithUrl($"https://danbooru.donmai.us/posts/{post.Id}/");

        var bestVariant = GetBestVariant(post.MediaAsset.Variants);
        if (bestVariant != null)
        {
            eb.WithImageUrl(bestVariant.Url);
        }

        eb.WithDescription($"{post.MediaAsset.FileExtension.ToUpperInvariant()} file | " +
                           $"embed is {bestVariant?.Type} quality{(bestVariant?.Type != "original" ? $" ({bestVariant?.FileExt.ToUpperInvariant()} file)" : "")}");

        var components = new ComponentBuilder();

        if (post.PixivId != null)
        {
            QuotingHelpers.TryParseEmote(config.PixivEmote, out var pixivEmote);

            var pixivUrl = $"https://www.pixiv.net/artworks/{post.PixivId}";
            components.WithButton("Pixiv", emote: pixivEmote, url: pixivUrl, style: ButtonStyle.Link);
        }
        else if (!string.IsNullOrWhiteSpace(post.Source) && CompiledRegex.GenericLinkRegex().IsMatch(post.Source))
        {
            components.WithButton("Source", url: post.Source, style: ButtonStyle.Link);
        }

        return new MessageContents(eb, components);
    }

    public static DanbooruVariant? GetBestVariant(DanbooruVariant[] variants)
    {
        // we only want embeddable variants
        var validVariants = variants.Where(v => KnownImageExtensions.Contains(v.FileExt.ToLower())).ToArray();

        // original is the ideal pick here
        var originalVariant = validVariants.FirstOrDefault(v => v.Type == "original");

        if (originalVariant != null)
        {
            return originalVariant;
        }

        // original doesn't exist/work oh god lets just hope the rest of the options are ok
        return validVariants.MaxBy(v => v.Width * v.Height);
    }
}

public class RssFeedMessageGenerator(Feed genericFeed, FeedItem[] feedItems) : IEmbedGenerator
{
    public IEnumerable<MessageContents> GenerateFeedItemMessages(FeedListener feedListener, HashSet<int> seenArticles, HashSet<int> processedArticles, Color embedColor, bool shouldCreateEmbeds)
    {
        foreach (var feedItem in feedItems)
        {
            processedArticles.Add(feedItem.Id.GetHashCode(StringComparison.Ordinal));

            if (seenArticles.Contains(feedItem.Id.GetHashCode(StringComparison.Ordinal))) continue;
            if (!shouldCreateEmbeds) continue;

            yield return GenerateFeedItemEmbed(feedListener, feedItem, embedColor);
        }
    }

    public MessageContents GenerateFeedItemEmbed(FeedListener feedListener, FeedItem genericItem, Color embedColor)
    {
        var eb = new EmbedBuilder();

        switch (genericFeed.Type)
        {
            case FeedType.Atom:
                {
                    var feed = (AtomFeed)genericFeed.SpecificFeed;
                    var item = (AtomFeedItem)genericItem.SpecificItem;

                    var footer = new EmbedFooterBuilder();

                    if (item.Author != null)
                    {
                        eb.WithAuthor(item.Author.ToString(), url: !string.IsNullOrEmpty(item.Author.Uri) ? item.Author.Uri : null);
                    }

                    if (!string.IsNullOrWhiteSpace(item.Summary))
                    {
                        eb.WithDescription(item.Summary);
                    }

                    if (!string.IsNullOrWhiteSpace(item.Title))
                    {
                        eb.WithTitle(item.Title);
                    }

                    if (!string.IsNullOrWhiteSpace(item.Link))
                    {
                        eb.WithUrl(item.Link);
                    }

                    if (item.PublishedDate != null)
                    {
                        eb.WithTimestamp(item.PublishedDate.Value);
                    }
                    else if (item.UpdatedDate != null)
                    {
                        eb.WithTimestamp(item.UpdatedDate.Value);
                    }

                    // general feed stuff
                    if (!string.IsNullOrWhiteSpace(feed.Icon))
                    {
                        footer.IconUrl = feed.Icon;

                        // stupid ass bug
                        if (footer.IconUrl == "https://www.redditstatic.com/icon.png/")
                        {
                            footer.IconUrl = "https://www.redditstatic.com/icon.png";
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(feedListener.FeedTitle))
                    {
                        footer.Text = $"{feedListener.FeedTitle} • {item.Id}";
                    }
                    else if (!string.IsNullOrWhiteSpace(feed.Title))
                    {
                        footer.Text = $"{feed.Title} • {item.Id}";
                    }

                    eb.WithFooter(footer);

                    break;
                }
            case FeedType.Rss_1_0:
            case FeedType.Rss_2_0:
            case FeedType.MediaRss:
            case FeedType.Rss:
            case FeedType.Rss_0_91:
            case FeedType.Rss_0_92:
                {
                    var footer = new EmbedFooterBuilder();

                    if (!string.IsNullOrWhiteSpace(genericItem.Author))
                    {
                        eb.WithAuthor(genericItem.Author);
                    }

                    if (!string.IsNullOrWhiteSpace(genericItem.Description))
                    {
                        eb.WithDescription(genericItem.Description);
                    }

                    if (!string.IsNullOrWhiteSpace(genericItem.Title))
                    {
                        eb.WithTitle(genericItem.Title);
                    }

                    if (!string.IsNullOrWhiteSpace(genericItem.Link))
                    {
                        eb.WithUrl(genericItem.Link);
                    }

                    if (genericItem.PublishingDate.HasValue)
                    {
                        eb.WithTimestamp(genericItem.PublishingDate.Value);
                    }

                    // general feed stuff
                    if (!string.IsNullOrWhiteSpace(genericFeed.ImageUrl))
                    {
                        eb.WithThumbnailUrl(genericFeed.ImageUrl);
                    }

                    if (!string.IsNullOrWhiteSpace(feedListener.FeedTitle))
                    {
                        footer.Text = $"{feedListener.FeedTitle} • {genericItem.Id}";
                    }
                    else if (!string.IsNullOrWhiteSpace(genericFeed.Title))
                    {
                        footer.Text = $"{genericFeed.Title} • {genericItem.Id}";
                    }

                    eb.WithFooter(footer);

                    break;
                }
            case FeedType.Unknown:
            default:
                throw new NotSupportedException();
        }

        var thumbnail = genericItem.SpecificItem.Element.Descendants().FirstOrDefault(x =>
                x.Name.LocalName == "content" && x.Attribute("type")?.Value == "xhtml")?
            .Descendants().FirstOrDefault(x => x.Name.LocalName == "img")?
            .Attributes().FirstOrDefault(x => x.Name == "src")?.Value;

        thumbnail ??=
            genericItem.SpecificItem.Element.Descendants().FirstOrDefault(x => x.Name.LocalName.Contains("thumbnail", StringComparison.InvariantCultureIgnoreCase))?
                .Attribute("url")?.Value;

        if (!string.IsNullOrWhiteSpace(thumbnail))
        {
            eb.WithImageUrl(thumbnail);
        }

        eb.WithColor(embedColor);

        if (!string.IsNullOrWhiteSpace(eb.Title))
            eb.Title = eb.Title.Truncate(200);

        if (!string.IsNullOrWhiteSpace(eb.Description))
            eb.Description = eb.Description.Truncate(400);

        return new MessageContents(eb);
    }
}

public interface IEmbedGenerator
{
    /// <summary>
    /// Returns an IEnumerable of all the embeds for that feed's items.
    /// </summary>
    /// <param name="feedListener">The listener.</param>
    /// <param name="seenArticles">The previously seen articles from the last run. Will not be edited.</param>
    /// <param name="processedArticles">The current work in progress articles that have been processed. Will be edited.</param>
    /// <param name="embedColor">The color to use for the embed.</param>
    /// <returns></returns>
    public IEnumerable<MessageContents> GenerateFeedItemMessages(FeedListener feedListener, HashSet<int> seenArticles, HashSet<int> processedArticles, Color embedColor, bool shouldCreateEmbeds);
}
