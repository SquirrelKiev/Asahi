using Discord.Interactions;

namespace Asahi.Modules.Tatsu;

[Group("tatsu", "Commands for managing tatsu. All involving API keys are ephemeral.")]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class TatsuModule : BotModule
{
    [Group("score", "Tatsu score stuff.")]
    public class TatsuScoreModule(ITatsuClient tatsuClient) : BotModule
    {
        [SlashCommand("user", "Adds/removes the specified amount of score to the specified user.")]
        public async Task AddScoreSingleUserSlash([Summary(description: "API key from t!apikey create.")]
            string apiKey, IGuildUser user, [Summary(name: "operator")] ITatsuClient.TatsuActionType op, 
            [MinValue(1), MaxValue(100000)] int score)
        {
            await DeferAsync(true);

            var profile = await tatsuClient.ModifyGuildMemberScore(apiKey, Context.Guild.Id, user.Id, new ITatsuClient.ModifyGuildMemberScoreBody
            {
                action = op,
                amount = score
            });

            await FollowupAsync($"done'd, score is now {profile.score}");
        }
    }
}