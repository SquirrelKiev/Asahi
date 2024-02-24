using BotBase;
using BotBase.Modules.ConfigCommand;
using Seigen.Modules.ConfigCommand.Pages;

namespace Seigen.Modules.ConfigCommand;

public class ConfigCommandModule : ConfigCommandModuleBase<ConfigPage.Page>
{
    public ConfigCommandModule(ConfigCommandServiceBase<ConfigPage.Page> configService) : base(configService)
    {
    }

    [SlashCommand("config", "Pulls up various options for configuring the bot to the server's needs.")]
    [RequireUserPermission(GuildPermission.ManageGuild, Group = ModulePrefixes.PERMISSION_GROUP)]
    [HasOverride(Group = ModulePrefixes.PERMISSION_GROUP)]
    [EnabledInDm(false)]
    public override Task ConfigSlash()
    {
        return base.ConfigSlash();
    }
}