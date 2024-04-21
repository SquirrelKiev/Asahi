using Asahi.Database;
using Discord.Commands;
using Discord.WebSocket;

namespace Asahi.Modules;

public class CommandsModCommandAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        var dbService = services.GetRequiredService<DbService>();

        await using var dbContext = dbService.GetDbContext();

        var config = services.GetRequiredService<BotConfigBase>();
        var overrideService = services.GetRequiredService<OverrideTrackerService>();

        if (config.ManagerUserIds.Contains(context.User.Id) && await overrideService.HasOverride(context.User.Id))
        {
            return PreconditionResult.FromSuccess();
        }

        if (context.User is SocketGuildUser user)
        {
            var guildConfig = await dbContext.GetGuildConfig(user.Guild.Id);

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