using Discord.Interactions;

namespace Asahi.Modules.About;

public class AboutModule(AboutService aboutService, OverrideTrackerService overrideTrackerService) : BotModule
{
    [SlashCommand("about", "Info about the bot.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm)]
    public async Task AboutSlash()
    {
        await DeferAsync();

        IGuildUser? us = null;
        if (Context.Guild != null)
        {
            us = await Context.Guild.GetCurrentUserAsync();
        }

        var contents = aboutService.GetMessageContents(await AboutService.GetPlaceholders(Context.Client), Context.User.Id, us);

        await FollowupAsync(contents);
    }

    [ComponentInteraction(ModulePrefixes.ABOUT_OVERRIDE_TOGGLE)]
    public async Task OverrideToggleButton()
    {
        await DeferAsync();

        if (overrideTrackerService.TryToggleOverride(Context.User.Id))
        {
            IGuildUser? us = null;
            if (Context.Guild != null)
            {
                us = await Context.Guild.GetCurrentUserAsync();
            }

            var contents = aboutService.GetMessageContents(await AboutService.GetPlaceholders(Context.Client), Context.User.Id, us);

            await ModifyOriginalResponseAsync(contents);
        }
        else
        {
            await RespondAsync(new MessageContents("No permission."), true);
        }
    }
}