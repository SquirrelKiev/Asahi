namespace April.Config;

public class RewardBox : UniqueObject
{
    public string name = "a super awesome box";
    public List<BoxPool> pools = [];
}

public class BoxPool : UniqueObject
{
    public required Guid poolId;
    public int dropChance = 1000;
    public List<PoolConditionContainer> conditions = [];
}