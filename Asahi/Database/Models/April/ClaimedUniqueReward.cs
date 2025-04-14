namespace Asahi.Database.Models.April;

public class ClaimedUniqueReward
{
    public required Guid RewardGuid { get; set; }
    public required ulong GuildId { get; set; }
}
