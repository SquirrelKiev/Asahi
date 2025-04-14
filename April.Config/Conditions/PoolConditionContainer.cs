using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace April.Config;

[JsonConverter(typeof(PoolConditionContainerConverter))]
public class PoolConditionContainer
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ConditionType
    {
        HasItemCondition,
        NotCondition,
        OrCondition,
        HasRoleCondition
    }

    public required ConditionType conditionType;
    public required IPoolConditionData data;

    public PoolConditionContainer() { }

    [SetsRequiredMembers]
    public PoolConditionContainer(ConditionType conditionType)
    {
        switch (conditionType)
        {
            case ConditionType.HasItemCondition:
                this.conditionType = conditionType;
                data = new HasItemCondition();
                break;
            case ConditionType.NotCondition:
                this.conditionType = conditionType;
                data = new NotCondition();
                break;
            case ConditionType.OrCondition:
                this.conditionType = conditionType;
                data = new OrCondition();
                break;
            case ConditionType.HasRoleCondition:
                this.conditionType = conditionType;
                data = new HasRoleCondition();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(conditionType), conditionType, null);
        }
    }
}