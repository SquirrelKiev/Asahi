namespace April.Config;

public class SendMessageActionData : UniqueObject, IRewardActionData
{
    /// <remarks>0 == current channel</remarks>
    public ulong channelId = 0;

    /// <remarks>{{user}} should be replaced with the user who rolled this.</remarks>
    public GachaMessage message = new();
}