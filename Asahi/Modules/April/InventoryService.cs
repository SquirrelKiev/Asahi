using Asahi.Database;
using Asahi.Database.Models.April;
using Discord.Interactions;

namespace Asahi.Modules.April;

[Inject(ServiceLifetime.Singleton)]
#if !DEBUG
[DontAutoRegister]
#endif
public class InventoryService(IDbService dbService, AprilConfigService configService)
{
    public class InventoryItemInspectState
    {
        public required uint inventoryKey;
    }

    public class InventoryContentsState
    {
        public ulong userId;
    }

    public class InventoryItemInspectActionState
    {
        public enum ActionType
        {
            Use,
            ToggleEquip
        }

        public required ActionType type;
        public required InventoryItemInspectState inventoryItemInspectState;
        public required InventoryContentsState inventoryContentsState;
    }

    public async Task<MessageContents> GetInventoryContentsMessage(ulong guildId, InventoryContentsState state,
        UserData? userData = null)
    {
        await using var context = dbService.GetDbContext();

        if (userData == null)
        {
            userData = await context.GetAprilUserData(guildId, state.userId);
        }

        var config = await configService.GetConfig(guildId, context);

        if (config == null)
        {
            return new MessageContents("Config not loaded!");
        }

        var embed = new EmbedBuilder().WithTitle("Items");

        foreach (var itemDb in userData.InventoryItems)
        {
            var item = config.items.FirstOrDefault(x => x.Guid == itemDb.ItemGuid);
            if (item == null) continue;

            embed.AddField(item.name, item.description);
        }

        var components = new ComponentBuilder().WithButton("Inspect",
            ModulePrefixes.InventoryInspectButton + StateSerializer.SerializeObject(state),
            ButtonStyle.Secondary, disabled: userData.InventoryItems.Count == 0);

        return new MessageContents(embed, components);
    }

    public async Task<MessageContents> GetInspectMessage(ulong guildId, InventoryContentsState state,
        InventoryItemInspectState value)
    {
        await using var context = dbService.GetDbContext();

        var config = await configService.GetConfig(guildId, context);

        if (config == null)
        {
            return new MessageContents("Config not loaded!");
        }

        var userData = await context.GetAprilUserData(guildId, state.userId);
        var inventoryItem = userData.InventoryItems.FirstOrDefault(x => x.Id == value.inventoryKey);

        if (inventoryItem == null)
        {
            return new MessageContents("Item no longer in inventory.");
        }

        var item = config.items.FirstOrDefault(y => y.Guid == inventoryItem.ItemGuid);

        if (item == null)
        {
            return new MessageContents("Item no longer in config?");
        }

        var embed = new EmbedBuilder()
            .WithTitle(item.name)
            .WithDescription(item.description)
            .WithImageUrl(item.imageUrl);

        var components = new ComponentBuilder();

        if (item.hasUseActions)
        {
            var action = new InventoryItemInspectActionState()
            {
                type = InventoryItemInspectActionState.ActionType.Use,
                inventoryContentsState = state,
                inventoryItemInspectState = value
            };
            components.WithButton("Use",
                ModulePrefixes.InventoryInspectExecuteActionButton + StateSerializer.SerializeObject(action));
        }

        if (item.hasEquipActions)
        {
            var action = new InventoryItemInspectActionState()
            {
                type = InventoryItemInspectActionState.ActionType.ToggleEquip,
                inventoryContentsState = state,
                inventoryItemInspectState = value
            };
            components.WithButton(inventoryItem.IsEquipped ? "De-Equip" : "Equip",
                ModulePrefixes.InventoryInspectExecuteActionButton + StateSerializer.SerializeObject(action));
        }

        components.AddRow(new ActionRowBuilder()
            .WithButton("Back",
                ModulePrefixes.InventoryInspectGoToPage + StateSerializer.SerializeObject(state),
                ButtonStyle.Secondary));

        return new MessageContents(embed, components);
    }
}
