using System.Web;
using Asahi.Database;
using Asahi.Database.Models.Rss;
using CodeHollow.FeedReader;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.RssAtomFeed;

[Group("rss", "Commands relating to RSS/Atom feeds.")]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class RssModule(DbService dbService, RssTimerService rts, InteractiveService interactive, HttpClient http,
    IRedditApi redditApi,
    ILogger<RssModule> logger) : BotModule
{
    [SlashCommand("add-feed", "Adds a feed.")]
    public async Task AddFeedSlash(
        [Summary(description: "The RSS/Atom url."), MaxLength(512)]
        string url,
        [Summary(description: "The channel to send updates to.")]
        IMessageChannel channel)
    {
        await CommonConfig(async (context, eb) =>
        {
            if (await context.RssFeedListeners.AnyAsync(
                    x => x.GuildId == Context.Guild.Id &&
                         x.ChannelId == channel.Id &&
                         x.FeedUrl == url))
            {
                return new ConfigChangeResult(false, "You already have this feed added for this channel!");
            }

            var (configChangeResult, _) = await ValidateFeedUrl(url, eb);

            if (configChangeResult != null)
                return configChangeResult.Value;

            context.Add(new FeedListener()
            {
                GuildId = Context.Guild.Id,
                ChannelId = channel.Id,
                FeedUrl = url,
                FeedTitle = eb.Title?.Truncate(64)
            });

            return new ConfigChangeResult(true, "Added feed.");
        });
    }

    [SlashCommand("rm-feed", "Removes a feed.")]
    public async Task RemoveFeedSlash(
        [Summary(description: "The ID of the feed to remove.")]
        uint id)
    {
        await CommonFeedConfig(id, options =>
        {
            options.context.Remove(options.feedListener);

            return Task.FromResult(new ConfigChangeResult(true, "Removed feed."));
        });
    }

    [SlashCommand("set-channel", "Sets the feed's channel to something different.")]
    public async Task SetFeedUrlSlash([Summary(description: "The ID of the feed to edit.")] uint id,
        [Summary(description: "The channel to send updates to.")]
        IMessageChannel channel)
    {
        await CommonFeedConfig(id, options =>
        {
            options.feedListener.ChannelId = channel.Id;

            return Task.FromResult(new ConfigChangeResult(true, $"Successfully set channel to <#{channel.Id}>. " +
                                                                $"(Make sure the bot has permission to speak there)"));
        });
    }

    [SlashCommand("set-url", "Sets the feed's URL to something different.")]
    public async Task SetFeedUrlSlash([Summary(description: "The ID of the feed to edit.")] uint id,
        [Summary(description: "The RSS/Atom url."), MaxLength(512)]
        string url)
    {
        await CommonFeedConfig(id, async options =>
        {
            var (configChangeResult, _) = await ValidateFeedUrl(url, options.embedBuilder);

            if (configChangeResult != null)
                return configChangeResult.Value;

            options.feedListener.FeedUrl = url;

            return new ConfigChangeResult(true, $"Successfully set feed URL to {url}.");
        });
    }

    [SlashCommand("set-title", "Sets the title of the feed to something else.")]
    public async Task SetFeedTitleSlash([Summary(description: "The ID of the feed to edit.")] uint id,
        [Summary(description: "The new title for the feed."), MaxLength(64)] string feedTitle)
    {
        await CommonFeedConfig(id, options =>
        {
            options.feedListener.FeedTitle = feedTitle;

            return Task.FromResult(new ConfigChangeResult(true, $"Set feed title to `{feedTitle}`."));
        });
    }

    [SlashCommand("list-feeds", "Lists the feeds within the server.")]
    public async Task ListFeedsSlash(
        [Summary(description: "Filters the list to only show feeds for the specified channel.")]
        IMessageChannel? channel = null
        )
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var feedsQuery = context.RssFeedListeners.Where(x => x.GuildId == Context.Guild.Id);

        if (channel != null)
        {
            feedsQuery = feedsQuery.Where(x => x.ChannelId == channel.Id);
        }
        var feeds = await feedsQuery.OrderBy(x => x.Id).ToArrayAsync();

        var us = await Context.Guild.GetCurrentUserAsync();
        if (feeds.Length == 0)
        {
            await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(us,
                new EmbedBuilder(), new ConfigChangeResult(true, "No feeds.")));

            return;
        }

        var roleColor = QuotingHelpers.GetUserRoleColorWithFallback(us, Color.Green);

        var pages = feeds
            .Select(x =>
            {
                if (string.IsNullOrWhiteSpace(x.FeedTitle))
                    return $"* ({x.Id}) <#{x.ChannelId}> - {x.FeedUrl}";

                return $"* ({x.Id}) <#{x.ChannelId}> - [{x.FeedTitle}]({x.FeedUrl})";
            })
            .Chunk(20).Select(x => new PageBuilder().WithColor(roleColor)
                .WithDescription(string.Join('\n', x)));

        var paginator = new StaticPaginatorBuilder()
            .WithPages(pages)
            .WithOptions(
            [
                new PaginatorButton("<", PaginatorAction.Backward, ButtonStyle.Secondary),
                new PaginatorButton("Jump", PaginatorAction.Jump, ButtonStyle.Secondary),
                new PaginatorButton(">", PaginatorAction.Forward, ButtonStyle.Secondary),
                new PaginatorButton(ModulePrefixes.RED_BUTTON, null, "X", ButtonStyle.Danger),
            ])
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithUsers(Context.User);

        await interactive.SendPaginatorAsync(paginator.Build(), Context.Interaction, TimeSpan.FromMinutes(2), InteractionResponseType.DeferredChannelMessageWithSource);
    }

    [SlashCommand("test-feed", "Sends the newest post from a feed.")]
    public async Task TestFeedSlash([Summary(description: "The url to test.")] string url,
        [MinValue(1), MaxValue(10), Summary(description: "How many entries to send.")] int entriesToSend = 1)
    {
        await DeferAsync();

        var eb = new EmbedBuilder();
        var (configRes, feedRes) = await ValidateFeedUrl(url, eb);

        var currentUser = await Context.Guild.GetCurrentUserAsync();
        if (configRes != null)
        {
            await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(currentUser, eb,
                configRes.Value));
            return;
        }

        var thing = await rts.TryGetEmbedGeneratorForFeed(url, feedRes);
        if (!thing.isSuccess)
        {
            await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(currentUser, new EmbedBuilder(), new ConfigChangeResult(false,
                "Failed to generate embeds.")));
            return;
        }

        var testFeedListener = new FeedListener()
        {
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            FeedTitle = eb.Title?.Truncate(64),
            FeedUrl = url,
            Id = 0
        };

        var messages = thing.embedGenerator!.GenerateFeedItemMessages(testFeedListener, new HashSet<int>(), new HashSet<int>(),
            QuotingHelpers.GetUserRoleColorWithFallback(currentUser, Color.Default), true);

        int i = 0;
        foreach (var message in messages.Take(entriesToSend))
        {
            //logger.LogTrace($"message {i++}: {message.embeds?[0].Title} {message.embeds?[0].Timestamp}");
            await Context.Channel.SendMessageAsync(message.body, embeds: message.embeds,
                components: message.components, allowedMentions: new AllowedMentions(AllowedMentionTypes.Users));
            i++;
        }

        await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(currentUser, eb, new ConfigChangeResult(true,
            $"Sent {i} entries.")));
    }

#if DEBUG
    [SlashCommand("force-poll", "[DEBUG] Poll for feed updates.")]
    public async Task DebugSlash()
    {
        await DeferAsync();

        await rts.PollFeeds();

        await FollowupAsync("Polled. Check logs for more info.");
    }
#endif

    private async Task<(ConfigChangeResult? configRes, string? feedRes)> ValidateFeedUrl(string url, EmbedBuilder eb)
    {
        string? res = null;
        try
        {
            var feedHandler = RssTimerService.FeedHandlerForUrl(url);
            switch (feedHandler)
            {
                case RssTimerService.FeedHandler.RssAtom:
                    {
                        http.MaxResponseContentBufferSize = 8000000;
                        using var req = await http.GetAsync(url);
                        res = await req.Content.ReadAsStringAsync();

                        var feed = FeedReader.ReadFromString(res);

                        if (!RssTimerService.ValidateFeed(feed))
                        {
                            return (new ConfigChangeResult(false, "Feed isn't valid!"), res);
                        }

                        if (!string.IsNullOrWhiteSpace(feed.ImageUrl))
                            eb.WithThumbnailUrl(feed.ImageUrl);

                        eb.WithUrl(url);

                        if (!string.IsNullOrWhiteSpace(feed.Title))
                            eb.WithTitle(feed.Title);

                        if (!string.IsNullOrWhiteSpace(feed.Description))
                            eb.WithFields(new EmbedFieldBuilder().WithName("Description").WithValue(feed.Description));
                        break;
                    }
                case RssTimerService.FeedHandler.Danbooru:
                    {
                        http.MaxResponseContentBufferSize = 8000000;
                        using var req = await http.GetAsync(url);
                        res = await req.Content.ReadAsStringAsync();

                        var uri = new Uri(url);

                        var query = HttpUtility.ParseQueryString(uri.Query);

                        var tags = query["tags"];
                        string? title;
                        if (tags != null)
                        {
                            title = $"Danbooru: {tags}";
                        }
                        else
                        {
                            title = "Danbooru";
                        }

                        eb.WithTitle(title);
                        eb.WithUrl(url);

                        break;
                    }
                case RssTimerService.FeedHandler.Reddit:
                    {
                        var regex = CompiledRegex.RedditFeedRegex().Match(url);

                        var feedType = regex.Groups["type"].Value;

                        if (feedType != "r")
                        {
                            return (new ConfigChangeResult(false, "Not a supported reddit feed type. (Currently only subreddits)"), res);
                        }

                        var subreddit = regex.Groups["subreddit"].Value;

                        // TODO: test non existent subreddits

                        var sr = await redditApi.GetSubredditInfo(subreddit);

                        eb.WithTitle(sr.Data.DisplayNamePrefixed);
                        eb.WithUrl($"https://reddit.com/{feedType}/{subreddit}/new");

                        break;
                    }
                default:
                    throw new NotSupportedException();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get feed: {url}. Exception: {ex}", url, ex.Message);

            return (new ConfigChangeResult(false, $"Failed to get feed. `{ex.GetType()}`: `{ex.Message}`"), null);
        }

        return (null, res);
    }

    public struct ConfigChangeOptions(BotDbContext context, FeedListener feedListener, EmbedBuilder embedBuilder)
    {
        public BotDbContext context = context;
        public EmbedBuilder embedBuilder = embedBuilder;
        public FeedListener feedListener = feedListener;
    }

    private Task<bool> CommonFeedConfig(uint id, Func<ConfigChangeOptions, Task<ConfigChangeResult>> updateAction)
    {
        return CommonConfig(async (context, eb) =>
        {
            var feed = await context.GetFeed(id, Context.Guild.Id);

            if (feed == null)
                return new ConfigChangeResult(false, "No feed found with that ID.");

            return await updateAction(new ConfigChangeOptions(context, feed, eb));
        });
    }

    private Task<bool> CommonConfig(Func<BotDbContext, EmbedBuilder, Task<ConfigChangeResult>> updateAction)
    {
        return ConfigUtilities.CommonConfig(Context, dbService, updateAction);
    }
}
