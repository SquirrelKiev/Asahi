using Seigen.Modules.ConfigCommand.Pages;
using BotBase.Modules.ConfigCommand;

namespace DibariBot.Modules.ConfigCommand;

public class ConfigCommandService : ConfigCommandServiceBase<ConfigPage.Page>
{
    public ConfigCommandService(IServiceProvider services) : base(services)
    {
    }
}