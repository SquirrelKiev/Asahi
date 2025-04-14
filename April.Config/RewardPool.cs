namespace April.Config;

public class RewardPool : UniqueObject
{
    // don't think we'll be using this name for anything other than a user readable way of identifying the pool
    public string name = "a super awesome pool";
    public List<Reward> rewards = [];
}