using Discord.Commands;

namespace Zatsuyou.Modules.CustomCommands;

[Group("cmd")]
public class CustomCommandPrefixModule(CustomCommandService commandService, Fergun.Interactive.InteractiveService interactiveService) : PrefixModule
{
    [CommandsModCommand]
    [Command("add")]
    public async Task AddCommand(string name, [Remainder] string contents)
    {
        await DeferAsync();

        await ReplyAsync(await commandService.AddCustomCommand(await ((IGuild)Context.Guild).GetUserAsync(Context.User.Id), name, contents));
    }

    [CommandsModCommand]
    [Command("remove")]
    public async Task RemoveCommand(string name)
    {
        await DeferAsync();

        await ReplyAsync(await commandService.RemoveCustomCommand(await ((IGuild)Context.Guild).GetUserAsync(Context.User.Id), name));
    }

    [Command("listcmds")]
    public async Task ListCommands()
    {
        await DeferAsync();

        var paginator = await commandService.ListCommands(Context.User, Context.Guild);

        await interactiveService.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(5));
    }
}