using Asahi.Database;
using Asahi.Database.Models;
using Discord.Interactions;
using Discord.WebSocket;
using FluentResults;
using Microsoft.VisualBasic;
using Newtonsoft.Json;

namespace Asahi.Modules.Welcome;

[Group("welcome-config", "Commands related to configuration of the welcome module.")]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
public class WelcomeConfigModule(IDbService dbService, WelcomeService welcomeService) : BotModule
{
    [SlashCommand("set-template", "Sets the welcome message template.")]
    public async Task SetWelcomeJsonSlash(
        [Summary(description: "JSON template of a message. discohook.org is your friend! Template language is Fluid.")]
        string jsonTemplate)
    {
        await CommonWelcomeConfig(Context.Guild.Id, async (context) =>
        {
            var parsedMessageRes =
                await welcomeService.ParseJson(jsonTemplate, ((SocketGuild)Context.Guild).GetUser(Context.User.Id));

            if (parsedMessageRes.IsFailed)
            {
                return new ConfigChangeResult(parsedMessageRes.Errors);
            }

            context.Config.WelcomeMessageJson = jsonTemplate;

            return new ConfigChangeResult(true, "Successfully set the welcome message JSON.");
        });
    }
    
    [SlashCommand("get-template", "Gets the welcome message template.")]
    public async Task GetWelcomeJsonSlash()
    {
        await DeferAsync();
        
        await using var context = dbService.GetDbContext();

        var config = await context.GetGuildConfig(Context.Guild.Id);

        var json = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<object>(config.WelcomeMessageJson), Formatting.Indented);

        if (json.Length > 1900)
        {
            await FollowupWithFileAsync(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)),
                $"Welcome Message Template.json"
            );
        }
        else
        {
            await FollowupAsync($"```json\n{json}\n```");
        }
    }

    [SlashCommand("test-json", "Tests a welcome message template.")]
    public async Task TestWelcomeJsonSlash(
        [Summary(description: "JSON template of a message. discohook.org is your friend! Template language is Fluid.")]
        string jsonTemplate)
    {
        await DeferAsync();
        
        await using var context = dbService.GetDbContext();

        var config = await context.GetGuildConfig(Context.Guild.Id);

        var welcomeMessageRes =
            await welcomeService.ParseJson(jsonTemplate, ((SocketGuild)Context.Guild).GetUser(Context.User.Id));

        if (welcomeMessageRes.IsFailed)
        {
            var embeds = ConfigUtilities.CreateEmbeds(await Context.Guild.GetCurrentUserAsync(), new EmbedBuilder(),
                new ConfigChangeResult(welcomeMessageRes.Errors));

            await FollowupAsync(embeds: embeds);
            
            return;
        }
        
        var welcomeMessage = welcomeMessageRes.Value;

        await FollowupAsync("See below for welcome message.");
        
        await Context.Channel.SendMessageAsync(welcomeMessage.Content, welcomeMessage.Tts ?? false, null, null,
            welcomeMessage.AllowedMentions.ToAllowedMentions(), null, null, null,
            welcomeMessage.Embeds?.Select(x => x.ToEmbedBuilder().Build()).ToArray(), welcomeMessage.MessageFlags);
    }

    [SlashCommand("set-welcome-channel", "Sets the channel to send welcome messages in.")]
    public async Task SetWelcomeChannel(
        [Summary(description: "Channel to send welcome messages to.")]
        ITextChannel channel)
    {
        await CommonWelcomeConfig(Context.Guild.Id, async (context) =>
        {
            var perms = (await Context.Guild.GetUserAsync(Context.Client.CurrentUser.Id)).GetPermissions(channel);

            if (perms.SendMessages != true || perms.ViewChannel != true)
            {
                return new ConfigChangeResult(false, "Bot does not have permission to send messages there.");
            }

            context.Config.WelcomeMessageChannelId = channel.Id;

            return new ConfigChangeResult(true, $"Set welcome message channel to {channel.Mention}.");
        });
    }

    [SlashCommand("toggle", "Whether to send welcome messages or not.")]
    public async Task ToggleWelcomeMessagesSlash(
        [Summary(description: "True to send welcome messages; False to not.")]
        bool toggle)
    {
        await CommonWelcomeConfig(Context.Guild.Id, (context) =>
        {
            if (context.Config.WelcomeMessageJson == "")
            {
                return Task.FromResult(new ConfigChangeResult(false, "Welcome message template not set!"));
            }

            if (context.Config.WelcomeMessageChannelId == 0ul)
            {
                if (Context.Guild.SystemChannelId.HasValue)
                {
                    context.Config.WelcomeMessageChannelId = Context.Guild.SystemChannelId.Value;
                }
                else
                {
                    return Task.FromResult(new ConfigChangeResult(false, "Channel to send welcome messages to is not set!"));
                }
            }

            context.Config.ShouldSendWelcomeMessage = toggle;

            return Task.FromResult(new ConfigChangeResult(true, $"Welcome messages are now {(toggle ? "enabled" : "disabled")}."));
        });
    }

    private Task<bool> CommonConfig(Func<BotDbContext, EmbedBuilder, Task<ConfigChangeResult>> updateAction)
    {
        return ConfigUtilities.CommonConfig(Context, dbService, updateAction);
    }

    public record ConfigContext(BotDbContext Context, GuildConfig Config, EmbedBuilder EmbedBuilder);

    private Task<bool> CommonWelcomeConfig(ulong guildId, Func<ConfigContext, Task<ConfigChangeResult>> updateAction)
    {
        return CommonConfig(async (context, eb) =>
        {
            try
            {
                var config = await context.GetGuildConfig(guildId);

                return await updateAction(new ConfigContext(context, config, eb));
            }
            catch (ConfigException ex)
            {
                return new ConfigChangeResult(false, ex.Message);
            }
        });
    }
}
