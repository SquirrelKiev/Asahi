using Discord.Interactions;

namespace Asahi.Modules.About;

public class AboutModule(AboutService aboutService, OverrideTrackerService overrideTrackerService) : BotModule
{
    [SlashCommand("about", "Info about the bot.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm)]
    public async Task AboutSlash()
    {
        await DeferAsync();

        var contents = aboutService.GetMessageContents(await AboutService.GetPlaceholders(Context.Client), Context.User.Id);

        await FollowupAsync(contents);
    }

    [ComponentInteraction(ModulePrefixes.ABOUT_OVERRIDE_TOGGLE)]
    public async Task OverrideToggleButton()
    {
        await DeferAsync();

        if (overrideTrackerService.TryToggleOverride(Context.User.Id))
        {
            var contents = aboutService.GetMessageContents(await AboutService.GetPlaceholders(Context.Client), Context.User.Id);

            await ModifyOriginalResponseAsync(contents);
        }
        else
        {
            await RespondAsync(new MessageContents("No permission."), true);
        }
    }
}