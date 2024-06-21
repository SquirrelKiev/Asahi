﻿using Asahi.Database;
using Asahi.Database.Models.Rss;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.RssAtomFeed;

[Inject(ServiceLifetime.Singleton)]
public class RssTimerService(IHttpClientFactory clientFactory, DbService dbService, DiscordSocketClient client, ILogger<RssTimerService> logger)
{
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

    // TODO: Error handling
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
                    //logger.LogTrace("never seen the url {url} before", url);
                    unseenUrl = true;
                    seenArticles = [];
                    hashedSeenArticles.Add(urlHash, seenArticles);
                }

                using var req = await http.GetAsync(url);
                var xml = await req.Content.ReadAsStringAsync();

                var feed = FeedReader.ReadFromString(xml);

                if (!ValidateFeed(feed))
                    continue;

                var processedArticles = new HashSet<int>();

                //logger.LogTrace("seen articles is {seen}", string.Join(',',seenArticles.Select(x => x.ToString())));

                IEnumerable<FeedItem> feedsEnumerable = feed.Items;
                if (feed.Items.All(x => x.PublishingDate.HasValue))
                {
                    feedsEnumerable = feedsEnumerable.OrderByDescending(x => x.PublishingDate);
                }
                var feedsArray = feedsEnumerable.ToArray();

                foreach (var feedListener in feedGroup)
                {
                    try
                    {
                        var guild = client.GetGuild(feedListener.GuildId);

                        if (guild.GetChannel(feedListener.ChannelId) is not ISocketMessageChannel channel)
                        {
                            channelsToPurgeFeedsOf.Add(feedListener.ChannelId);
                            continue;
                        }

                        var embeds = new List<Embed>();
                        foreach (var feedItem in feedsArray)
                        {
                            if (!unseenUrl && embeds.Count < 10 &&
                                !seenArticles.Contains(feedItem.Id.GetHashCode(StringComparison.Ordinal)))
                                embeds.Add(GenerateFeedItemEmbed(feedItem, feed, feedListener, QuotingHelpers.GetUserRoleColorWithFallback(guild.CurrentUser, Color.Default)));
                        }

                        if (embeds.Count != 0)
                            await channel.SendMessageAsync(embeds: embeds.Take(10).ToArray());
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to send feed {url} to guild {guildId}, channel {channelId}", 
                            feedGroup.Key, feedListener.GuildId, feedListener.ChannelId);
                    }
                }

                foreach (var feedItem in feedsArray)
                {
                    processedArticles.Add(feedItem.Id.GetHashCode(StringComparison.Ordinal));
                }

                //logger.LogTrace("seen articles is now {seen}",
                //    string.Join(',', seenArticles.Select(x => x.ToString())));

                seenArticles.Clear();
                foreach (var article in processedArticles)
                {
                    seenArticles.Add(article);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch feed {url}", feedGroup.Key);
            }
        }

        if (channelsToPurgeFeedsOf.Count != 0)
        {
            IQueryable<RssFeedListener> query = context.RssFeedListeners;
            foreach (var channelId in channelsToPurgeFeedsOf)
            {
                query = query.Where(x => x.ChannelId == channelId);
            }

            await query.ExecuteDeleteAsync();
        }
    }

    public static bool ValidateFeed(Feed? feed)
    {
        return feed?.Type != FeedType.Unknown;
    }

    private Embed GenerateFeedItemEmbed(FeedItem genericItem, Feed genericFeed, RssFeedListener feedListener, Color embedColor)
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

                    var content = item.Element.Descendants().FirstOrDefault(x =>
                        x.Name.LocalName == "content" && x.Attribute("type")?.Value == "xhtml")?
                        .Descendants().FirstOrDefault(x => x.Name.LocalName == "img")?
                        .Attributes().FirstOrDefault(x => x.Name == "src")?.Value;

                    if (content != null)
                    {
                        eb.WithImageUrl(content);
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
                throw new ArgumentOutOfRangeException();
        }

        string? thumbnail =
            genericItem.SpecificItem.Element.Descendants().FirstOrDefault(x => x.Name.LocalName.Contains("thumbnail", StringComparison.InvariantCultureIgnoreCase))?
                .Attribute("url")?.Value;

        if (!string.IsNullOrWhiteSpace(thumbnail))
        {
            eb.WithImageUrl(thumbnail);
        }

        eb.WithColor(embedColor);

        return eb.Build();
    }
}