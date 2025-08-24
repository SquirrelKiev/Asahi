using Discord.Interactions;

namespace Asahi.Modules
{
    public class RequireCommandInvokerAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
            ICommandInfo commandInfo, IServiceProvider services)
        {
            if (context.Interaction is not IComponentInteraction componentInteraction)
                return Task.FromResult(
                    PreconditionResult.FromError("This precondition can only be used in components."));

            var ogRes = componentInteraction.Message;

            var ogAuthor = ogRes.InteractionMetadata?.User.Id;

            if (ogAuthor == null || ogAuthor != componentInteraction.User.Id)
            {
                return Task.FromResult(
                    PreconditionResult.FromError(
                        "You did not originally trigger this. Please run the command yourself."));
            }
            else
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
        }
    }
}
