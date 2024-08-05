namespace Asahi.Modules;

public static class WebhookExtensions
{
    // userFilter should be the bot user
    public static async Task<IWebhook> GetOrCreateWebhookAsync(this IIntegrationChannel channel, string name, IUser? userFilter = null)
    {
        var webhooks = await channel.GetWebhooksAsync();

        IWebhook? webhook;
        if (userFilter != null)
        {
            webhook = webhooks.FirstOrDefault(x =>
                x.Name == name && x.Creator.Id == userFilter.Id);
        }
        else
        {
            webhook = webhooks.FirstOrDefault(x =>
                x.Name == name);
        }

        if (webhook != null)
            return webhook;

        return await channel.CreateWebhookAsync(name);
    }
}
