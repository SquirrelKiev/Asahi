using Asahi.Database;
using Discord.WebSocket;
using Fluid;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Asahi.Modules.Welcome;

[Inject(ServiceLifetime.Singleton)]
public class WelcomeService(IDbContextFactory<BotDbContext> dbService, ILogger<WelcomeService> logger)
{
    private readonly FluidParser parser = new();
    
    public async ValueTask<Result<MessageModel>> ParseJson(string json, SocketGuildUser user)
    {
        MessageModel? message;
        try
        {
            var template = parser.Parse(json);

            var data = new
            {
                user = new
                {
                    id = user.Id,
                    mention = user.Mention,
                    
                    createdAt = user.CreatedAt,
                    
                    avatarUrl = user.GetAvatarUrl(size: 512),
                    serverAvatarUrl = user.GetGuildAvatarUrl(size: 512),
                    displayAvatarUrl = user.GetDisplayAvatarUrl(size: 512),
                    username = user.Username,
                    nickname = user.Nickname,
                    displayName = user.DisplayName,
                    
                    joinTimestamp = user.JoinedAt,
                    accountCreated = user.CreatedAt
                },
                guild = new
                {
                    id = user.Guild.Id,
                    name = user.Guild.Name,
                    iconUrl = user.Guild.IconUrl,
                    bannerUrl = user.Guild.BannerUrl,
                    memberCount = user.Guild.MemberCount,
                    vanityUrlCode = user.Guild.VanityURLCode
                }
            };

            var templateContext = new TemplateContext(data);
            templateContext.Options.MemberAccessStrategy.Register(data.guild.GetType());
            templateContext.Options.MemberAccessStrategy.Register(data.user.GetType());
            var result = await template.RenderAsync(templateContext);
            
            message = JsonConvert.DeserializeObject<MessageModel>(result);
        }
        catch (Exception ex)
        {
            return Result<MessageModel>.Fail($"{ex.GetType()}: {ex.Message}");
        }


        if (message is null)
        {
            return Result<MessageModel>.Fail("JSON deserialized as null.");
        }

        return Result<MessageModel>.Ok(message);
    }

    public async Task OnUserJoined(SocketGuildUser user)
    {
        await using var context = await dbService.CreateDbContextAsync();

        var config = await context.GetGuildConfig(user.Guild.Id);
        if (!config.ShouldSendWelcomeMessage)
            return;

        var welcomeMessageRes = await ParseJson(config.WelcomeMessageJson, user);

        if (welcomeMessageRes.IsFailed)
        {
            logger.LogWarning("Failed to parse welcome message for Guild {guildId}. {error}", user.Guild.Id,
                welcomeMessageRes.Error);
            return;
        }

        var welcomeMessage = welcomeMessageRes.Value;

        var channel = user.Guild.GetTextChannel(config.WelcomeMessageChannelId);

        if (channel is null)
        {
            logger.LogWarning("Failed to get welcome channel for Guild {guildId}.", user.Guild.Id);
            return;
        }

        await channel.SendMessageAsync(welcomeMessage.Content, welcomeMessage.Tts ?? false, null, null,
            welcomeMessage.AllowedMentions?.ToAllowedMentions(), null, null, null,
            welcomeMessage.Embeds?.Select(x => x.ToEmbedBuilder().Build()).ToArray(), welcomeMessage.MessageFlags);
    }
}
