using Discord.Interactions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Asahi.Modules.About;

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class AboutModule(AboutService aboutService, HealthCheckService healthCheckService) : BotModule
{
    [SlashCommand("about", "Info about the bot.")]
    public async Task AboutSlash()
    {
        var healthCheckTask = healthCheckService.CheckHealthAsync();
        
        var component = await aboutService.GetComponent(null);
        
        await RespondAsync(components: component, allowedMentions: AllowedMentions.None);

        var healthCheck = await healthCheckTask;
        
        component = await aboutService.GetComponent(healthCheck);
        
        await ModifyOriginalResponseAsync(x =>
        {
            x.Components = component;
            x.AllowedMentions = AllowedMentions.None;
        });
    }
}
