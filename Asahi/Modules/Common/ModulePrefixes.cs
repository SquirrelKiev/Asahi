namespace Asahi.Modules;

// TODO: change this to be in PascalCase
public static class ModulePrefixes
{
    public const string RED_BUTTON = "rb:";
    public const string ABOUT_OVERRIDE_TOGGLE = "ovt:";

    public const string SPOILER_PREFIX = "ms";
    public const string SPOILER_MODAL_PREFIX = $"{SPOILER_PREFIX}-m";
    public const string SPOILER_MODAL  = $"{SPOILER_MODAL_PREFIX}:";
    public const string SPOILER_MODAL_CONTEXT_INPUT = $"{SPOILER_MODAL_PREFIX}-cti:";

    public const string BIRTHDAY_TEXT_MODAL = "bday-m:";
    
    // TODO: Swap these out for fergun
    private const string INVENTORY_BASE = "i";
    private const string INVENTORY_INSPECT_BASE = $"{INVENTORY_BASE}-i";
    public const string InventoryInspectButton = $"{INVENTORY_INSPECT_BASE}-b:";
    public const string InventoryInspectGoToPage = $"{INVENTORY_INSPECT_BASE}-bb:";
    public const string InventoryInspectSelectMenu = $"{INVENTORY_INSPECT_BASE}-sm:";
    public const string InventoryInspectExecuteActionButton = $"{INVENTORY_INSPECT_BASE}-eab:";
}
