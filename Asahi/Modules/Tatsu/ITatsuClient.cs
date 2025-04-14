using Refit;

namespace Asahi.Modules.Tatsu;

public interface ITatsuClient
{
    // TODO: switch to use ApiResponse and come up with a better client that handles ratelimits etc
    [Patch("/guilds/{guildId}/members/{userId}/score")]
    public Task<ApiResponse<GuildMemberScore>> ModifyGuildMemberScore([Header("Authorization")] string apiKey, ulong guildId, ulong userId, [Body] ModifyGuildMemberScoreBody amount);
    
    [Get("/guilds/{guildId}/rankings/members/{userId}/all")]
    public Task<ApiResponse<GuildMemberScore>> GetUserRank([Header("Authorization")] string apiKey, ulong guildId, ulong userId);

    public enum TatsuActionType
    {
        Add = 0,
        Remove = 1
    }

    public class ModifyGuildMemberScoreBody
    {
        public required TatsuActionType action;
        public required int amount;
    }

    public class GuildMemberScore
    {
        public ulong score;
    }
}
