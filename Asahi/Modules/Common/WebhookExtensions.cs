namespace Asahi.Modules;

public static class WebhookExtensions
{
    public static async Task<IWebhook> GetOrCreateWebhookAsync(this IIntegrationChannel channel, string name, IUser? userFilter = null)
    {
        var webhooks = await channel.GetWebhooksAsync();

        var webhook = webhooks.FirstOrDefault(x => (userFilter == null || x.Creator.Id == userFilter.Id) && x.Name == name);

        if (webhook != null)
            return webhook;

        return await channel.CreateWebhookAsync(name);
    }
}
