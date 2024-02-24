using BotBase;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace Seigen.Modules.Ping;

[Inject(ServiceLifetime.Singleton)]
public class PingService
{
    public MessageContents GetMessageContents(DiscordSocketClient client)
    {
        return new MessageContents($"Pong! Client latency is {client.Latency}ms.", embed: null, null);
    }
}