using Asahi.Database;
using Asahi.Modules.Tatsu;
using Discord.Interactions;

namespace Asahi.Modules.April;

[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
[Group("coins", "Commands relating to coins.")]
#if !DEBUG
[DontAutoRegister]
#endif
public class EconomyModule(IDbService dbService, AprilConfigService configService) : BotModule
{
    [SlashCommand("balance", "Get your coin balance.")]
    public async Task GetBalance(IUser? user = null)
    {
        await DeferAsync();

        user ??= Context.User;

        await using var context = dbService.GetDbContext();

        var config = await configService.GetConfig(Context.Guild.Id, context);
        if (config == null)
        {
            await FollowupAsync("Config not loaded!");
            return;
        }

        var userData = await context.GetAprilUserData(Context.Guild.Id, user.Id);

        var userBalance = userData.CoinBalance;

        var embed = new EmbedBuilder()
            .WithDescription($"<@{user.Id}> currently has {AprilUtility.PrettyPrintCoinCounter(config, userBalance)}.")
            .WithColor(new Color(config.defaultEmbedColor));

        await FollowupAsync(new MessageContents(embed, new ComponentBuilder()));
    }

    [SlashCommand("claim-tatsu", "Claims coins based off your tatsu score.")]
    public async Task ClaimSlash()
    {
        await DeferAsync();

        // claimService.TryAddUser(Context.User.Id);
    }
}

[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
[DefaultMemberPermissions(GuildPermission.ManageRoles)]
[Group("econ-management", "Commands relating to managing coins.")]
public class AdminEconomyModule(IDbService dbService, AprilConfigService configService) : BotModule
{
        [SlashCommand("add", "Add coins to a user's balance.")]
    //[RequireUserPermission(GuildPermission.ManageGuild)]
    public async Task AddCoinsToBalance(IUser user, [MinValue(1)] int coinsToAdd)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var config = await configService.GetConfig(Context.Guild.Id, context);

        if (config == null)
        {
            await FollowupAsync("Config not loaded!");
            return;
        }

        var userData = await context.GetAprilUserData(Context.Guild.Id, user.Id);

        var oldBalance = userData.CoinBalance;
        var newBalance = userData.AddCoinsToUser(coinsToAdd);

        await context.SaveChangesAsync();

        var embed = new EmbedBuilder()
            .WithDescription($"<@{user.Id}> was at {AprilUtility.PrettyPrintCoinCounter(config, oldBalance)}, " +
                             $"and is now at {AprilUtility.PrettyPrintCoinCounter(config, newBalance)}.")
            .WithColor(new Color(config.defaultEmbedColor));

        await FollowupAsync(new MessageContents(embed, new ComponentBuilder()));
    }

    [SlashCommand("remove", "[ADMIN] Remove coins from a user's balance.")]
    //[RequireUserPermission(GuildPermission.ManageGuild)]
    public async Task RemoveCoinsFromBalance(IUser user, [MinValue(1)] int coinsToRemove)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var config = await configService.GetConfig(Context.Guild.Id, context);

        if (config == null)
        {
            await FollowupAsync("Config not loaded!");
            return;
        }

        var userData = await context.GetAprilUserData(Context.Guild.Id, user.Id);

        var oldBalance = userData.CoinBalance;
        if (userData.RemoveCoinsFromUser(coinsToRemove))
        {
            var errorEmbed = new EmbedBuilder()
                .WithDescription($"Could not remove {AprilUtility.PrettyPrintCoinCounter(config, coinsToRemove)} from <@{user.Id}>, " +
                                 $"as that would place them below {AprilUtility.PrettyPrintCoinCounter(config, 0)}.");

            await FollowupAsync(new MessageContents(errorEmbed));
            return;
        }
        var newBalance = userData.CoinBalance;

        await context.SaveChangesAsync();

        var embed = new EmbedBuilder()
            .WithDescription($"<@{user.Id}> was at {AprilUtility.PrettyPrintCoinCounter(config, oldBalance)}, " +
                             $"and is now at {AprilUtility.PrettyPrintCoinCounter(config, newBalance)}.")
            .WithColor(new Color(config.defaultEmbedColor));

        await FollowupAsync(new MessageContents(embed, new ComponentBuilder()));
    }

    [SlashCommand("set", "Set a user's balance to the specified value.")]
    //[RequireUserPermission(GuildPermission.ManageGuild)]
    public async Task SetUserBalance(IUser user, [MinValue(0)] int newBalance)
    {
        await DeferAsync(true);

        await using var context = dbService.GetDbContext();

        var config = await configService.GetConfig(Context.Guild.Id, context);

        if (config == null)
        {
            await FollowupAsync("Config not loaded!");
            return;
        }

        var userData = await context.GetAprilUserData(Context.Guild.Id, user.Id);

        var oldBalance = userData.CoinBalance;

        userData.CoinBalance = newBalance;

        await context.SaveChangesAsync();

        var embed = new EmbedBuilder()
            .WithDescription($"<@{user.Id}> was at {AprilUtility.PrettyPrintCoinCounter(config, oldBalance)}, " +
                             $"and is now at {AprilUtility.PrettyPrintCoinCounter(config, newBalance)}.")
            .WithColor(new Color(config.defaultEmbedColor));

        await FollowupAsync(new MessageContents(embed, new ComponentBuilder()));
    }
}
