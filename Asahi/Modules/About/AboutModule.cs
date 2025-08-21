using Discord.Interactions;

namespace Asahi.Modules.About;

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class AboutModule(AboutService aboutService) : BotModule
{
    [SlashCommand("about", "Info about the bot.")]
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
}
