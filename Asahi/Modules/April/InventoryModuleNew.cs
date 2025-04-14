using April.Config;
using Asahi.Database;
using Asahi.Database.Models.April;
using Discord.Interactions;
using Fergun.Interactive;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.April;

[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
#if !DEBUG
[DontAutoRegister]
#endif
public class InventoryModuleNew(
    IDbService dbService,
    AprilConfigService configService,
    InteractiveService interactiveService,
    ILogger<InventoryModuleNew> logger,
    IColorProviderService colorProviderService,
    AprilUtility aprilUtility) : BotModule
{
    [SlashCommand("inventory", "Opens your inventory.")]
    public async Task InventorySlash()
    {
        var interaction = Context.Interaction;

        await interaction.DeferAsync();

        await using var context = dbService.GetDbContext();

        var config = await configService.GetConfig(Context.Guild.Id, context);

        if (config == null)
        {
            await interaction.ModifyOriginalResponseAsync(new MessageContents("Config not loaded!"));
            return;
        }

        var userData = await context.GetAprilUserData(Context.Guild.Id, Context.User.Id);
        var currentUser = await Context.Guild.GetUserAsync(Context.User.Id);

        const int chunkCount = 5;
        int page = 0;

        bool timedOut = false;
        while (!timedOut)
        {
            var chunks = userData.InventoryItems.Chunk(chunkCount).ToArray();

            if (chunks.Length == 0)
            {
                await interaction.ModifyOriginalResponseAsync(new MessageContents(
                    "You don't have anything in your inventory!"));

                return;
            }

            page = Math.Min(Math.Max(page, 0), chunks.Length - 1);

            var pageEmbed = new EmbedBuilder();

            var items = chunks[page];
            var resolvedItems = config.items.Select(item => config.items.FirstOrDefault(y => item.Guid == y.Guid))
                .Where(x => x != null).Cast<RewardItem>().ToArray();

            pageEmbed.WithFields(resolvedItems.Select(item =>
                new EmbedFieldBuilder()
                    .WithName(item.name)
                    .WithValue(item.hasEquipActions
                        ? $"{item.description}\n*{(items.First(x => x.ItemGuid == item.Guid).IsEquipped ? "Currently equipped" : "Not equipped")}*"
                        : item.description)));

            var components = new ComponentBuilder()
                .WithButton("<", "left", ButtonStyle.Secondary, disabled: page <= 0)
                .WithButton(">", "right", ButtonStyle.Secondary, disabled: page >= chunks.Length - 1)
                .WithSelectMenu("inspect", resolvedItems
                    .Select(x => new SelectMenuOptionBuilder().WithLabel(x.name).WithValue(x.Guid.ToString()))
                    .ToList(), "Inspect an item...");

            var msg = await interaction.ModifyOriginalResponseAsync(new MessageContents(pageEmbed, components));

            var res = await interactiveService.NextMessageComponentAsync(x =>
                x.Message.Id == msg.Id && x.User.Id == Context.User.Id && x.Data.CustomId is "left" or "right" or "inspect");

            if (!res.IsSuccess)
            {
                logger.LogTrace("button check failed for reason {Reason}", res.Status);
                await interaction.ModifyOriginalResponseAsync(x => x.Components = new ComponentBuilder().Build());
                timedOut = true;
                continue;
            }

            interaction = res.Value;

            if (res.Value.Data.CustomId == "left")
            {
                await interaction.DeferAsync();
                page += 1;
            }
            else if (res.Value.Data.CustomId == "right")
            {
                await interaction.DeferAsync();
                page -= 1;
            }
            else if (res.Value.Data.CustomId == "inspect")
            {
                await interaction.DeferAsync();

                var guid = Guid.Parse(res.Value.Data.Values.First());

                var item = items.FirstOrDefault(x => x.ItemGuid == guid);
                var resolvedItem = resolvedItems.FirstOrDefault(x => x.Guid == guid);
                if (item == null || resolvedItem == null)
                {
                    await interaction.ModifyOriginalResponseAsync(new MessageContents("Item no longer exists!"));
                    timedOut = true;
                    continue;
                }

                var embed = new EmbedBuilder()
                    .WithTitle(resolvedItem.name)
                    .WithDescription(resolvedItem.description)
                    .WithImageUrl(resolvedItem.imageUrl);

                components = new ComponentBuilder();

                if (resolvedItem.hasUseActions)
                {
                    components.WithButton("Use", "use");
                }

                if (resolvedItem.hasEquipActions)
                {
                    components.WithButton(item.IsEquipped ? "De-Equip" : "Equip", "toggle-equip");
                }

                components.WithButton("Back", "back", ButtonStyle.Secondary);

                msg = await interaction.ModifyOriginalResponseAsync(new MessageContents(embed, components));

                res =
                    await interactiveService.NextMessageComponentAsync(
                        x => x.Message.Id == msg.Id && x.User.Id == Context.User.Id && x.Data.CustomId is "use" or "toggle-equip" or "back",
                        timeout: TimeSpan.FromMinutes(2));

                if (!res.IsSuccess)
                {
                    logger.LogTrace("button check failed for reason {Reason}", res.Status);
                    await interaction.ModifyOriginalResponseAsync(x => x.Components = new ComponentBuilder().Build());
                    timedOut = true;
                    continue;
                }

                interaction = res.Value;

                if (res.Value.Data.CustomId is not "use" and not "toggle-equip" and not "back")
                {
                    // something is wrong
                    logger.LogTrace("Unknown custom ID {customId}", res.Value.Data.CustomId);
                    timedOut = true;
                    continue;
                }

                await interaction.DeferAsync();

                if (res.Value.Data.CustomId == "back")
                    continue;
                
                else if(res.Value.Data.CustomId == "use")
                {
                    var message = await aprilUtility.ExecuteRewardActions(resolvedItem.useActions, context, userData,
                        await Context.Guild.GetUserAsync(Context.User.Id), Context.Channel);

                    await context.SaveChangesAsync();
                    
                    var messageContents = AprilUtility.GachaMessageToMessageContents(message);
                    messageContents.components =
                        new ComponentBuilder()
                            .WithButton("Back", "back", ButtonStyle.Secondary).Build();

                    await interaction.ModifyOriginalResponseAsync(messageContents);
                    
                    res = await interactiveService.NextMessageComponentAsync(x =>
                        x.Message.Id == msg.Id && x.User.Id == Context.User.Id && x.Data.CustomId == "back");
                
                    if (res.IsSuccess)
                    {
                        interaction = res.Value;
                        await interaction.DeferAsync();
                        continue;
                    }

                    logger.LogTrace("button check failed for reason {Reason}", res.Status);
                    await interaction.ModifyOriginalResponseAsync(x => x.Components = new ComponentBuilder().Build());
                    timedOut = true;
                }
                else if(res.Value.Data.CustomId == "toggle-equip")
                {
                    var actions = item.IsEquipped ? resolvedItem.deEquipActions : resolvedItem.equipActions;
                    
                    var message = await aprilUtility.ExecuteRewardActions(actions, context, userData,
                        currentUser, Context.Channel);

                    item.IsEquipped = !item.IsEquipped;

                    await context.SaveChangesAsync();
                    
                    var messageContents = AprilUtility.GachaMessageToMessageContents(message);
                    messageContents.components =
                        new ComponentBuilder()
                            .WithButton("Back", "back", ButtonStyle.Secondary).Build();

                    await interaction.ModifyOriginalResponseAsync(messageContents);
                    
                    res = await interactiveService.NextMessageComponentAsync(x =>
                        x.Message.Id == msg.Id && x.User.Id == Context.User.Id && x.Data.CustomId == "back");
                
                    if (res.IsSuccess)
                    {
                        interaction = res.Value;
                        await interaction.DeferAsync();
                        continue;
                    }

                    logger.LogTrace("button check failed for reason {Reason}", res.Status);
                    await interaction.ModifyOriginalResponseAsync(x => x.Components = new ComponentBuilder().Build());
                    timedOut = true;
                }
            }
        }
    }
}
