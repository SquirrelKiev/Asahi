using April.Config;
using Asahi.Database;
using Asahi.Database.Models.April;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.April;

[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
#if !DEBUG
[DontAutoRegister]
#endif
public class ShopModule(
    InteractiveService interactiveService,
    AprilConfigService configService,
    IColorProviderService colorProviderService,
    IDbService dbService,
    AprilUtility aprilUtility,
    ILogger<ShopModule> logger) : BotModule
{
    [SlashCommand("shop", "Pulls up the shop.")]
    public async Task ShopSlash(
        [Summary(description: "An optional person to gift whatever you're gonna buy to.")]
        IGuildUser? giftee = null)
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
        
        UserData? gifteeUserData = null;
        if (giftee != null)
            gifteeUserData = await context.GetAprilUserData(Context.Guild.Id, giftee.Id);

        const int chunkCount = 5;
        int page = 0;
        
        bool timedOut = false;
        while (!timedOut)
        {
            var chunks = config.shopWares.Where(x => AprilUtility.CheckConditionStatus(x.conditions, gifteeUserData ?? userData, giftee ?? currentUser)).Chunk(chunkCount).ToArray();

            if (chunks.Length == 0)
            {
                await interaction.ModifyOriginalResponseAsync(new MessageContents(
                    "No items in the shop at the moment! You've probably bought them all."));

                return;
            }
            
            page = Math.Min(Math.Max(page, 0), chunks.Length - 1);

            var pageEmbed = new EmbedBuilder();

            var wares = chunks[page];

            pageEmbed.WithFields(wares.Select(y =>
                new EmbedFieldBuilder()
                    .WithName($"{y.name} - {AprilUtility.PrettyPrintCoinCounterAbbreviated(config, y.cost)}")
                    .WithValue(y.description)));

            var components = new ComponentBuilder()
                .WithButton("<", "left", ButtonStyle.Secondary, disabled: page <= 0)
                .WithButton(">", "right", ButtonStyle.Secondary, disabled: page >= chunks.Length - 1)
                .WithSelectMenu("buy",
                    wares.Select(x => new SelectMenuOptionBuilder().WithLabel(x.name).WithValue(x.Guid.ToString()))
                        .ToList());

            var msg = await interaction.ModifyOriginalResponseAsync(new MessageContents(pageEmbed, components));

            var res = await interactiveService.NextMessageComponentAsync(x =>
                x.Message.Id == msg.Id && x.User.Id == Context.User.Id && x.Data.CustomId is "left" or "right" or "buy");

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
            else if (res.Value.Data.CustomId == "buy")
            {
                var guid = Guid.Parse(res.Value.Data.Values.First());

                await interaction.DeferAsync();

                var ware = config.shopWares.FirstOrDefault(x => x.Guid == guid);
                if (ware == null)
                {
                    await interaction.ModifyOriginalResponseAsync(new MessageContents("Ware no longer exists!"));
                    timedOut = true;
                    continue;
                }

                var embed = new EmbedBuilder()
                    .WithTitle(
                        $"Are you sure you want to buy this for {AprilUtility.PrettyPrintCoinCounter(config, ware.cost)}?")
                    .WithFields(new EmbedFieldBuilder().WithName(ware.name).WithValue(ware.description))
                    .WithOptionalColor(await colorProviderService.GetEmbedColor(Context.Guild.Id));

                if (giftee != null)
                {
                    // terrible UI
                    embed.WithDescription($"This is a gift for <@{giftee.Id}>");
                }

                components = new ComponentBuilder()
                    .WithButton(giftee == null ? "Buy" : "Buy as gift", "yes", ButtonStyle.Success)
                    .WithButton("Back", "back", ButtonStyle.Secondary);

                msg = await interaction.ModifyOriginalResponseAsync(new MessageContents(embed, components));

                res =
                    await interactiveService.NextMessageComponentAsync(
                        x => x.Message.Id == msg.Id && x.User.Id == Context.User.Id && x.Data.CustomId is "yes" or "back",
                        timeout: TimeSpan.FromMinutes(2));

                if (!res.IsSuccess)
                {
                    logger.LogTrace("button check failed for reason {Reason}", res.Status);
                    await interaction.ModifyOriginalResponseAsync(x => x.Components = new ComponentBuilder().Build());
                    timedOut = true;
                    continue;
                }

                interaction = res.Value;

                if (res.Value.Data.CustomId is not "yes" and not "back")
                {
                    // something is wrong
                    logger.LogTrace("Unknown custom ID {customId}", res.Value.Data.CustomId);
                    timedOut = true;
                    continue;
                }

                if (res.Value.Data.CustomId == "back")
                    continue;

                await interaction.DeferAsync();

                if (!userData.RemoveCoinsFromUser(ware.cost))
                {
                    logger.LogTrace("{user} was too poor to afford {guid}. has {coins}, item is {cost}.",
                        Context.User.Id,
                        ware.Guid, userData.CoinBalance, ware.cost);
                    await interaction.ModifyOriginalResponseAsync(new MessageContents(
                        $"You can't afford that! You're {AprilUtility.PrettyPrintCoinCounter(config, ware.cost - userData.CoinBalance)} short."));
                    return;
                }

                GachaMessage message;
                if (gifteeUserData == null)
                {
                    message = await aprilUtility.ExecuteRewardActions(ware.actionsUponRedeem, context, userData, currentUser, Context.Channel);
                }
                else
                {
                    message = await aprilUtility.ExecuteRewardActions(ware.actionsUponRedeem, context,
                        gifteeUserData, giftee!, Context.Channel);
                }
                await context.SaveChangesAsync();

                var messageContents = AprilUtility.GachaMessageToMessageContents(message);
                messageContents.components =
                    new ComponentBuilder()
                        .WithButton("Back to shop", "back", ButtonStyle.Secondary).Build();

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
