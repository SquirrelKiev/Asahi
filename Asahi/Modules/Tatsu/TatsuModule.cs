using Discord.Interactions;

namespace Asahi.Modules.Tatsu;

[Group("tatsu", "Commands for managing tatsu. All involving API keys are ephemeral.")]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
public class TatsuModule : BotModule
{
    [Group("score", "Tatsu score stuff.")]
    public class TatsuScoreModule(ITatsuClient tatsuClient) : BotModule
    {
        [SlashCommand("user", "Adds/removes the specified amount of score to the specified user.")]
        public async Task AddScoreSingleUserSlash(
            [Summary(description: "API key from t!apikey create.")]
            string apiKey, 
            [Summary(description: "The user to change the score of.")]
            IGuildUser user, 
            [Summary(name: "operator", "The operator.")] ITatsuClient.TatsuActionType op, 
            [MinValue(1), MaxValue(100000), Summary(description: "The score to modify the existing score with.")] int score)
        {
            await DeferAsync(true);

            var profile = await tatsuClient.ModifyGuildMemberScore(apiKey, Context.Guild.Id, user.Id, new ITatsuClient.ModifyGuildMemberScoreBody
            {
                action = op,
                amount = score
            });

            await FollowupAsync($"User's score is now {profile.score}.");
        }
    }
}
