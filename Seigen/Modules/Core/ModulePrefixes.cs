namespace Seigen.Modules;

public static class ModulePrefixes
{
    public const string PERMISSION_GROUP = "'User has Manage Roles'";

    #region Config - Trackables

    public const string CONFIG_TRACKABLES = "c-t";
    public const string CONFIG_TRACKABLES_ADD = $"{CONFIG_TRACKABLES}-a:";
    public const string CONFIG_TRACKABLES_MODIFY = $"{CONFIG_TRACKABLES}-m:";
    public const string CONFIG_TRACKABLES_REMOVE = $"{CONFIG_TRACKABLES}-r:";

    public const string CONFIG_TRACKABLES_CONFIRMATION_BASE = $"{CONFIG_TRACKABLES}-c";
    public const string CONFIG_TRACKABLES_CONFIRMATION_ADD_BUTTON = $"{CONFIG_TRACKABLES_CONFIRMATION_BASE}-a:";
    public const string CONFIG_TRACKABLES_CONFIRMATION_MONITORED_GUILD = $"{CONFIG_TRACKABLES_CONFIRMATION_BASE}-mg:";
    public const string CONFIG_TRACKABLES_CONFIRMATION_MONITORED_ROLE = $"{CONFIG_TRACKABLES_CONFIRMATION_BASE}-mr:";
    public const string CONFIG_TRACKABLES_CONFIRMATION_ASSIGNABLE_GUILD = $"{CONFIG_TRACKABLES_CONFIRMATION_BASE}-ag:";
    public const string CONFIG_TRACKABLES_CONFIRMATION_ASSIGNABLE_ROLE = $"{CONFIG_TRACKABLES_CONFIRMATION_BASE}-ar:";

    #endregion
}