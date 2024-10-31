namespace Asahi;

// officially the stupidest service to exist.
// 100% could be extension methods on ICacheProvider, but that felt wrong.
/// <remarks>None of the functions check or care if the user is allowed to have an override. Make sure to check with <see cref="BotConfig"/> before using these.</remarks>
public class OverrideTrackerService(BotConfig botConfig)
{
    private readonly HashSet<ulong> OverriddenIds = [];

    public bool SetOverride(ulong id) => OverriddenIds.Add(id);

    public bool ClearOverride(ulong id) => OverriddenIds.Remove(id);

    public bool HasOverride(ulong id) => OverriddenIds.Contains(id);

    public bool TryToggleOverride(ulong userId)
    {
        if (!botConfig.ManagerUserIds.Contains(userId)) return false;

        if (!HasOverride(userId))
        {
            SetOverride(userId);
        }
        else
        {
            ClearOverride(userId);
        }

        return true;

    }
}
