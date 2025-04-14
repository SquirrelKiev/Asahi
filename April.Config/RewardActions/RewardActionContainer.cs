using Newtonsoft.Json.Converters;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace April.Config;

[JsonConverter(typeof(RewardActionContainerConverter))]
public class RewardActionContainer
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ActionType
    {
        SetResponse,
        SendMessage,
        GrantRole,
        RemoveRole,
        AddItem,
        RemoveItem,
        ChangeNickname,
        ExecuteAfterDelay
    }

    public required ActionType actionType;
    public required IRewardActionData data;

    public RewardActionContainer() { }

    [SetsRequiredMembers]
    public RewardActionContainer(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.SetResponse:
                this.actionType = ActionType.SetResponse;
                data = new SetResponseData();
                break;
            case ActionType.SendMessage:
                this.actionType = ActionType.SendMessage;
                data = new SendMessageActionData();
                break;

            case ActionType.GrantRole:
                this.actionType = ActionType.GrantRole;
                data = new RoleActionData();
                break;

            case ActionType.RemoveRole:
                this.actionType = ActionType.RemoveRole;
                data = new RoleActionData();
                break;

            case ActionType.AddItem:
                this.actionType = ActionType.AddItem;
                data = new ItemActionData();
                break;

            case ActionType.RemoveItem:
                this.actionType = ActionType.RemoveItem;
                data = new ItemActionData();
                break;

            case ActionType.ChangeNickname:
                this.actionType = ActionType.ChangeNickname;
                data = new ChangeNicknameActionData();
                break;

            case ActionType.ExecuteAfterDelay:
                this.actionType = ActionType.ExecuteAfterDelay;
                data = new ExecuteAfterDelayActionData();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null);
        }
    }
}