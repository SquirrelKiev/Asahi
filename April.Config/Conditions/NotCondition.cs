namespace April.Config;

public class NotCondition : UniqueObject, IPoolConditionData
{
    public List<PoolConditionContainer> conditions = [];
}