namespace April.Config;

public class OrCondition : UniqueObject, IPoolConditionData
{
    public List<PoolConditionContainer> conditions = [];
}