using April.Config;
using Asahi.Database;
using Discord.Interactions;

namespace Asahi.Modules.April;

#if !DEBUG
[DontAutoRegister]
#endif
public class InventoryModule(
    InventoryService inventoryService,
    IDbService dbService,
    AprilConfigService configService,
    AprilUtility aprilUtility) : BotModule
{
    // [SlashCommand("inventory", "Opens your inventory.")]
    public async Task InventorySlash()
    {
        await DeferAsync();

        await FollowupAsync(await inventoryService.GetInventoryContentsMessage(Context.Guild.Id,
            new InventoryService.InventoryContentsState { userId = Context.User.Id }));
    }

    [ComponentInteraction(ModulePrefixes.InventoryInspectButton + "*")]
    public async Task InventoryInspectButton(string customId)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();
        var config = await configService.GetConfig(Context.Guild.Id, context);

        if (config == null)
        {
            await FollowupAsync("Config not loaded!");
            return;
        }

        var state = StateSerializer.DeserializeObject<InventoryService.InventoryContentsState>(customId) ??
                    throw new NullReferenceException("tf? state is null");

        var userData = await context.GetAprilUserData(Context.Guild.Id, state.userId);

        var messageContents = await inventoryService.GetInventoryContentsMessage(Context.Guild.Id, state);
        messageContents.components = new ComponentBuilder()
            .WithSelectMenu(ModulePrefixes.InventoryInspectSelectMenu + StateSerializer.SerializeObject(state),
                userData.InventoryItems.Select(x =>
                    {
                        var item = config.items.FirstOrDefault(y => y.Guid == x.ItemGuid);

                        if (item == null)
                            return null;

                        var serializedState = StateSerializer.SerializeObject(
                            new InventoryService.InventoryItemInspectState
                            {
                                inventoryKey = x.Id,
                            });

                        return new SelectMenuOptionBuilder(item.name, serializedState);
                    }
                ).Where(x => x != null).ToList())
            .WithButton("Back",
                ModulePrefixes.InventoryInspectGoToPage + StateSerializer.SerializeObject(state),
                ButtonStyle.Secondary)
            .Build();

        await ModifyOriginalResponseAsync(messageContents);
    }

    [ComponentInteraction(ModulePrefixes.InventoryInspectGoToPage + "*")]
    public async Task InspectBackButton(string customId)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();
        
        var config = await configService.GetConfig(Context.Guild.Id, context);

        if (config == null)
        {
            await FollowupAsync("Config not loaded!");
            return;
        }

        var state = StateSerializer.DeserializeObject<InventoryService.InventoryContentsState>(customId) ??
                    throw new NullReferenceException("tf? state is null");

        await ModifyOriginalResponseAsync(await inventoryService.GetInventoryContentsMessage(Context.Guild.Id, state));
    }

    [ComponentInteraction(ModulePrefixes.InventoryInspectSelectMenu + "*")]
    public async Task InspectDropdown(string customId, string valueId)
    {
        await DeferAsync();

        var state = StateSerializer.DeserializeObject<InventoryService.InventoryContentsState>(customId) ??
                    throw new NullReferenceException("tf? state is null");
        var value = StateSerializer.DeserializeObject<InventoryService.InventoryItemInspectState>(valueId) ??
                    throw new NullReferenceException("tf? value is null");

        var message = await inventoryService.GetInspectMessage(Context.Guild.Id, state, value);

        await ModifyOriginalResponseAsync(message);
    }

    [ComponentInteraction(ModulePrefixes.InventoryInspectExecuteActionButton + "*")]
    public async Task ExecuteActionButton(string customId)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var config = await configService.GetConfig(Context.Guild.Id, context);

        if (config == null)
        {
            await ModifyOriginalResponseAsync(new MessageContents("Config not loaded!"));
            return;
        }

        var state = StateSerializer.DeserializeObject<InventoryService.InventoryItemInspectActionState>(customId) ??
                    throw new NullReferenceException("tf? state is null");

        var userData = await context.GetAprilUserData(Context.Guild.Id, state.inventoryContentsState.userId);
        var inventoryItem =
            userData.InventoryItems.FirstOrDefault(x => x.Id == state.inventoryItemInspectState.inventoryKey);

        if (inventoryItem == null)
        {
            await ModifyOriginalResponseAsync(new MessageContents("Item no longer in inventory."));
            return;
        }

        var item = config.items.FirstOrDefault(y => y.Guid == inventoryItem.ItemGuid);

        if (item == null)
        {
            await ModifyOriginalResponseAsync(new MessageContents("Item no longer in config?"));
            return;
        }

        List<RewardActionContainer>? actions = null;

        switch (state.type)
        {
            case InventoryService.InventoryItemInspectActionState.ActionType.ToggleEquip:
                actions = inventoryItem.IsEquipped ? item.deEquipActions : item.equipActions;
                inventoryItem.IsEquipped = !inventoryItem.IsEquipped;
                break;
            case InventoryService.InventoryItemInspectActionState.ActionType.Use:
                actions = item.useActions;
                break;
            default:
                throw new NotSupportedException();
        }

        var response = await aprilUtility.ExecuteRewardActions(actions, context, userData,
            await Context.Guild.GetUserAsync(state.inventoryContentsState.userId), Context.Channel);

        await context.SaveChangesAsync();

        var inspectMessage = await inventoryService.GetInspectMessage(Context.Guild.Id,
            state.inventoryContentsState, state.inventoryItemInspectState);

        await FollowupAsync(AprilUtility.GachaMessageToMessageContents(response));

        await ModifyOriginalResponseAsync(inspectMessage);
    }
}
