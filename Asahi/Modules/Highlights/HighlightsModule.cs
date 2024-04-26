using System.Text;
using System.Text.RegularExpressions;
using Asahi.Database;
using Asahi.Database.Models;
using Discord.Interactions;
using Discord.WebSocket;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Asahi.Modules.Highlights;

[InteractionsModCommand]
[CommandContextType(InteractionContextType.Guild)]
[Group("highlights", "Commands relating to the highlights system.")]
public class HighlightsModule(DbService dbService, HighlightsTrackingService hts, ILogger<HighlightsModule> logger) : HighlightsSubmodule(dbService)
{
    #region Create/Remove board

    [SlashCommand("create", "Creates a new highlight board.")]
    public Task CreateSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")] [MaxLength(HighlightBoard.MaxNameLength)]
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

    #region Override Threshold

    [Group("threshold", "Commands relating to threshold overrides.")]
    public class HighlightsThresholdSubmodule(DbService dbService) : HighlightsSubmodule(dbService)
    {
        [SlashCommand("add", "Adds a new threshold override for the specified channel.")]
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
                    return Task.FromResult(new ConfigChangeResult(false,
                        $"Channel type {channelType} is not supported."));
                }

                if (cloneOverride == "")
                    cloneOverride = Context.Guild.Id.ToString();

                if (!ulong.TryParse(cloneOverride, out var toCloneFrom))
                {
                    return Task.FromResult(
                        new ConfigChangeResult(false, $"Could not parse clone override."));
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

                return Task.FromResult(
                    new ConfigChangeResult(true, $"Created override for channel <#{channel.Id}>."));
            }, boards => boards.Include(x => x.Thresholds));
        }

        [SlashCommand("remove", "Removes the threshold override.")]
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
                return Task.FromResult(new ConfigChangeResult(true,
                    $"Removed override for channel <#{threshold.OverrideId}>."));
            });
        }

        [SlashCommand("set-base", "Sets the base threshold.")]
        public Task ThresholdSetBaseSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
            [Summary(description: "The threshold to edit.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        [MaxLength(20)]
        string overrideId,
            [Summary(description: "The base threshold all other threshold calculations will work off of.")] [MinValue(1)]
        int baseThreshold)
        {
            return CommonThresholdConfig(name, overrideId, (options, threshold) =>
            {
                threshold.BaseThreshold = baseThreshold;
                return Task.FromResult(
                    new ConfigChangeResult(true, $"Set base threshold to {baseThreshold}."));
            });
        }

        [SlashCommand("set-max", "Sets the max threshold.")]
        public Task ThresholdSetMaxSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
            [Summary(description: "The threshold to edit.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        [MaxLength(20)]
        string overrideId,
            [Summary(description: "The maximum the threshold will cap at.")] [MinValue(1)]
        int maxThreshold)
        {
            return CommonThresholdConfig(name, overrideId, (options, threshold) =>
            {
                threshold.MaxThreshold = maxThreshold;
                return Task.FromResult(
                    new ConfigChangeResult(true, $"Set max threshold to {maxThreshold}."));
            });
        }

        [SlashCommand("set-rounding", "If the threshold goes higher than this, it's rounded up instead of down.")]
        public Task ThresholdSetRoundingSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
            [MaxLength(HighlightBoard.MaxNameLength)]
            [Autocomplete(typeof(HighlightsNameAutocomplete))]
            string name,
            [Summary(description: "The threshold to edit.")]
            [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
            [MaxLength(20)]
            string overrideId,
            [Summary(description: "If the threshold goes higher than this, it's rounded up instead of down.")] [MinValue(0),MaxValue(1)]
            float roundingThreshold)
        {
            return CommonThresholdConfig(name, overrideId, (options, threshold) =>
            {
                threshold.RoundingThreshold = roundingThreshold;
                return Task.FromResult(
                    new ConfigChangeResult(true, $"Set rounding threshold to {roundingThreshold}."));
            });
        }

        [SlashCommand("set-max-age",
            "Any messages older than this will not be considered when calculating unique users.")]
        public Task ThresholdSetMaxMessageAgeSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
            [Summary(description: "The threshold to edit.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        [MaxLength(20)]
        string overrideId,
            [Summary(description: "The maximum age of the message, in seconds.")] [MinValue(0)]
        int maxMessageAge)
        {
            return CommonThresholdConfig(name, overrideId, (options, threshold) =>
            {
                threshold.UniqueUserMessageMaxAgeSeconds = maxMessageAge;
                return Task.FromResult(new ConfigChangeResult(true,
                    $"Any messages older than {maxMessageAge}s will not be considered when calculating unique users."));
            });
        }

        [SlashCommand("set-unique-mult",
            "The total unique users in a convo will be multiplied by this before being added to the threshold.")]
        public Task ThresholdSetUniqueUserMultiplierSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
            [Summary(description: "The threshold to edit.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        [MaxLength(20)]
        string overrideId,
            [Summary(description: "The maximum age of the message, in seconds.")] [MinValue(0)]
        float uniqueUserMultiplier)
        {
            return CommonThresholdConfig(name, overrideId, (options, threshold) =>
            {
                threshold.UniqueUserMultiplier = uniqueUserMultiplier;
                return Task.FromResult(new ConfigChangeResult(true,
                    $"Set the unique user multiplier to {uniqueUserMultiplier}."));
            });
        }

        [SlashCommand("set-unique-decay",
            "A user's contribution to the threshold will decay once their latest message is this old.")]
        public Task ThresholdSetUniqueUserAgeDecaySlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
            [MaxLength(HighlightBoard.MaxNameLength)]
            [Autocomplete(typeof(HighlightsNameAutocomplete))]
            string name,
            [Summary(description: "The threshold to edit.")]
            [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
            [MaxLength(20)]
            string overrideId,
            [Summary(description: "The delay (in seconds) to wait before decaying the user.")] [MinValue(0)]
            int decayDelaySeconds)
        {
            return CommonThresholdConfig(name, overrideId, (options, threshold) =>
            {
                threshold.UniqueUserDecayDelaySeconds = decayDelaySeconds;
                return Task.FromResult(new ConfigChangeResult(true,
                    $"Set the decay delay to {decayDelaySeconds}."));
            });
        }

        [SlashCommand("set-active-lookback",
            "Which message to check the age of for checking high activity. (e.g. 50th message)")]
        public Task ThresholdSetHighActivityLookbackSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
            [MaxLength(HighlightBoard.MaxNameLength)]
            [Autocomplete(typeof(HighlightsNameAutocomplete))]
            string name,
            [Summary(description: "The threshold to edit.")]
            [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
            [MaxLength(20)]
            string overrideId,
            [Summary(description: "The index of the message to look at.")] [MinValue(1)]
            int lookbackIndex)
        {
            return CommonThresholdConfig(name, overrideId, (options, threshold) =>
            {
                threshold.HighActivityMessageLookBack = lookbackIndex;
                return Task.FromResult(new ConfigChangeResult(true,
                    $"Set the look-back index to {lookbackIndex}."));
            });
        }

        [SlashCommand("set-active-max-age",
            "If the look-back message is this old or less, we deem the channel as \"high activity\".")]
        public Task ThresholdSetHighActivityMaxAgeSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
            [MaxLength(HighlightBoard.MaxNameLength)]
            [Autocomplete(typeof(HighlightsNameAutocomplete))]
            string name,
            [Summary(description: "The threshold to edit.")]
            [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
            [MaxLength(20)]
            string overrideId,
            [Summary(description: "The maximum age of the message, in seconds.")] [MinValue(0)]
            int maxAgeSeconds)
        {
            return CommonThresholdConfig(name, overrideId, (options, threshold) =>
            {
                threshold.HighActivityMessageMaxAgeSeconds = maxAgeSeconds;
                return Task.FromResult(new ConfigChangeResult(true,
                    $"Set the maximum look-back message age to {maxAgeSeconds}."));
            });
        }

        [SlashCommand("set-active-mult",
            "If the channel is deemed high activity, threshold will be multiplied by this.")]
        public Task ThresholdSetHighActivityMultiplierSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
            [MaxLength(HighlightBoard.MaxNameLength)]
            [Autocomplete(typeof(HighlightsNameAutocomplete))]
            string name,
            [Summary(description: "The threshold to edit.")]
            [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
            [MaxLength(20)]
            string overrideId,
            [Summary(description: "If the channel is deemed high activity, threshold will be multiplied by this.")] [MinValue(0)]
            float highActivityMultiplier)
        {
            return CommonThresholdConfig(name, overrideId, (options, threshold) =>
            {
                threshold.HighActivityMultiplier = highActivityMultiplier;
                return Task.FromResult(new ConfigChangeResult(true,
                    $"Set the high activity multiplier to {highActivityMultiplier}."));
            });
        }

    }

    #endregion


    [SlashCommand("add-spoiler-channel", "Any channel added here will always have its messages spoiler tagged.")]
    public Task AddSpoilerChannelSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "Any highlights originating from this channel are spoiler-tagged.")]
        [ChannelTypes(ChannelType.Text, ChannelType.PrivateThread, ChannelType.PublicThread)]
        IGuildChannel spoilerTaggedChannel,
        [MaxLength(SpoilerChannel.MaxContextLength)]
        string spoilerContext
        )
    {
        return CommonBoardConfig(name, options =>
        {
            var spoilerChannel = options.board.SpoilerChannels.FirstOrDefault(x => x.ChannelId == spoilerTaggedChannel.Id);

            if (spoilerChannel == null)
            {
                spoilerChannel = new SpoilerChannel()
                {
                    ChannelId = spoilerTaggedChannel.Id,
                    SpoilerContext = spoilerContext
                };
                options.board.SpoilerChannels.Add(spoilerChannel);
            }
            else
            {
                spoilerChannel.SpoilerContext = spoilerContext;
            }

            return Task.FromResult(new ConfigChangeResult(true, $"Added <#{spoilerTaggedChannel.Id}> to spoiler channels."));
        }, boards => boards.Include(x => x.SpoilerChannels));
    }

    [SlashCommand("rm-spoiler-channel", "Removes a spoiler channel.")]
    public Task RemoveSpoilerChannelSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The channel to remove.")]
        [ChannelTypes(ChannelType.Text, ChannelType.PrivateThread, ChannelType.PublicThread)]
        IGuildChannel spoilerTaggedChannel
    )
    {
        return CommonBoardConfig(name, options =>
        {
            var spoilerChannel = options.board.SpoilerChannels.FirstOrDefault(x => x.ChannelId == spoilerTaggedChannel.Id);

            if (spoilerChannel == null)
            {
                return Task.FromResult(new ConfigChangeResult(false, "Channel isn't in spoiler channels list anyway."));
            }

            options.board.SpoilerChannels.Remove(spoilerChannel);

            return Task.FromResult(new ConfigChangeResult(true, $"Removed <#{spoilerTaggedChannel.Id}> from spoiler channels."));
        }, boards => boards.Include(x => x.SpoilerChannels));
    }

    [SlashCommand("add-log-override", "Highlighted messages from the overridden channel will be sent to a different highlight channel.")]
    public Task AddLogOverrideSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The channel to override the logging channel for.")]
        [ChannelTypes(ChannelType.Text, ChannelType.PrivateThread, ChannelType.PublicThread)]
        IGuildChannel overriddenChannel,
        [Summary(description: "The logging channel to use instead.")]
        ITextChannel logChannel
        )
    {
        return CommonBoardConfig(name, options =>
        {
            var overriden = options.board.LoggingChannelOverrides
                .FirstOrDefault(x => x.OverriddenChannelId == overriddenChannel.Id);

            if (overriden == null)
            {
                overriden = new()
                {
                    OverriddenChannelId = overriddenChannel.Id,
                    LoggingChannelId = logChannel.Id
                };
                options.board.LoggingChannelOverrides.Add(overriden);
            }
            else
            {
                overriden.LoggingChannelId = logChannel.Id;
            }

            return Task.FromResult(new ConfigChangeResult(true, $"Changed <#{overriddenChannel.Id}> to log to <#{logChannel.Id}> instead."));
        }, boards => boards.Include(x => x.SpoilerChannels));
    }

    [SlashCommand("rm-log-override", "Removes a logging channel override.")]
    public Task RemoveLogOverrideSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The overridden channel to reset to default.")]
        [ChannelTypes(ChannelType.Text, ChannelType.PrivateThread, ChannelType.PublicThread)]
        IGuildChannel overriddenChannel
    )
    {
        return CommonBoardConfig(name, options =>
        {
            var overridden = options.board.LoggingChannelOverrides.FirstOrDefault(x => x.OverriddenChannelId == overriddenChannel.Id);

            if (overridden == null)
            {
                return Task.FromResult(new ConfigChangeResult(false, "Channel isn't overridden anyway."));
            }

            options.board.LoggingChannelOverrides.Remove(overridden);

            return Task.FromResult(new ConfigChangeResult(true, $"Reset logging channel for <#{overriddenChannel.Id}>."));
        }, boards => boards.Include(x => x.SpoilerChannels));
    }

    #region Misc

    [SlashCommand("set-mute-role", "Any users with the role specified will not make it into highlights.")]
    public Task SetMuteRoleSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "Any users with this role specified will not make it into highlights.")]
        IRole? muteRole = null)
    {
        return CommonBoardConfig(name, options =>
        {
            options.board.HighlightsMuteRole = muteRole?.Id ?? 0;
            return Task.FromResult(
                new ConfigChangeResult(true, muteRole != null ? $"Set mute role to <@&{muteRole.Id}>." : "No mute role set."));
        });
    }

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
            return Task.FromResult(
                new ConfigChangeResult(true, $"Set channel to <#{channel.Id}>."));
        });
    }

    [SlashCommand("max-message-age",
        "The maximum age (in seconds) a message is allowed to be to be added as a highlight. 0 = any age.")]
    public Task SetMessageAgeSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The maximum age of a message, in seconds. 0 = any age.")] [MinValue(0)]
        int maxAge)
    {
        return CommonBoardConfig(name, options =>
        {
            options.board.MaxMessageAgeSeconds = maxAge;
            return Task.FromResult(
                new ConfigChangeResult(true, $"Maximum age set to {maxAge}s."));
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
        [Summary(
            description:
            "The fallback embed color. Noting that #000000 (Black) will be interpreted as no embed color.")]
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
                    .WithOptionalColor(await HighlightsHelpers.GetQuoteEmbedColor(colorSource, color, currentUser,
                        (DiscordSocketClient)Context.Client))
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

    [Group("filtered-channel", "Commands relating to channel filters.")]
    public class HighlightsFilteredChannelsSubmodule(DbService dbService) : HighlightsSubmodule(dbService)
    {
        [SlashCommand("add",
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
                    return Task.FromResult(
                        new ConfigChangeResult(false, $"Channel already in filtered channels."));
                }

                options.board.FilteredChannels.Add(channel.Id);
                return Task.FromResult(new ConfigChangeResult(true,
                    $"Channel <#{channel.Id}> added to filtered channels."));
            });
        }

        [SlashCommand("add-raw",
            "Add multiple channels to the channel filter. This takes channel IDs. Comma separated.")]
        public Task AddFilterChannelsRawSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
            [MaxLength(HighlightBoard.MaxNameLength)]
            [Autocomplete(typeof(HighlightsNameAutocomplete))]
            string name,
            string channels)
        {
            return CommonBoardConfig(name, async options =>
            {
                List<ulong> addedChannels = [];
                List<ulong> failedChannelsBecauseNull = [];
                List<ulong> failedChannelsBecauseAlreadyExists = [];
                List<string> failedChannelsBecauseInvalidUlong = [];
                foreach (var channelStr in channels.Split(","))
                {
                    if (!ulong.TryParse(channelStr, out var channelId))
                    {
                        failedChannelsBecauseInvalidUlong.Add(channelStr);
                        continue;
                    }

                    var channel = await Context.Guild.GetChannelAsync(channelId);

                    if (channel == null)
                    {
                        failedChannelsBecauseNull.Add(channelId);
                        continue;
                    }
                    if (options.board.FilteredChannels.Contains(channel.Id))
                    {
                        failedChannelsBecauseAlreadyExists.Add(channelId);
                        continue;
                    }

                    options.board.FilteredChannels.Add(channel.Id);
                    addedChannels.Add(channel.Id);
                }

                var sb = new StringBuilder();
                if (addedChannels.Count != 0)
                {
                    sb.AppendLine().Append("Added the following channels: ");
                    foreach (var channel in addedChannels)
                    {
                        sb.Append("<#").Append(channel).Append(">, ");
                    }
                }

                if (failedChannelsBecauseNull.Count != 0)
                {
                    sb.AppendLine().Append("Failed to add the following channels as they could not be found: ");
                    foreach (var channel in failedChannelsBecauseNull)
                    {
                        sb.Append("<#").Append(channel).Append(">, ");
                    }
                }

                if (failedChannelsBecauseAlreadyExists.Count != 0)
                {
                    sb.AppendLine().Append("Failed to add the following channels as they are already added: ");
                    foreach (var channel in failedChannelsBecauseAlreadyExists)
                    {
                        sb.Append("<#").Append(channel).Append(">, ");
                    }
                }

                if (failedChannelsBecauseInvalidUlong.Count != 0)
                {
                    sb.AppendLine().Append("Failed to add the following channels as they could not be parsed: ");
                    foreach (var channel in failedChannelsBecauseInvalidUlong)
                    {
                        sb.Append(channel).Append(", ");
                    }
                }

                return new ConfigChangeResult(true, sb.ToString());
            });
        }

        [SlashCommand("remove-raw",
            "Remove multiple channels to the channel filter. This takes channel IDs. Comma separated.")]
        public Task RemoveFilterChannelsRawSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
            [MaxLength(HighlightBoard.MaxNameLength)]
            [Autocomplete(typeof(HighlightsNameAutocomplete))]
            string name,
            string channels)
        {
            return CommonBoardConfig(name, async options =>
            {
                List<ulong> removedChannels = [];
                List<ulong> failedChannelsBecauseNull = [];
                List<ulong> failedChannelsBecauseDoesntExist = [];
                List<string> failedChannelsBecauseInvalidUlong = [];
                foreach (var channelStr in channels.Split(","))
                {
                    if (!ulong.TryParse(channelStr, out var channelId))
                    {
                        failedChannelsBecauseInvalidUlong.Add(channelStr);
                        continue;
                    }

                    var channel = await Context.Guild.GetChannelAsync(channelId);

                    if (channel == null)
                    {
                        failedChannelsBecauseNull.Add(channelId);
                        continue;
                    }
                    if (!options.board.FilteredChannels.Contains(channel.Id))
                    {
                        failedChannelsBecauseDoesntExist.Add(channelId);
                        continue;
                    }

                    options.board.FilteredChannels.Remove(channel.Id);
                    removedChannels.Add(channel.Id);
                }

                var sb = new StringBuilder();
                if (removedChannels.Count != 0)
                {
                    sb.AppendLine().Append("Removed the following channels: ");
                    foreach (var channel in removedChannels)
                    {
                        sb.Append("<#").Append(channel).Append(">, ");
                    }
                }

                if (failedChannelsBecauseNull.Count != 0)
                {
                    sb.AppendLine().Append("Failed to remove the following channels as they could not be found: ");
                    foreach (var channel in failedChannelsBecauseNull)
                    {
                        sb.Append("<#").Append(channel).Append(">, ");
                    }
                }

                if (failedChannelsBecauseDoesntExist.Count != 0)
                {
                    sb.AppendLine().Append("Failed to remove the following channels as they weren't filtered anyway: ");
                    foreach (var channel in failedChannelsBecauseDoesntExist)
                    {
                        sb.Append("<#").Append(channel).Append(">, ");
                    }
                }

                if (failedChannelsBecauseInvalidUlong.Count != 0)
                {
                    sb.AppendLine().Append("Failed to remove the following channels as they could not be parsed: ");
                    foreach (var channel in failedChannelsBecauseInvalidUlong)
                    {
                        sb.Append(channel).Append(", ");
                    }
                }

                return new ConfigChangeResult(true, sb.ToString());
            });
        }

        [SlashCommand("remove",
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
                    return Task.FromResult(
                        new ConfigChangeResult(false, $"Channel not in filtered channels anyway."));
                }

                options.board.FilteredChannels.Remove(channel.Id);
                return Task.FromResult(new ConfigChangeResult(true,
                    $"Channel <#{channel.Id}> removed from filtered channels."));
            });
        }


        public enum AllowBlockList
        {
            BlockList,
            AllowList
        }

        [SlashCommand("set-filter-type",
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
                shouldFilter
                    ? "Self-reactions will now no longer count towards the total unique reactions."
                    : "Self-reactions will now count towards the total unique reactions."));
        });
    }

    #endregion

    #region Auto Reacts

    [Group("auto-react", "Commands relating to auto reacts.")]
    public class HighlightsAutoReactSubmodule(DbService dbService) : HighlightsSubmodule(dbService)
    {
        [SlashCommand("max-attempts",
        "Sets how many times the bot will try to react to the highlight before using fallback.")]
        public Task AutoReactMaxAttemptsSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "How many times the bot will try to react to the highlight before using fallback.")]
        [MinValue(0)]
        [MaxValue(20)]
        int maxAttempts)
        {
            return CommonBoardConfig(name, options =>
            {
                options.board.AutoReactMaxAttempts = maxAttempts;

                var message = maxAttempts == 0
                    ? "Auto reactions are now **disabled**."
                    : $"Will now attempt to react a maximum of {maxAttempts} times to the highlighted message. " +
                      (options.board.AutoReactFallbackEmoji == ""
                          ? "If exceeding that, no reaction shall be added (no fallback emoji set.)"
                          : "If exceeding that, fallback will be reacted with instead.");
                return Task.FromResult(new ConfigChangeResult(true, message));
            });
        }

        [SlashCommand("max-reactions",
            "Sets how many attempts the bot will make to react to the message sent in the highlights channel.")]
        public Task AutoReactMaxReactionsSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
            [Summary(
            description:
            "The maximum number of emojis the bot will react to the message sent in the highlights channel with.")]
        [MinValue(0)]
        [MaxValue(20)]
        int maxReactions)
        {
            return CommonBoardConfig(name, options =>
            {
                options.board.AutoReactMaxReactions = maxReactions;
                return Task.FromResult(new ConfigChangeResult(true,
                    maxReactions == 0
                        ? "Auto reactions are now **disabled**."
                        : $"Auto reaction count now capped at {maxReactions}."));
            });
        }

        [SlashCommand("fallback",
            "Sets the emote to react with if the bot can't react with the attempted emote.")]
        public Task AutoReactFallbackSlash(
            [Summary(description: "The name/ID of the board. Case insensitive.")]
            [MaxLength(HighlightBoard.MaxNameLength)]
            [Autocomplete(typeof(HighlightsNameAutocomplete))]
            string name,
            [Summary(description: "The fallback emote to use. Put \"none\" if you don't want any reaction on fallback.")]
            [MaxLength(100)]
            string emote)
        {
            return CommonBoardConfig(name, options =>
            {
                // TODO: Check if this works with the EmoteTypeConverter
                IEmote? parsedEmote = null;
                if (emote == "none")
                {
                }
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

        [SlashCommand("emote-preference", "Sets how the bot decides what to react with.")]
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

        [SlashCommand("alias-emote",
            "Any reaction emote with the name specified will be replaced with the alias. Good for private emotes.")]
        public Task AddEmoteAliasSlash(
            [Summary(description: "The emote name to replace. Case insensitive.")] [MaxLength(32)]
            string emoteName,
            [Summary(description: "The emote to replace with.")] [MaxLength(100)]
            IEmote emote)
        {
            return CommonConfig(null, async (context, _) =>
            {
                emoteName = emoteName.ToLowerInvariant();


                var existingEntry = await context.EmoteAliases.FirstOrDefaultAsync(x => x.EmoteName == emoteName);
                if (existingEntry != null)
                {
                    existingEntry.EmoteReplacement = emote.ToString()!;
                }
                else
                {
                    context.Add(new EmoteAlias
                    { GuildId = Context.Guild.Id, EmoteName = emoteName, EmoteReplacement = emote.ToString()! });
                }

                return new ConfigChangeResult(true,
                    $"Aliased any emote with the name \"{emoteName}\" to the emote {emote}.");
            });
        }

        [SlashCommand("rm-alias-emote", "Removes an emote alias.")]
        public Task RemoveEmoteAliasSlash(
            [Summary(description: "The emote name to replace. Case insensitive.")]
        [MaxLength(32)]
        [Autocomplete(typeof(AliasedEmoteAutocomplete))]
        string emoteName)
        {
            return CommonConfig(null, async (context, _) =>
            {
                emoteName = emoteName.ToLowerInvariant();

                var existingEntry = await context.EmoteAliases.FirstOrDefaultAsync(x => x.EmoteName == emoteName);
                if (existingEntry != null)
                {
                    context.EmoteAliases.Remove(existingEntry);
                }
                else
                {
                    return new ConfigChangeResult(false,
                        $"Alias for {emoteName} doesn't exist anyway.");
                }

                return new ConfigChangeResult(true, $"Removed the alias for {emoteName}.");
            });
        }


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
        [Summary(description: "Includes thresholds in the output.")]
        bool includeThresholds = false,
        [Summary(description: "Includes filtered channels in the output.")]
        bool includeFilteredChannels = false,
        [Summary(description: "Includes logging channel overrides in the output.")]
        bool includeLoggingOverrides = false,
        [Summary(description: "Includes spoiler channels in the output.")]
        bool includeSpoilerChannels = false)
    {
        await DeferAsync();

        name = name.ToLowerInvariant();

        if (!HighlightsModuleUtility.IsValidId().IsMatch(name))
        {
            await FollowupAsync(new MessageContents(new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(name)
                .WithDescription($"`{name}` is not valid.")));
            return;
        }

        await using var context = dbService.GetDbContext();

        IQueryable<HighlightBoard> highlightBoards = context.HighlightBoards;

        if (includeThresholds)
        {
            highlightBoards = highlightBoards.Include(x => x.Thresholds);
        }
        if (includeLoggingOverrides)
        {
            highlightBoards = highlightBoards.Include(x => x.LoggingChannelOverrides);
        }
        if (includeSpoilerChannels)
        {
            highlightBoards = highlightBoards.Include(x => x.SpoilerChannels);
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
            await FollowupWithFileAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)),
                $"{board.Name}.json");
        }
        else
        {
            await FollowupAsync($"```json\n{json}\n```");
        }
    }

    [SlashCommand("get-override", "Retrieves a board's threshold.")]
    public async Task GetThresholdSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The threshold to edit.")]
        [Autocomplete(typeof(HighlightsThresholdAutocomplete))]
        [MaxLength(20)]
        string overrideId
    )
    {
        await DeferAsync();

        name = name.ToLowerInvariant();

        if (!HighlightsModuleUtility.IsValidId().IsMatch(name))
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

        if (!ulong.TryParse(overrideId, out var id))
        {
            await FollowupAsync(new MessageContents(new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(name)
                .WithDescription($"`{overrideId}` is not a valid ulong.")));
            return;
        }

        var threshold = board.Thresholds.FirstOrDefault(x => x.OverrideId == id);

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
            await FollowupWithFileAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)),
                $"{board.Name}-{threshold.OverrideId}.json");
        }
        else
        {
            await FollowupAsync($"```json\n{json}\n```");
        }
    }

    [SlashCommand("get-threshold", "Gets the current calculated threshold for a channel.")]
    public Task GetCurrentChannelsThresholdSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The channel to check the threshold of.")]
        [ChannelTypes(ChannelType.Text, ChannelType.PrivateThread, ChannelType.PublicThread)]
        IGuildChannel channel)
    {
        return CommonBoardConfig(name, options =>
        {
            var threshold = options.board.Thresholds.FirstOrDefault(x => x.OverrideId == channel.Id);
            if (channel is SocketThreadChannel threadChannel)
                threshold ??= options.board.Thresholds.FirstOrDefault(x => x.OverrideId == threadChannel.ParentChannel.Id);
            if (channel is ITextChannel textChannel)
                threshold ??= options.board.Thresholds.FirstOrDefault(x => x.OverrideId == textChannel.CategoryId);
            threshold ??= options.board.Thresholds.FirstOrDefault(x => x.OverrideId == channel.Guild.Id);

            if (threshold == null)
                return Task.FromResult(new ConfigChangeResult(false, "Couldn't find a threshold for that channel! " +
                                                                     "This is very bad, we should at least be able to find the Guild's threshold. Ping Kiev."));

            HighlightsHelpers.CalculateThreshold(threshold, hts.GetCachedMessages(channel.Id), DateTimeOffset.UtcNow, out var message);

            return Task.FromResult(new ConfigChangeResult(true, message));
        }, boards => boards.Include(x => x.Thresholds));
    }

    #endregion

    #region Debug Commands

#if DEBUG

    private static bool loggingShouldStop = false;

    [SlashCommand("debug-log-threshold", "[DEBUG] Logs the current channel's threshold in console eternally.")]
    public async Task DebugLogThresholdSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The channel to check the threshold of.")]
        [ChannelTypes(ChannelType.Text, ChannelType.PrivateThread, ChannelType.PublicThread)]
        IGuildChannel channel, bool stopLogging = false)
    {
        loggingShouldStop = stopLogging;

        await RespondAsync("away we go");

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync())
        {
            await using var context = dbService.GetDbContext();

            var board = await context.HighlightBoards.Where(x => x.GuildId == Context.Guild.Id && x.Name == name)
                .Include(highlightBoard => highlightBoard.Thresholds).FirstAsync();

            var threshold = board.Thresholds.FirstOrDefault(x => x.OverrideId == channel.Id);
            if (channel is SocketThreadChannel threadChannel)
                threshold ??= board.Thresholds.FirstOrDefault(x => x.OverrideId == threadChannel.ParentChannel.Id);
            if (channel is ITextChannel textChannel)
                threshold ??= board.Thresholds.FirstOrDefault(x => x.OverrideId == textChannel.CategoryId);
            threshold ??= board.Thresholds.FirstOrDefault(x => x.OverrideId == channel.Guild.Id);

            if (threshold == null || loggingShouldStop)
                return;

            HighlightsHelpers.CalculateThreshold(board.Thresholds.First(), hts.GetCachedMessages(Context.Channel.Id), DateTimeOffset.UtcNow, out var threshDebugInfo);
            logger.LogTrace("info is {info}", threshDebugInfo);
        }
    }

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
}

public struct ConfigChangeResult(bool wasSuccess, string message, Embed[] extraEmbeds)
{
    public bool wasSuccess = wasSuccess;
    public string message = message;
    public Embed[] extraEmbeds = extraEmbeds;

    public ConfigChangeResult(bool wasSuccess, string message) : this(wasSuccess, message, [])
    {
    }
}

public struct ConfigChangeOptions(BotDbContext context, HighlightBoard board, string name)
{
    public BotDbContext context = context;
    public HighlightBoard board = board;
    public string name = name;
}

public static partial class HighlightsModuleUtility
{
    public static async Task<bool> CommonConfig(IInteractionContext botContext, DbService dbService,
        string? name,
        Func<BotDbContext, string, Task<ConfigChangeResult>> updateAction)
    {
        await botContext.Interaction.DeferAsync();
        if (name != null)
        {
            name = name.ToLowerInvariant();

            if (!IsValidId().IsMatch(name))
            {
                await botContext.Interaction.FollowupAsync($"`{name}` is not valid.");
                return false;
            }
        }

        await using var context = dbService.GetDbContext();

        var message = await updateAction(context, name ?? "(N/A)");

        if (message.wasSuccess)
            await context.SaveChangesAsync();

        await botContext.Interaction.FollowupAsync(embeds: message.extraEmbeds.Prepend(
            new EmbedBuilder()
                .WithAuthor(name)
                .WithDescription(message.message)
                .WithColor(message.wasSuccess ? Color.Green : Color.Red)
                .Build()
            ).ToArray());
        return message.wasSuccess;
    }

    public static Task<bool> CommonBoardConfig(IInteractionContext botContext, DbService dbService,
        string userSetName,
        Func<ConfigChangeOptions, Task<ConfigChangeResult>> updateAction,
        Func<IQueryable<HighlightBoard>, IQueryable<HighlightBoard>>? highlightBoardModifier = null)
    {
        highlightBoardModifier ??= boards => boards;

        return CommonConfig(botContext, dbService, userSetName, async (context, name) =>
        {
            var board = await highlightBoardModifier(context.HighlightBoards).FirstOrDefaultAsync(x => x.GuildId == botContext.Guild.Id && x.Name == name);

            if (board == null)
            {
                return new ConfigChangeResult(false, $"`{name}` does not exist.");
            }

            return await updateAction(new ConfigChangeOptions(context, board, name));
        });
    }

    public static Task<bool> CommonThresholdConfig(IInteractionContext botContext, DbService dbService,
        string userSetName,
        string channelId,
        Func<ConfigChangeOptions, HighlightThreshold, Task<ConfigChangeResult>> updateAction)
    {
        return CommonBoardConfig(botContext, dbService, userSetName, async options =>
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
    public static partial Regex IsValidId();
}

public class HighlightsSubmodule(DbService dbService) : BotModule
{
    protected readonly DbService dbService = dbService;

    protected Task<bool> CommonConfig(string? name,
        Func<BotDbContext, string, Task<ConfigChangeResult>> updateAction)
    {
        return HighlightsModuleUtility.CommonConfig(Context, dbService, name, updateAction);
    }

    protected Task<bool> CommonBoardConfig(string userSetName,
        Func<ConfigChangeOptions, Task<ConfigChangeResult>> updateAction,
        Func<IQueryable<HighlightBoard>, IQueryable<HighlightBoard>>? highlightBoardModifier = null)
    {
        return HighlightsModuleUtility.CommonBoardConfig(Context, dbService, userSetName, updateAction,
            highlightBoardModifier);
    }

    protected Task<bool> CommonThresholdConfig(string userSetName,
        string channelId,
        Func<ConfigChangeOptions, HighlightThreshold, Task<ConfigChangeResult>> updateAction)
    {
        return HighlightsModuleUtility.CommonThresholdConfig(Context, dbService, userSetName, channelId, updateAction);
    }
}