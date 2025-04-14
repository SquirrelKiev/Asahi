namespace April.Config;

public class Reward : UniqueObject
{
    public string name = "a super rewarding reward";
    public List<RewardActionContainer> actions = [];
    public bool isUnique = false;
}