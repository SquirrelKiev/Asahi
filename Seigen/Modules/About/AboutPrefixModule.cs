using BotBase;
using BotBase.Modules.About;
using Discord.Commands;

namespace Seigen.Modules.About;

public class AboutPrefixModule : PrefixModule
{
    private readonly AboutService aboutService;

    public AboutPrefixModule(AboutService aboutService)
    {
        this.aboutService = aboutService;
    }

    [Command("about")]
    [ParentModulePrefix(typeof(AboutModule))]
    public async Task AboutCommand()
    {
        await DeferAsync();

        var contents = await aboutService.GetMessageContents(await AboutService.GetPlaceholders(Context.Client), Context.User.Id);

        await ReplyAsync(contents);
    }
}