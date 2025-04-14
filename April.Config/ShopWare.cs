namespace April.Config;

public class ShopWare : UniqueObject
{
    public string name = "a super cool thing you should totally buy";
    public string description = "No description.";
    public int cost = 100;
    public List<RewardActionContainer> actionsUponRedeem = [];
    public List<PoolConditionContainer> conditions = [];
}