using System.Text;
using Discord.Interactions;
using Fergun.Interactive;

namespace Asahi.Modules.CustomCommands;

[CommandContextType(InteractionContextType.Guild)]
[Group("commands", "Stuff related to custom commands.")]
public class CustomCommandModule(CustomCommandService commandService, InteractiveService interactiveService) : BotModule
{
    [InteractionsModCommand]
    [SlashCommand("add", "Adds a custom command.")]
    public async Task AddCommand(string name, string contents)
    {
        await DeferAsync();

        await FollowupAsync(await commandService.AddCustomCommand(await Context.Guild.GetUserAsync(Context.User.Id), name, contents));
    }

    [InteractionsModCommand]
    [SlashCommand("remove", "Removes a custom command (if you own it).")]
    public async Task RemoveCommand(string name)
    {
        await DeferAsync();

        await FollowupAsync(await commandService.RemoveCustomCommand(await Context.Guild.GetUserAsync(Context.User.Id), name));
    }

    [SlashCommand("list", "Lists all the custom commands.")]
    public async Task ListCommands()
    {
        var paginator = await commandService.ListCommands(Context.User, Context.Guild);

        await interactiveService.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(5));
    }

    [InteractionsModCommand]
    [SlashCommand("export", "Exports all the commands added to the Guild as JSON.")]
    public async Task ExportCommands()
    {
        var json = await commandService.CommandsAsJson(Context.Guild);

        await RespondWithFileAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)), "commands.json",
            "tada heres the stuff");
    }
}