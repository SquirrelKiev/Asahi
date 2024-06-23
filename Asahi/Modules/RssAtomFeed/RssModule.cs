using System.Web;
using Asahi.Database;
using Asahi.Database.Models.Rss;
using CodeHollow.FeedReader;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.RssAtomFeed;

[Group("rss", "Commands relating to RSS/Atom feeds.")]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class RssModule(DbService dbService, RssTimerService rts, InteractiveService interactive, HttpClient http) : BotModule
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

            var configChangeResult = await ValidateFeedUrl(url, eb);

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

    private async Task<ConfigChangeResult?> ValidateFeedUrl(string url, EmbedBuilder eb)
    {
        try
        {
            http.MaxResponseContentBufferSize = 8000000;
            using var req = await http.GetAsync(url);
            var xml = await req.Content.ReadAsStringAsync();

            var feedHandler = RssTimerService.FeedHandlerForUrl(url);
            switch (feedHandler)
            {
                case RssTimerService.FeedHandler.RssAtom:
                    {
                        var feed = FeedReader.ReadFromString(xml);

                        if (!RssTimerService.ValidateFeed(feed))
                        {
                            return new ConfigChangeResult(false, "Feed isn't valid!");
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
                default:
                    throw new NotSupportedException();
            }
        }
        catch (Exception ex)
        {
            {
                return new ConfigChangeResult(false, $"Failed to get feed. `{ex.GetType()}`: `{ex.Message}`");
            }
        }

        return null;
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
            var configChangeResult = await ValidateFeedUrl(url, options.embedBuilder);

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

#if DEBUG
    [SlashCommand("force-poll", "[DEBUG] Poll for feed updates.")]
    public async Task DebugSlash()
    {
        await DeferAsync();

        await rts.PollFeeds();

        await FollowupAsync("Polled. Check logs for more info.");
    }
#endif

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
