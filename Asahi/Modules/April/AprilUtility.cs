using April.Config;
using Asahi.Database;
using Asahi.Database.Models.April;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Asahi.Modules.April;

[Inject(ServiceLifetime.Singleton)]
public class AprilUtility(IDiscordClient client, ILogger<AprilUtility> logger)
{
    private static readonly Random rng = new Random();

    public static string PrettyPrintCoinCounter(ConfigFile config, int coins)
    {
        return $"**{config.coinEmote} {coins} {config.coinName}**";
    }

    public static string PrettyPrintCoinCounterAbbreviated(ConfigFile config, int coins)
    {
        return $"**{config.coinEmote} {coins}**";
    }

    public static bool CheckConditionStatus(List<PoolConditionContainer> conditions,
        UserData userData,
        IGuildUser user)
    {
        return conditions.All(condition => CheckCondition(condition, userData, user));
    }

    public static bool CheckCondition(PoolConditionContainer condition, UserData userData, IGuildUser user)
    {
        switch (condition.conditionType)
        {
            case PoolConditionContainer.ConditionType.HasItemCondition:
            {
                var data = (HasItemCondition)condition.data;

                return userData.InventoryItems.Any(x => x.ItemGuid == data.itemGuid);
            }
            case PoolConditionContainer.ConditionType.NotCondition:
            {
                var data = (NotCondition)condition.data;

                return !CheckConditionStatus(data.conditions, userData, user);
            }
            case PoolConditionContainer.ConditionType.OrCondition:
            {
                var data = (OrCondition)condition.data;

                return data.conditions.Any(innerCondition => CheckCondition(innerCondition, userData, user));
            }
            case PoolConditionContainer.ConditionType.HasRoleCondition:
            {
                var data = (HasRoleCondition)condition.data;

                return user.RoleIds.Contains(data.roleId);
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public async Task<GachaMessage> ExecuteRewardActions(
        List<RewardActionContainer> actions,
        BotDbContext dbContext,
        UserData userData,
        IGuildUser user,
        IMessageChannel channel)
    {
        GachaMessage? potentialContents = null;

        foreach (var action in actions)
        {
            switch (action.actionType)
            {
                case RewardActionContainer.ActionType.SetResponse:
                {
                    var data = (SetResponseData)action.data;

                    potentialContents = data.response;
                    break;
                }

                case RewardActionContainer.ActionType.SendMessage:
                {
                    var data = (SendMessageActionData)action.data;

                    var msg = GachaMessageToMessageContents(data.message);
                    await (data.channelId == 0
                            ? channel
                            : (IMessageChannel)await client.GetChannelAsync(data.channelId))
                        .SendMessageAsync(msg.body, embeds: msg.embeds, components: msg.components);

                    break;
                }
                case RewardActionContainer.ActionType.GrantRole:
                {
                    var data = (RoleActionData)action.data;

                    await user.AddRoleAsync(data.roleId);

                    break;
                }
                case RewardActionContainer.ActionType.RemoveRole:
                {
                    var data = (RoleActionData)action.data;

                    await user.RemoveRoleAsync(data.roleId);

                    break;
                }
                case RewardActionContainer.ActionType.AddItem:
                {
                    var data = (ItemActionData)action.data;

                    if (userData.InventoryItems.All(x => x.ItemGuid != data.itemGuid))
                    {
                        userData.InventoryItems.Add(new InventoryItem()
                        {
                            ItemGuid = data.itemGuid
                        });
                    }
                    else
                    {
                        potentialContents = new GachaMessage()
                        {
                            content = "You already have that item!"
                        };
                    }

                    break;
                }
                case RewardActionContainer.ActionType.RemoveItem:
                {
                    var data = (ItemActionData)action.data;

                    var filteredItems = userData.InventoryItems.Where(x => x.ItemGuid == data.itemGuid);
                    foreach (var item in filteredItems)
                    {
                        userData.InventoryItems.Remove(item);
                    }

                    break;
                }
                case RewardActionContainer.ActionType.ChangeNickname:
                {
                    var data = (ChangeNicknameActionData)action.data;

                    try
                    {
                        await user.ModifyAsync(x => { x.Nickname = data.newNickname; });
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to set nickname.");
                    }

                    break;
                }
                case RewardActionContainer.ActionType.ExecuteAfterDelay:
                {
                    var data = (ExecuteAfterDelayActionData)action.data;

                    var timeSpanSeconds = rng.Next(data.delaySecondsMin, data.delaySecondsMax);
                    var now = DateTimeOffset.UtcNow;
                    var timeSpan = now + TimeSpan.FromSeconds(timeSpanSeconds);
                    await AddDelayedActions(data.actions, timeSpan, dbContext, user, channel);

                    break;
                }
                default:
                    throw new NotSupportedException();
            }
        }

        potentialContents ??= new GachaMessage { content = "No response set for this reward :(" };

        return potentialContents;
    }

    public async Task AddDelayedActions(List<RewardActionContainer> actions, DateTimeOffset whenToExecute,
        BotDbContext context, IGuildUser user, IMessageChannel channel)
    {
        var actionsJson = JsonConvert.SerializeObject(actions);

        var dbEntry = new DelayedAction
        {
            ActionJson = actionsJson,
            WhenToExecute = whenToExecute,

            ChannelId = channel.Id,
            UserId = user.Id,
            GuildId = user.GuildId,
        };

        context.DelayedActions.Add(dbEntry);
    }

    public static MessageContents GachaMessageToMessageContents(GachaMessage gachaMessage)
    {
        return new MessageContents(gachaMessage.content ?? "",
            gachaMessage.embeds?.Select(embed => new EmbedBuilder()
            {
                Author = embed.author == null
                    ? null
                    : new EmbedAuthorBuilder()
                    {
                        IconUrl = embed.author.iconUrl,
                        Name = embed.author.name,
                        Url = embed.author.url
                    },
                Color = embed.color,
                Title = embed.title,
                Url = embed.url,
                Description = embed.description,
                Fields = embed.fields == null
                    ? []
                    : embed.fields.Select(field =>
                        new EmbedFieldBuilder()
                        {
                            IsInline = field.inline,
                            Name = field.name,
                            Value = field.value
                        }).ToList(),
                ImageUrl = embed.image?.url,
                ThumbnailUrl = embed.thumbnail?.url,
                Footer = embed.footer == null
                    ? null
                    : new EmbedFooterBuilder()
                    {
                        Text = embed.footer.text,
                        IconUrl = embed.footer.iconUrl
                    },
                Timestamp = embed.timestamp,
            }.Build()).ToArray() ?? [], new ComponentBuilder());
    }
}
