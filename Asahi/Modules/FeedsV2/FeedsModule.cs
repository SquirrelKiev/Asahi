using Asahi.Database;
using Asahi.Database.Models.Rss;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.FeedsV2;

[Group("feeds", "Commands relating to feeds.")]
// [DefaultMemberPermissions(GuildPermission.ManageGuild)]
[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
public class FeedsModule(
#if DEBUG
    FeedsTimerService feedsTimerService,
#endif
    FeedsProcessorService feedsProcessor,
    IFeedProviderFactory feedProviderFactory,
    IFeedMessageDispatcher feedMessageDispatcher,
    IDbContextFactory<BotDbContext> dbService,
    InteractiveService interactive,
    FeedsStateTracker stateTracker,
    ILogger<FeedsModule> logger) : BotModule
{
    [SlashCommand("add-feed", "Adds a feed.")]
    public async Task AddFeedSlash(
        [Summary(description: "The feed source. usually a URL."), MaxLength(512)]
        string feedSource,
        [Summary(description: "The channel to send updates to.")]
        IMessageChannel channel)
    {
        await CommonConfig(async (context, eb) =>
        {
            if (await context.RssFeedListeners.AnyAsync(x => x.GuildId == Context.Guild.Id &&
                                                             x.ChannelId == channel.Id &&
                                                             x.FeedUrl == feedSource))
            {
                return new ConfigChangeResult(false, "You already have this feed added for this channel!");
            }

            var feedProvider = feedProviderFactory.GetFeedProvider(feedSource);

            if (feedProvider == null)
                return new ConfigChangeResult(false, "No feed handler supports that source.");

            try
            {
                await feedProvider.Initialize(feedSource);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to initialize feed {feedSource}.", feedSource);
                return new ConfigChangeResult(false, "Failed to retrieve feed.");
            }

            feedsProcessor.TryCacheInitialArticlesIfNecessary(feedSource, feedProvider, stateTracker);

            context.Add(new FeedListener()
            {
                GuildId = Context.Guild.Id,
                ChannelId = channel.Id,
                FeedUrl = feedSource
            });

            eb.WithTitle(feedProvider.DefaultFeedTitle);

            return new ConfigChangeResult(true, "Added feed.");
        });
    }

    [SlashCommand("rm-feed", "Removes a feed.")]
    public async Task RemoveFeedSlash(
        [Summary(description: "The ID of the feed to remove.")] [Autocomplete(typeof(FeedAutocomplete))]
        uint id)
    {
        await CommonFeedConfig(id, options =>
        {
            options.context.Remove(options.feedListener);

            return Task.FromResult(new ConfigChangeResult(true, "Removed feed."));
        });
    }

    [SlashCommand("set-channel", "Sets the feed's channel to something different.")]
    public async Task SetFeedUrlSlash(
        [Summary(description: "The ID of the feed to edit.")] [Autocomplete<FeedAutocomplete>]
        uint id,
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

    [SlashCommand("set-source", "Sets the feed's source to something different.")]
    public async Task SetFeedUrlSlash(
        [Summary(description: "The ID of the feed to edit.")] [Autocomplete<FeedAutocomplete>]
        uint id,
        [Summary(description: "The feed source. usually a URL."), MaxLength(512)]
        string feedSource)
    {
        await CommonFeedConfig(id, async options =>
        {
            var feedProvider = feedProviderFactory.GetFeedProvider(feedSource);

            if (feedProvider == null)
                return new ConfigChangeResult(false, "No feed handler supports that source.");

            try
            {
                await feedProvider.Initialize(feedSource);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to initialize feed {feedSource}.", feedSource);
                return new ConfigChangeResult(false, "Failed to retrieve feed.");
            }

            options.feedListener.FeedUrl = feedSource;
            options.embedBuilder.WithTitle(feedProvider.DefaultFeedTitle);

            return new ConfigChangeResult(true, $"Successfully set feed URL to {feedSource}.");
        });
    }

    [SlashCommand("set-title", "Sets the title of the feed to something else.")]
    public async Task SetFeedTitleSlash(
        [Summary(description: "The ID of the feed to edit.")] [Autocomplete<FeedAutocomplete>]
        uint id,
        [Summary(description: "The new title for the feed. Leave unspecified for default feed title."), MaxLength(64)]
        string? feedTitle = null)
    {
        await CommonFeedConfig(id, options =>
        {
            options.feedListener.FeedTitle = feedTitle;

            return Task.FromResult(new ConfigChangeResult(true, $"Set feed title to `{feedTitle}`."));
        });
    }

    [SlashCommand("set-webhook-name", "Sets or clears the name of the webhook to look for to send with.")]
    public async Task SetFeedWebhookNameSlash(
        [Summary(description: "The ID of the feed to edit.")] [Autocomplete<FeedAutocomplete>] uint id,
        [Summary(description: "The new webhook name for the feed."), MaxLength(64)]
        string? webhookName = null)
    {
        await CommonFeedConfig(id, async options =>
        {
            options.feedListener.WebhookName = webhookName;

            if (webhookName == null)
            {
                return new ConfigChangeResult(true, "Cleared the webhook name.");
            }

            if (((SocketGuild)Context.Guild).GetChannel(options.feedListener.ChannelId) is IIntegrationChannel channel)
            {
                await channel.GetOrCreateWebhookAsync(webhookName, Context.Client.CurrentUser);
            }

            return new ConfigChangeResult(true, $"Set feed webhook name to `{webhookName}`.");
        });
    }

    [SlashCommand("toggle", "Turns a feed on or off.")]
    public async Task ToggleFeedSlash(
        [Summary(description: "The ID of the feed.")] [Autocomplete<FeedAutocomplete>] uint id,
        [Summary(description: "Whether the feed should be enabled or disabled.")]
        bool state)
    {
        await CommonFeedConfig(id, options =>
        {
            if (options.feedListener is { Enabled: false, ForcedDisable: true })
            {
                return Task.FromResult(new ConfigChangeResult(false,
                    $"This feed has been temporarily disabled for reason **{options.feedListener.DisabledReason}**. You cannot enable it."));
            }

            options.feedListener.Enabled = state;
            options.feedListener.DisabledReason = state ? "" : $"Disabled by <@{Context.User.Id}>.";

            return Task.FromResult(new ConfigChangeResult(true, $"Feed has been {(state ? "enabled" : "disabled")}."));
        });
    }

    // TODO: Update to work with FeedsV2 stuff - namely the new naming stuff. ideally we just have the feed name as null and pull from the cache in FeedsStateTracker. (Also a TODO)
    [SlashCommand("list-feeds", "Lists the feeds within the server.")]
    public async Task ListFeedsSlash(
        [Summary(description: "Filters the list to only show feeds for the specified channel.")]
        IMessageChannel? channel = null
    )
    {
        await DeferAsync();

        await using var context = await dbService.CreateDbContextAsync();

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
                var feedTitle = x.FeedTitle ?? stateTracker.GetCachedDefaultFeedTitle(x.FeedUrl);
                
                if (string.IsNullOrWhiteSpace(feedTitle))
                    return $"* ({x.Id}) <#{x.ChannelId}> - {x.FeedUrl}";

                return $"* ({x.Id}) <#{x.ChannelId}> - [{feedTitle}]({x.FeedUrl})";
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

        await interactive.SendPaginatorAsync(paginator.Build(), Context.Interaction, TimeSpan.FromMinutes(2),
            InteractionResponseType.DeferredChannelMessageWithSource);
    }

    [SlashCommand("test-feed", "Sends the newest post from a feed.")]
    public async Task TestFeedSlash([Summary(description: "The url to test.")] string feedSource,
        [MinValue(1), MaxValue(10), Summary(description: "How many entries to send.")]
        int entriesToSend = 1)
    {
        await DeferAsync();

        var feedProvider = feedProviderFactory.GetFeedProvider(feedSource);

        var currentUser = await Context.Guild.GetCurrentUserAsync();
        if (feedProvider == null)
        {
            await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(await Context.Guild.GetCurrentUserAsync(),
                new EmbedBuilder(), new ConfigChangeResult(false, "No feed handler supports that source.")));
            return;
        }

        try
        {
            await feedProvider.Initialize(feedSource);
        }
        catch
        {
            await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(currentUser, new EmbedBuilder(),
                new ConfigChangeResult(false,
                    "Failed to retrieve feed.")));
            return;
        }

        logger.LogTrace("Retrieved {embedBuilderType} embed builder.", feedProvider.GetType());

        var testFeedListener = new FeedListener()
        {
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            FeedUrl = feedSource,
            Id = 0
        };

        var articleIds = feedProvider.ListArticleIds().Take(entriesToSend).Reverse();

        var embedColor = QuotingHelpers.GetUserRoleColorWithFallback(currentUser, Color.Default);
        foreach (var messages in articleIds.Select(x => feedProvider.GetArticleMessageContent(x, embedColor, null)))
        {
            //logger.LogTrace($"message {i++}: {message.embeds?[0].Title} {message.embeds?[0].Timestamp}");
            await feedMessageDispatcher.SendMessages(testFeedListener, messages);
        }

        await FollowupAsync(embeds: ConfigUtilities.CreateEmbeds(currentUser,
            new EmbedBuilder().WithTitle(feedProvider.DefaultFeedTitle), new ConfigChangeResult(true,
                $"Sent entries.")));
    }

#if DEBUG
    [SlashCommand("force-poll", "[DEBUG] Poll for feed updates.")]
    public async Task DebugSlash()
    {
        await DeferAsync();

        var feeds = await feedsTimerService.GetFeeds();
        await feedsProcessor.PollFeeds(stateTracker, feeds);

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
