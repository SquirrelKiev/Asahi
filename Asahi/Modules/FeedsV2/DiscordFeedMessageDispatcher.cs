using Asahi.Database.Models.Rss;
using Discord.Webhook;

namespace Asahi.Modules.FeedsV2
{
    public class DiscordFeedMessageDispatcher(IDiscordClient client) : IFeedMessageDispatcher
    {
        public async Task SendMessages(FeedListener listener, IAsyncEnumerable<MessageContents> messages)
        {
            var guild = await client.GetGuildAsync(listener.GuildId);
            var channel = await guild.GetTextChannelAsync(listener.ChannelId);

            await foreach (var message in messages)
            {
                if (listener.WebhookName == null)
                {
                    await channel.SendMessageAsync(message.body, embeds: message.embeds, components: message.components,
                        allowedMentions: new AllowedMentions(AllowedMentionTypes.Users));
                }
                else
                {
                    var webhook = await channel.GetOrCreateWebhookAsync(listener.WebhookName, client.CurrentUser);
                    using var webhookClient = new DiscordWebhookClient(webhook);
                    
                    await webhookClient.SendMessageAsync(message.body, embeds: message.embeds, components: message.components,
                        allowedMentions: new AllowedMentions(AllowedMentionTypes.Users));
                }
            }
        }
    }
}
