using System.Text;
using April.Config;
using Asahi.Database;
using Discord.Interactions;
using Newtonsoft.Json;

namespace Asahi.Modules.April;

[DefaultMemberPermissions(GuildPermission.ManageRoles)]
[Group("config", "Commands for managing the Guild's config.")]
[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
#if !DEBUG
[DontAutoRegister]
#endif
public class ConfigManagementModule(IDbService dbService, AprilConfigService configService) : BotModule
{
    [SlashCommand("get", "Sends you in DMs the current config.")]
    public async Task GetConfig()
    {
        await DeferAsync(true);

        await using var context = dbService.GetDbContext();

        var config = await context.GetGuildConfig(Context.Guild.Id);

        if (config.AprilConfig == null)
        {
            await FollowupAsync("No config loaded!");
            return;
        }

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(JsonConvert.DeserializeObject<ConfigFile>(config.AprilConfig), Formatting.Indented))
            );
        await (await Context.User.CreateDMChannelAsync()).SendFileAsync(new FileAttachment(stream, $"{config.GuildId}.json", isSpoiler: true));

        await FollowupAsync("Sent to DMs.");
    }

    [SlashCommand("set", "Sets the config.")]
    public async Task SetConfig(IAttachment attachment)
    {
        await DeferAsync(true);

        // TODO: This should be injected.
        var httpClient = new HttpClient();

        using var req = await httpClient.GetAsync(attachment.Url);

        var contents = await req.Content.ReadAsStringAsync();

        var json = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<ConfigFile>(contents), Formatting.None);

        var error = await configService.SetConfig(Context.Guild.Id, json);
        if (error != null)
        {
            await FollowupAsync(error);
            return;
        }

        await FollowupAsync("Config set successfully :D");
    }

    // [SlashCommand("toggle-rolling", "Enables or disables the roll command.")]
    // public async Task ToggleGacha()
    // {
    //     await DeferAsync(true);
    //
    //     await using var context = dbService.GetDbContext();
    //
    //     var guildConfig = await context.GetGuildConfig(Context.Guild.Id);
    //
    //     guildConfig.AprilEnabled = !guildConfig.AprilEnabled;
    //
    //     await context.SaveChangesAsync();
    //
    //     await FollowupAsync($"Rolling is now {(guildConfig.GachaEnabled ? "enabled" : "disabled")}.");
    // }
}
