using Asahi.Database;
using Asahi.Database.Models;
using Discord.Interactions;

namespace Asahi.Modules;

public class TrustedMemberAttribute(TrustedUserPerms requiredPerms) : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        var config = services.GetRequiredService<BotConfig>();

        var isTrusted = config.ManagerUserIds.Contains(context.User.Id);
        if (!isTrusted)
        {
            await using var dbContext = services.GetRequiredService<BotDbContext>();

            var botWideConfig = await dbContext.GetBotWideConfig(context.Client.CurrentUser.Id);

            isTrusted = botWideConfig.TrustedIds.Any(x => x.Id == context.User.Id && x.PermissionFlags.HasFlag(requiredPerms));

            if (!isTrusted && context.Channel != null)
                isTrusted = isTrusted || botWideConfig.TrustedIds.Any(x => x.Id == context.Channel.Id && x.PermissionFlags.HasFlag(requiredPerms));

            if (!isTrusted && context.Guild != null)
                isTrusted = isTrusted || botWideConfig.TrustedIds.Any(x => x.Id == context.Guild.Id && x.PermissionFlags.HasFlag(requiredPerms));
        }

        return isTrusted ? 
            PreconditionResult.FromSuccess() : 
            PreconditionResult.FromError("Only trusted users are allowed to use this command.");
    }
}
