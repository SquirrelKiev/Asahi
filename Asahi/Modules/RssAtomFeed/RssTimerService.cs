﻿using System.Net.Http.Headers;
using Asahi.Database;
using Asahi.Modules.RssAtomFeed.Models;
using CodeHollow.FeedReader;
using Discord.Rest;
using Discord.Webhook;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Asahi.Modules.RssAtomFeed;

[Inject(ServiceLifetime.Singleton)]
public class RssTimerService(
    IHttpClientFactory clientFactory,
    IDbService dbService,
    DiscordSocketClient client,
    ILogger<RssTimerService> logger,
    BotConfig config,
    DiscordRestConfig webhookRestConfig,
    IRedditApi redditApi,
    IFxTwitterApi fxTwitterApi
)
{
    public enum FeedHandler
    {
        RssAtom,
        Danbooru,
        Reddit,
        Nyaa,
        Bsky,
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
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            logger.LogCritical(
                e,
                "Unhandled exception in TimerTask! Except much worse because this was outside of the loop!!"
            );
        }
    }

    public async Task PollFeeds()
    {
        await using var context = dbService.GetDbContext();

        var feeds = await context.RssFeedListeners.ToArrayAsync();

        var feedsGrouped = feeds.GroupBy(x => x.FeedUrl);
        using var http = clientFactory.CreateClient();
        http.MaxResponseContentBufferSize = 8000000;
        
        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/atom+xml"));
        http.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/rss+xml"));
        http.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("text/xml"));
        http.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/xml"));

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

                IEmbedGeneratorAsync? embedGenerator;
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
                            logger.LogWarning(
                                "Unknown channel {channel}! Guild {guildId}, ID {feedListenerId}",
                                feedListener.ChannelId,
                                feedListener.GuildId,
                                feedListener.Id
                            );
                            continue;
                        }

                        // TODO: need to handle this better so error handling doesnt cause the send the top 10 bug
                        var messages = await embedGenerator!
                            .GenerateFeedItemMessages(
                                feedListener,
                                seenArticles,
                                processedArticles,
                                QuotingHelpers.GetUserRoleColorWithFallback(
                                    guild.CurrentUser,
                                    Color.Default
                                ),
                                !unseenUrl
                            )
                            .ToArrayAsync();

                        if (
                            messages.All(x =>
                                x.embeds is { Length: > 0 } && x.embeds[0].Timestamp.HasValue
                            )
                        )
                        {
                            messages = messages
                                .OrderByDescending(x => x.embeds![0].Timestamp.HasValue)
                                .ToArray();
                        }

                        if (messages.Length == 0)
                            continue;

                        // this may look wasteful (only taking the top 10) but im trying to avoid some feed with like 100 new contents ruining the rate limits
                        // Also doing this after the ToArray so that the reads are marked correctly
                        var threadChannel = channel as SocketThreadChannel;
                        if (feedListener.WebhookName != null)
                        {
                            var webhookCh =
                                threadChannel != null
                                    ? threadChannel.ParentChannel as IIntegrationChannel
                                    : channel;
                            if (webhookCh != null)
                            {
                                var webhook = await webhookCh.GetOrCreateWebhookAsync(
                                    feedListener.WebhookName,
                                    client.CurrentUser
                                );

                                webhookClient = new DiscordWebhookClient(
                                    webhook.Id,
                                    webhook.Token,
                                    webhookRestConfig
                                );
                                webhookClient.Log += msg => BotService.Client_Log(logger, msg);
                            }
                        }

                        foreach (var message in messages.Take(10).Reverse())
                        {
                            if (webhookClient != null)
                            {
                                await webhookClient.SendMessageAsync(
                                    message.body,
                                    embeds: message.embeds,
                                    components: message.components,
                                    threadId: threadChannel?.Id
                                );
                            }
                            else
                            {
                                await channel.SendMessageAsync(
                                    message.body,
                                    embeds: message.embeds,
                                    components: message.components
                                );
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
                        logger.LogWarning(
                            ex,
                            "Failed to send feed {url} to guild {guildId}, channel {channelId}",
                            feedGroup.Key,
                            feedListener.GuildId,
                            feedListener.ChannelId
                        );
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
    }

    public async Task<(
        bool isSuccess,
        IEmbedGeneratorAsync? embedGenerator
        )> TryGetEmbedGeneratorForFeed(string url, string? reqContent)
    {
        var feedHandler = FeedHandlerForUrl(url);
        IEmbedGeneratorAsync? embedGenerator;
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

                embedGenerator = new DanbooruMessageGenerator(posts, fxTwitterApi, config);
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
            case FeedHandler.Nyaa:
            {
                var feed = FeedReader.ReadFromString(reqContent);

                IEnumerable<FeedItem> feedsEnumerable = feed.Items;
                var feedsArray = feedsEnumerable.ToArray();

                embedGenerator = new NyaaFeedMessageGenerator(feed, feedsArray);
                break;
            }
            case FeedHandler.Bsky:
            {
                var feed = FeedReader.ReadFromString(reqContent);

                IEnumerable<FeedItem> feedsEnumerable = feed.Items;
                var feedsArray = feedsEnumerable.ToArray();

                embedGenerator = new BskyMessageGenerator(feedsArray);
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
        if (url.StartsWith("https://nyaa.si"))
            return FeedHandler.Nyaa;
        if (CompiledRegex.BskyPostRegex().IsMatch(url) || CompiledRegex.OpenRssBskyPostRegex().IsMatch(url))
            return FeedHandler.Bsky;
        if (CompiledRegex.RedditFeedRegex().IsMatch(url))
            return FeedHandler.Reddit;

        return FeedHandler.RssAtom;
    }

    public static bool ValidateFeed(Feed? feed)
    {
        return feed?.Type != FeedType.Unknown;
    }
}
