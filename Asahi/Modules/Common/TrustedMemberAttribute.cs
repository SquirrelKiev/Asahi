using Asahi.Database;
using Discord.Interactions;

namespace Asahi.Modules;

public class TrustedMemberAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        var config = services.GetRequiredService<BotConfig>();

        var isTrusted = config.ManagerUserIds.Contains(context.User.Id);
        if (!isTrusted)
        {
            await using var dbContext = services.GetRequiredService<DbService>().GetDbContext();

            var botWideConfig = await dbContext.GetBotWideConfig();

            isTrusted = botWideConfig.TrustedIds.Any(x => x.Id == context.User.Id);

            if (!isTrusted && context.Channel != null)
                isTrusted = isTrusted || botWideConfig.TrustedIds.Any(x => x.Id == context.Channel.Id);

            if (!isTrusted && context.Guild != null)
                isTrusted = isTrusted || botWideConfig.TrustedIds.Any(x => x.Id == context.Guild.Id);
        }

        return isTrusted ? 
            PreconditionResult.FromSuccess() : 
            PreconditionResult.FromError("Only trusted users are allowed to use this command.");
    }
}