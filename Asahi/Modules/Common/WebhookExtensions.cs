namespace Asahi.Modules;

public static class WebhookExtensions
{
    public static async Task<IWebhook> GetOrCreateWebhookAsync(this IIntegrationChannel channel, string name)
    {
        var webhooks = await channel.GetWebhooksAsync();

        var webhook = webhooks.FirstOrDefault(x => 
            x.Name == name);

        if (webhook != null)
            return webhook;

        return await channel.CreateWebhookAsync(name);
    }
}
