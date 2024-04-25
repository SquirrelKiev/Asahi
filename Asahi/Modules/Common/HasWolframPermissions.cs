using Discord.Interactions;

namespace Asahi.Modules;

public class HasWolframPermissions : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        var config = services.GetRequiredService<BotConfig>();

        var isTrusted = config.WolframTrustedIds.Contains(context.User.Id);

        if(!isTrusted && context.Channel != null)
            isTrusted = isTrusted || config.WolframTrustedIds.Contains(context.Channel.Id);

        if (!isTrusted && context.Guild != null)
            isTrusted = isTrusted || config.WolframTrustedIds.Contains(context.Guild.Id);

        return Task.FromResult(isTrusted ? 
            PreconditionResult.FromSuccess() : 
            PreconditionResult.FromError("Only trusted users are allowed to use this command."));
    }
}