using BotBase.Modules.About;
using Discord.Interactions;

namespace Asahi.Modules.About;

public class AboutModule(AboutService aboutService, OverrideTrackerService overrideTrackerService)
    : AboutModuleImpl(aboutService, overrideTrackerService)
{
    [SlashCommand("about", "Info about the bot.")]
    [HelpPageDescription("Pulls up info about the bot.")]
    public override Task AboutSlash()
    {
        return base.AboutSlash();
    }
}