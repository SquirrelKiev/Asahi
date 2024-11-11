using Asahi.Database;
using Discord.Interactions;
using Discord.WebSocket;

namespace Asahi.Modules;

public class InteractionsModCommandAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        var dbService = services.GetRequiredService<IDbService>();

        await using var dbContext = dbService.GetDbContext();

        var config = services.GetRequiredService<BotConfig>();
        var overrideService = services.GetRequiredService<OverrideTrackerService>();

        if (config.ManagerUserIds.Contains(context.User.Id) && overrideService.HasOverride(context.User.Id))
        {
            return PreconditionResult.FromSuccess();
        }

        if (context.User is SocketGuildUser user)
        {
            //var guildConfig = await dbContext.GetGuildConfig(user.Guild.Id);

            if (user.GuildPermissions.ManageGuild 
                //|| user.Roles.Any(x => x.Id == guildConfig.ModRole)
                )
                return PreconditionResult.FromSuccess();
            else
                return PreconditionResult.FromError("No permission.");
        }
        else
            return PreconditionResult.FromError("You must be in a guild to run this command.");
    }
}