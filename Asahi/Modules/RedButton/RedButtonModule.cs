using Discord.Interactions;

namespace Asahi.Modules.RedButton
{
    public class RedButtonModule : BotModule
    {
        [ComponentInteraction(ModulePrefixes.RedButton)]
        public async Task OnButton()
        {
            await DeferAsync();
            await Context.Interaction.DeleteOriginalResponseAsync();
        }
    }

    public static class RedButtonExtensions
    {
        public static ComponentBuilder WithRedButton(this ComponentBuilder componentBuilder, string label = "X", int row = 0)
        {
            componentBuilder.WithButton(label, ModulePrefixes.RedButton, ButtonStyle.Danger, row: row);

            return componentBuilder;
        }
    }
}
