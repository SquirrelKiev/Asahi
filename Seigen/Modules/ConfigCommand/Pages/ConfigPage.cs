using BotBase.Modules.ConfigCommand;

namespace Seigen.Modules.ConfigCommand.Pages;

public abstract class ConfigPage : ConfigPageBase<ConfigPage.Page>
{
    public enum Page
    {
        Help,
        Trackables
    }

    public override bool EnabledInDMs => false;
}
