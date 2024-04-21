using System.Text.RegularExpressions;
using Asahi.Database;
using Asahi.Database.Models;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Asahi.Modules.Highlights;

[Group("highlights", "Commands relating to the highlights system.")]
[InteractionsModCommand]
public partial class HighlightsModule(DbService dbService, InteractiveService interactiveService, ILogger<HighlightsModule> logger) : BotModule
{
    #region Create/Remove board

    [SlashCommand("create", "Creates a new highlight board.")]
    public Task CreateSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        string name,
        [Summary(description: "The channel to log highlights to. Can be changed later.")]
        ITextChannel channel)
    {
        return CommonConfig(name, async (context, cleanName) =>
        {
            if (await context.HighlightBoards.AnyAsync(x => x.GuildId == Context.Guild.Id && x.Name == cleanName))
            {
                return new ConfigChangeResult(false, $"`{cleanName}` already exists.");
            }

            context.HighlightBoards.Add(new HighlightBoard()
            {
                GuildId = Context.Guild.Id,
                Name = cleanName,
                LoggingChannelId = channel.Id,
                Thresholds =
                [
                    new HighlightThreshold
                    {
                        OverrideId = Context.Guild.Id
                    }
                ]
            });
            return new ConfigChangeResult(true, $"Added board `{cleanName}`.");
        });
    }

    [SlashCommand("remove", "Removes a highlight board.")]
    public Task RemoveSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name)
    {
        return CommonConfig(name, async (context, cleanName) =>
        {
            var board = await context.HighlightBoards.FirstOrDefaultAsync(x => x.Name == name);
            if (board == null)
            {
                return new ConfigChangeResult(false, $"`{name}` does not exist already.");
            }

            context.HighlightBoards.Remove(board);
            return new ConfigChangeResult(true, $"Removed board `{cleanName}`.");
        });
    }

    #endregion

    #region Threshold

    [SlashCommand("threshold-add-override", "Adds a new threshold override for the specified channel.")]
    public Task ThresholdAddOverrideSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The channel to add the threshold for.")]
        IChannel channel,
        [Summary(description: "The existing threshold override to clone from. Defaults to Guild.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        string cloneOverride = ""
        )
    {
        return CommonBoardConfig(name, options =>
        {
            var channelType = channel.GetChannelType();
            if (channelType is ChannelType.DM or ChannelType.Voice or ChannelType.Store)
            {
                return Task.FromResult(new ConfigChangeResult(false, $"Channel type {channelType} is not supported."));
            }

            if (cloneOverride == "")
                cloneOverride = Context.Guild.Id.ToString();

            if (!ulong.TryParse(cloneOverride, out var toCloneFrom))
            {
                return Task.FromResult(new ConfigChangeResult(false, $"Could not parse clone override."));
            }

            var guildOverride = options.board.Thresholds.FirstOrDefault(x => x.OverrideId == toCloneFrom);

            if (guildOverride == null)
            {
                return Task.FromResult(new ConfigChangeResult(false,
                    $"Could not find clone threshold for `{toCloneFrom}`. If you put the threshold in using autocomplete, " +
                    "there's probably something very wrong. Ping Kiev."));
            }

            var newEntry = guildOverride.ShallowCopy();

            newEntry.OverrideId = channel.Id;
            newEntry.HighlightBoard = null!;

            options.board.Thresholds.Add(newEntry);
            
            return Task.FromResult(new ConfigChangeResult(true, $"Created override for channel <#{channel.Id}>."));
        }, boards => boards.Include(x => x.Thresholds));
    }

    [SlashCommand("threshold-remove", "Removes the threshold override.")]
    public Task ThresholdSetBaseSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The threshold to edit.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        [MaxLength(20)]
        string overrideId)
    {
        return CommonThresholdConfig(name, overrideId, (options, threshold) =>
        {
            if (threshold.OverrideId == Context.Guild.Id)
            {
                return Task.FromResult(new ConfigChangeResult(false,
                    "Removing the Guild's threshold would cause stuff to explode, so not happening."));
            }

            options.board.Thresholds.Remove(threshold);
            return Task.FromResult(new ConfigChangeResult(true, $"Removed override for channel <#{threshold.OverrideId}>."));
        });
    }

    [SlashCommand("threshold-set-base", "Sets the base threshold.")]
    public Task ThresholdSetBaseSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The threshold to edit.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        [MaxLength(20)]
        string overrideId,
        [Summary(description: "The base threshold all other threshold calculations will work off of.")]
        [MinValue(1)]
        int baseThreshold)
    {
        return CommonThresholdConfig(name, overrideId, (options, threshold) =>
        {
            threshold.BaseThreshold = baseThreshold;
            return Task.FromResult(new ConfigChangeResult(true, $"Set base threshold to {baseThreshold}."));
        });
    }

    [SlashCommand("threshold-set-max", "Sets the base threshold.")]
    public Task ThresholdSetMaxSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The threshold to edit.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        [MaxLength(20)]
        string overrideId,
        [Summary(description: "The maximum the threshold will cap at.")]
        [MinValue(1)]
        int maxThreshold)
    {
        return CommonThresholdConfig(name, overrideId, (options, threshold) =>
        {
            threshold.MaxThreshold = maxThreshold;
            return Task.FromResult(new ConfigChangeResult(true, $"Set max threshold to {maxThreshold}."));
        });
    }

    [SlashCommand("threshold-set-max-age", "Any messages older than this will not be considered when calculating unique users.")]
    public Task ThresholdSetMaxMessageAgeSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The threshold to edit.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        [MaxLength(20)]
        string overrideId,
        [Summary(description: "The maximum age of the message, in seconds.")]
        [MinValue(0)]
        int maxMessageAge)
    {
        return CommonThresholdConfig(name, overrideId, (options, threshold) =>
        {
            threshold.UniqueUserMessageMaxAgeSeconds = maxMessageAge;
            return Task.FromResult(new ConfigChangeResult(true, 
                $"Any messages older than {maxMessageAge}s will not be considered when calculating unique users."));
        });
    }

    [SlashCommand("threshold-set-unique-mult", "The total unique users in a convo will be multiplied by this before being added to the threshold.")]
    public Task ThresholdSetUniqueUserMultiplierSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The threshold to edit.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        [MaxLength(20)]
        string overrideId,
        [Summary(description: "The maximum age of the message, in seconds.")]
        [MinValue(0)]
        float uniqueUserMultiplier)
    {
        return CommonThresholdConfig(name, overrideId, (options, threshold) =>
        {
            threshold.UniqueUserMultiplier = uniqueUserMultiplier;
            return Task.FromResult(new ConfigChangeResult(true,
                $"Set the unique user multiplier to {uniqueUserMultiplier}."));
        });
    }

    #endregion

    #region Misc

    [SlashCommand("set-channel", "Sets the channel where highlighted messages are sent to.")]
    public Task SetChannelSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The channel to send highlighted messages to.")]
        ITextChannel channel)
    {
        return CommonBoardConfig(name, options =>
        {
            options.board.LoggingChannelId = channel.Id;
            return Task.FromResult(new ConfigChangeResult(true, $"Set channel to <#{channel.Id}>."));
        });
    }

    [SlashCommand("max-message-age",
        "The maximum age (in seconds) a message is allowed to be to be added as a highlight. 0 = any age.")]
    public Task SetMessageAgeSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The maximum age of a message, in seconds. 0 = any age.")]
        [MinValue(0)]
        int maxAge)
    {
        return CommonBoardConfig(name, options =>
        {
            options.board.MaxMessageAgeSeconds = maxAge;
            return Task.FromResult(new ConfigChangeResult(true, $"Maximum age set to {maxAge}s."));
        });
    }

    [SlashCommand("set-embed-color", "Sets the embed color of the message sent to the board channel.")]
    public Task SetEmbedColorSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "Where to source the embed color from.")]
        EmbedColorSource colorSource,
        [Summary(description: "The fallback embed color. Noting that #000000 (Black) will be interpreted as no embed color.")]
        Color color)
    {
        return CommonBoardConfig(name, async options =>
        {
            options.board.EmbedColorSource = colorSource;
            options.board.FallbackEmbedColor = color.RawValue;

            var currentUser = (SocketGuildUser)Context.User;

            // TODO: update to new quote style
            Embed[] testEmbeds =
            [
                new EmbedBuilder()
                    .WithAuthor(currentUser)
                    .WithDescription("Here's how that color source looks.")
                    .WithCurrentTimestamp()
                    .WithOptionalColor(await HighlightsHelpers.GetQuoteEmbedColor(colorSource, color, currentUser, (DiscordSocketClient)Context.Client))
                    .Build(),
                new EmbedBuilder()
                    .WithAuthor(currentUser)
                    .WithDescription("Here's how that fallback color would look.")
                    .WithCurrentTimestamp()
                    .WithOptionalColor(options.board.FallbackEmbedColor)
                    .Build()
            ];

            return new ConfigChangeResult(true,
                $"Set the color source as `{colorSource.Humanize()}` Set fallback color as `{color}`.",
                extraEmbeds: testEmbeds);
        });
    }

    #endregion

    #region Filtered Channels

    [SlashCommand("add-filtered-channel",
        "Adds the channel to the channel filter. Channel filter is blocklist by default.")]
    public Task AddFilterChannelSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        IMessageChannel channel)
    {
        return CommonBoardConfig(name, options =>
        {
            if (options.board.FilteredChannels.Contains(channel.Id))
            {
                return Task.FromResult(new ConfigChangeResult(false, $"Channel already in filtered channels."));
            }

            options.board.FilteredChannels.Add(channel.Id);
            return Task.FromResult(new ConfigChangeResult(true, $"Channel <#{channel.Id}> added to filtered channels."));
        });
    }

    [SlashCommand("remove-filtered-channel",
        "Removes the channel from the channel filter. Channel filter is blocklist by default.")]
    public Task RemoveFilterChannelSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        IMessageChannel channel)
    {
        return CommonBoardConfig(name, options =>
        {
            if (!options.board.FilteredChannels.Contains(channel.Id))
            {
                return Task.FromResult(new ConfigChangeResult(false, $"Channel not in filtered channels anyway."));
            }

            options.board.FilteredChannels.Remove(channel.Id);
            return Task.FromResult(new ConfigChangeResult(true, $"Channel <#{channel.Id}> removed from filtered channels."));
        });
    }


    public enum AllowBlockList
    {
        BlockList,
        AllowList
    }

    [SlashCommand("set-channel-filter-type",
        "Sets the channel filter type.")]
    public Task SetFilterChannelTypeSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The filter type.")]
        AllowBlockList filterType)
    {
        return CommonBoardConfig(name, options =>
        {
            options.board.FilteredChannelsIsBlockList = filterType == AllowBlockList.BlockList;
            return Task.FromResult(
                new ConfigChangeResult(true, 
                    $"Set channel filter type as {(options.board.FilteredChannelsIsBlockList ? "Blocklist" : "Allowlist")}."));
        });
    }

    #endregion

    #region Misc Filters

    [SlashCommand("filter-self-reactions",
        "Changes whether reactions from the message author will count or not.")]
    public Task SelfReactionsToggleSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "True = self reactions will not count towards the total reactions, False = they will.")]
        bool shouldFilter)
    {
        return CommonBoardConfig(name, options =>
        {
            options.board.FilterSelfReactions = shouldFilter;
            return Task.FromResult(new ConfigChangeResult(true,
                shouldFilter ? "Self-reactions will now no longer count towards the total unique reactions." :
                    "Self-reactions will now count towards the total unique reactions."));
        });
    }

    [SlashCommand("filter-no-send-message",
        "Changes whether reactions from users without send message permissions will be filtered or not.")]
    public Task LockedChannelToggleSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "Whether to filter users who don't have send message permissions or not.")]
        bool shouldFilter)
    {
        return CommonBoardConfig(name, options =>
        {
            options.board.RequireSendMessagePermissionInChannel = shouldFilter;
            return Task.FromResult(new ConfigChangeResult(true,
                shouldFilter ? "Messages from people without send message permissions will no longer count." :
                    "Messages from people without send message permissions will now count."));
        });
    }

    #endregion

    #region Auto Reacts

    [SlashCommand("auto-react-max-attempts",
        "Sets how many times the bot will try to react to the highlight before using fallback.")]
    public Task AutoReactMaxAttemptsSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "How many times the bot will try to react to the highlight before using fallback.")]
        [MinValue(0)] [MaxValue(20)]
        int maxAttempts)
    {
        return CommonBoardConfig(name, options =>
        {
            options.board.AutoReactMaxAttempts = maxAttempts;

            var message = maxAttempts == 0 ? "Auto reactions are now **disabled**." :
                $"We'll now attempt to react a maximum of {maxAttempts} times to the highlighted message. " + 
                (options.board.AutoReactFallbackEmoji == "" ? "Otherwise, we'll not react (no fallback emoji set.)" : 
                    "If we go past that, we'll, switch to fallback.");
            return Task.FromResult(new ConfigChangeResult(true, message));
        });
    }

    [SlashCommand("auto-react-max-reactions",
        "Sets how many attempts the bot will make to react to the message sent in the highlights channel.")]
    public Task AutoReactMaxReactionsSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The maximum number of emojis the bot will react to the message sent in the highlights channel with.")]
        [MinValue(0)] [MaxValue(20)]
        int maxReactions)
    {
        return CommonBoardConfig(name, options =>
        {
            options.board.AutoReactMaxReactions = maxReactions;
            return Task.FromResult(new ConfigChangeResult(true, maxReactions == 0 ? "Auto reactions are now **disabled**." :
                $"We'll now react a maximum of {maxReactions} times."));
        });
    }

    [SlashCommand("auto-react-fallback", "Sets the emote to react with if the bot can't react with the attempted emote.")]
    public Task AutoReactFallbackSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description:"The fallback emote to use. Put \"none\" if you don't want any reaction on fallback.")]
        [MaxLength(100)]
        string emote)
    {
        return CommonBoardConfig(name, options =>
        {
            // TODO: Check if this works with the EmoteTypeConverter
            IEmote? parsedEmote = null;
            if (emote == "none")
            { }
            else if (EmoteTypeConverter.TryParse(emote, out var outEmoji))
            {
                parsedEmote = outEmoji;
            }
            else
            {
                return Task.FromResult(new ConfigChangeResult(false, "Could not parse emote."));
            }

            options.board.AutoReactFallbackEmoji = parsedEmote?.ToString() ?? "";

            return Task.FromResult(new ConfigChangeResult(true, 
                parsedEmote == null ? "Disabled fallback reactions." : $"Set the fallback emote as {parsedEmote}."));
        });
    }

    [SlashCommand("auto-react-emote-preference", "Sets how the bot decides what to react with.")]
    public Task AutoReactEmotePreferenceSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The emote choice preference.")]
        AutoReactEmoteChoicePreference reactEmotePreference)
    {
        return CommonBoardConfig(name, options =>
        {
            options.board.AutoReactEmoteChoicePreference = reactEmotePreference;
            return Task.FromResult(new ConfigChangeResult(true, 
                $"Set the emote preference as `{reactEmotePreference.Humanize()}`"));
        });
    }

    [SlashCommand("alias-emote", "Any reaction emote with the name specified will be replaced with the alias. Good for private emotes.")]
    public Task AddEmoteAliasSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The emote name to replace. Case insensitive.")]
        [MaxLength(32)]
        string emoteName,
        [Summary(description: "The emote to replace with.")]
        [MaxLength(32)]
        IEmote emote)
    {
        return CommonBoardConfig(name, options =>
        {
            emoteName = emoteName.ToLowerInvariant();

            var existingEntry = options.board.EmoteAliases.FirstOrDefault(x => x.EmoteName == emoteName);
            if (existingEntry != null)
            {
                existingEntry.EmoteReplacement = emote.ToString()!;
            }
            else
            {
                options.board.EmoteAliases.Add(new EmoteAlias { EmoteName = emoteName, EmoteReplacement = emote.ToString()! });
            }

            return Task.FromResult(new ConfigChangeResult(true, $"Aliased any emote with the name \"{emoteName}\" to the emote {emote}."));
        }, boards => boards.Include(x => x.EmoteAliases));
    }

    [SlashCommand("remove-alias-emote", "Removes an emote alias.")]
    public Task RemoveEmoteAliasSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The emote name to replace. Case insensitive.")]
        [MaxLength(32)]
        [Autocomplete(typeof(AliasedEmoteAutocomplete))]
        string emoteName)
    {
        return CommonBoardConfig(name, options =>
        {
            emoteName = emoteName.ToLowerInvariant();

            var existingEntry = options.board.EmoteAliases.FirstOrDefault(x => x.EmoteName == emoteName);
            if (existingEntry != null)
            {
                options.board.EmoteAliases.Remove(existingEntry);
            }
            else
            {
                return Task.FromResult(new ConfigChangeResult(false, $"Alias for {emoteName} doesn't exist anyway."));
            }

            return Task.FromResult(new ConfigChangeResult(true, $"Removed the alias for {emoteName}."));
        }, 
        boards => boards.Include(x => x.EmoteAliases));
    }

    #endregion

    #region Utility Commands

    private static string GetDisplayableBoardConfig(HighlightBoard board)
    {
        return $"```json\n{JsonConvert.SerializeObject(board, Formatting.Indented).Replace("```", @"\`\`\`")}\n```";
    }

    [SlashCommand("get-board", "Retrieves a board config.")]
    public async Task GetBoardSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "Includes emote aliases in the output.")]
        bool includeEmoteAliases = false,
        [Summary(description: "Includes thresholds in the output.")]
        bool includeThresholds = false,
        [Summary(description: "Includes filtered channels in the output.")]
        bool includeFilteredChannels = false)
    {
        await DeferAsync();

        name = name.ToLowerInvariant();

        if (!IsValidId().IsMatch(name))
        {
            await FollowupAsync(new MessageContents(new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(name)
                .WithDescription($"`{name}` is not valid.")));
            return;
        }

        await using var context = dbService.GetDbContext();

        IQueryable<HighlightBoard> highlightBoards = context.HighlightBoards;

        if (includeEmoteAliases)
        {
            highlightBoards = highlightBoards
                .Include(x => x.EmoteAliases);
        }

        if (includeThresholds)
        {
            highlightBoards = highlightBoards.Include(x => x.Thresholds);
        }

        var board = await highlightBoards.FirstOrDefaultAsync(x => x.Name == name);

        if (board == null)
        {
            await FollowupAsync(new MessageContents(new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(name)
                .WithDescription($"`{name}` does not exist.")));
            return;
        }

        if (!includeFilteredChannels)
        {
            board.FilteredChannels = [];
        }

        var json = JsonConvert.SerializeObject(board, Formatting.Indented);

        if (json.Length > 1900)
        {
            await FollowupWithFileAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)), $"{board.Name}.json");
        }
        else
        {
            await FollowupAsync($"```json\n{json}\n```");
        }
    }

    [SlashCommand("get-threshold", "Retrieves a board's threshold.")]
    public async Task GetThresholdSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The threshold to edit.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        [MaxLength(20)]
        string channelId
        )
    {
        await DeferAsync();

        name = name.ToLowerInvariant();

        if (!IsValidId().IsMatch(name))
        {
            await FollowupAsync(new MessageContents(new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(name)
                .WithDescription($"`{name}` is not valid.")));
            return;
        }

        await using var context = dbService.GetDbContext();

        var board = await context.HighlightBoards.Include(x => x.Thresholds).FirstOrDefaultAsync(x => x.Name == name);

        if (board == null)
        {
            await FollowupAsync(new MessageContents(new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(name)
                .WithDescription($"`{name}` does not exist.")));
            return;
        }

        if (!ulong.TryParse(channelId, out var overrideId))
        {
            await FollowupAsync(new MessageContents(new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(name)
                .WithDescription($"`{channelId}` is not a valid ulong.")));
            return;
        }

        var threshold = board.Thresholds.FirstOrDefault(x => x.OverrideId == overrideId);

        if (threshold == null)
        {
            await FollowupAsync(new MessageContents(new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(name)
                .WithDescription($"<#{overrideId}> does not have an override. Create one first.")));
            return;
        }

        var json = JsonConvert.SerializeObject(threshold, Formatting.Indented);

        if (json.Length > 1900)
        {
            await FollowupWithFileAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)), $"{board.Name}-{threshold.OverrideId}.json");
        }
        else
        {
            await FollowupAsync($"```json\n{json}\n```");
        }
    }

#if DEBUG
    [SlashCommand("debug-clear-highlights", "[DEBUG] Clear all cached highlighted messages from db.")]
    public async Task DebugClearHighlights()
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        await context.CachedHighlightedMessages.ExecuteDeleteAsync();

        await FollowupAsync("done'd");
    }
#endif

    #endregion

    #region Common

    private struct ConfigChangeResult(bool wasSuccess, string message, Embed[] extraEmbeds)
    {
        public bool wasSuccess = wasSuccess;
        public string message = message;
        public Embed[] extraEmbeds = extraEmbeds;

        public ConfigChangeResult(bool wasSuccess, string message) : this(wasSuccess, message, [])
        {
        }
    }

    private struct ConfigChangeOptions(BotDbContext context, HighlightBoard board, string name)
    {
        public BotDbContext context = context;
        public HighlightBoard board = board;
        public string name = name;
    }

    private async Task<bool> CommonConfig(
        string name,
        Func<BotDbContext, string, Task<ConfigChangeResult>> updateAction)
    {
        await DeferAsync();
        name = name.ToLowerInvariant();

        if (!IsValidId().IsMatch(name))
        {
            await FollowupAsync($"`{name}` is not valid.");
            return false;
        }

        await using var context = dbService.GetDbContext();

        var message = await updateAction(context, name);

        if (message.wasSuccess)
            await context.SaveChangesAsync();

        await FollowupAsync(embeds: message.extraEmbeds.Prepend(
            new EmbedBuilder()
                .WithAuthor(name)
                .WithDescription(message.message)
                .WithColor(message.wasSuccess ? Color.Green : Color.Red)
                .Build()
            ).ToArray());
        return message.wasSuccess;
    }

    private Task<bool> CommonBoardConfig(string userSetName,
        Func<ConfigChangeOptions, Task<ConfigChangeResult>> updateAction, 
        Func<IQueryable<HighlightBoard>, IQueryable<HighlightBoard>>? highlightBoardModifier = null)
    {
        highlightBoardModifier ??= boards => boards;

        return CommonConfig(userSetName, async (context, name) =>
        {
            var board = await highlightBoardModifier(context.HighlightBoards).FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.Name == name);

            if (board == null)
            {
                return new ConfigChangeResult(false, $"`{name}` does not exist.");
            }

            return await updateAction(new ConfigChangeOptions(context, board, name));
        });
    }

    private Task<bool> CommonThresholdConfig(string userSetName,
        string channelId,
        Func<ConfigChangeOptions, HighlightThreshold, Task<ConfigChangeResult>> updateAction)
    {
        return CommonBoardConfig(userSetName, async options =>
        {
            if (!ulong.TryParse(channelId, out var overrideId))
            {
                return new ConfigChangeResult(false, $"`{channelId}` is not a valid ulong.");
            }

            var threshold = options.board.Thresholds.FirstOrDefault(x => x.OverrideId == overrideId);

            if (threshold == null)
            {
                return new ConfigChangeResult(false, $"<#{overrideId}> does not have an override. Create one first.");
            }

            return await updateAction(options, threshold);
        }, x => x.Include(y => y.Thresholds));
    }

    [GeneratedRegex(@"^[\w-]+$")]
    private static partial Regex IsValidId();

    #endregion
}
