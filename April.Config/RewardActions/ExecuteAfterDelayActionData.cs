namespace April.Config;

public class ExecuteAfterDelayActionData : UniqueObject, IRewardActionData
{
    public int delaySecondsMin = 10;
    public int delaySecondsMax = 20;

    public List<RewardActionContainer> actions = [];
}