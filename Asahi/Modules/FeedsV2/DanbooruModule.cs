using Discord.Interactions;
using Microsoft.Extensions.Logging;
using ProtoBuf;

namespace Asahi.Modules.FeedsV2;

[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
public class DanbooruModule(
    DanbooruUtility danbooruUtility,
    BotEmoteService emotes,
    IDanbooruApi danbooruApi,
    ILogger<DanbooruModule> logger) : BotModule
{
    public class DeletionSetNoteModal : IModal
    {
        public string Title => "Deletion note";

        [ModalTextInput(ModulePrefixes.Danbooru.DeletionNotesModalNoteInput, maxLength: 80)]
        [RequiredInput]
        public string Note { get; set; } = "Deleted";
    }

    [ComponentInteraction(ModulePrefixes.Danbooru.MoreInfoButton + "*")]
    public async Task MoreInfoButton(string data)
    {
        var responseTask =
            RespondAsync(components: new ComponentBuilderV2().WithTextDisplay($"{emotes.Loading} Loading...").Build(),
                ephemeral: true, allowedMentions: AllowedMentions.None);

        var extraInfo = StateSerializer.DeserializeObject<DanbooruExtraInfoData>(data);

        var post = await danbooruApi.GetPost(extraInfo.PostId);

        if (!post.IsSuccessful)
        {
            logger.LogError(post.Error, "Failed to get Danbooru post #{postId} for more info message.",
                extraInfo.PostId);

            await responseTask;

            await ModifyOriginalResponseAsync(x =>
                x.Components = new ComponentBuilderV2().WithTextDisplay($"{emotes.Loading} Loading...").Build());
            return;
        }

        var component = await danbooruUtility.GetComponent(post.Content, new Color(extraInfo.EmbedColor),
            extraInfo.FeedTitle, true, extraInfo.MessageDeletedBy != 0ul, extraInfo.MessageDeletedBy);

        await responseTask;
        await ModifyOriginalResponseAsync(x =>
        {
            x.Components = component;
            x.AllowedMentions = AllowedMentions.None;
        });
    }

    [ComponentInteraction(ModulePrefixes.Danbooru.DeleteButton + "*")]
    public async Task DeleteButton(string data)
    {
        var extraInfo = StateSerializer.DeserializeObject<DanbooruExtraInfoData>(data);

        extraInfo.MessageDeletedBy = Context.User.Id;

        var component = GetDeletionInfoComponent(extraInfo);

        await ((IComponentInteraction)Context.Interaction).UpdateAsync(x => { x.Components = component; });
    }

    [ComponentInteraction(ModulePrefixes.Danbooru.DeletionRestoreButton + "*")]
    public async Task RestoreButton(string data)
    {
        var loading =
            ((IComponentInteraction)Context.Interaction).UpdateAsync(x =>
                x.Components = new ComponentBuilderV2().WithTextDisplay($"{emotes.Loading} Loading...").Build());

        var extraInfo = StateSerializer.DeserializeObject<DanbooruExtraInfoData>(data);
        try
        {
            var post = await danbooruApi.GetPost(extraInfo.PostId);

            if (!post.IsSuccessful)
            {
                logger.LogError(post.Error, "Failed to get Danbooru post #{postId} to restore.", extraInfo.PostId);

                await loading;
                await ModifyOriginalResponseAsync(x => x.Components = GetDeletionInfoComponent(extraInfo));
                await RespondAsync("Failed to get post from Danbooru.", ephemeral: true);
                return;
            }

            var component =
                await danbooruUtility.GetComponent(post.Content, new Color(extraInfo.EmbedColor), extraInfo.FeedTitle);

            await loading;
            await ModifyOriginalResponseAsync(x => { x.Components = component; });
        }
        catch (Exception)
        {
            await loading;
            await ModifyOriginalResponseAsync(x => x.Components = GetDeletionInfoComponent(extraInfo));
            throw;
        }
    }

    [ComponentInteraction(ModulePrefixes.Danbooru.DeletionNotesButton + "*")]
    public async Task NotesButton(string data)
    {
        var extraInfo = StateSerializer.DeserializeObject<DanbooruExtraInfoData>(data);

        await RespondWithModalAsync<DeletionSetNoteModal>(
            StateSerializer.SerializeObject(extraInfo, ModulePrefixes.Danbooru.DeletionNotesModal));
    }

    [ModalInteraction(ModulePrefixes.Danbooru.DeletionNotesModal + "*")]
    public async Task DeletionNotesModal(string data, DeletionSetNoteModal modal)
    {
        var extraInfo = StateSerializer.DeserializeObject<DanbooruExtraInfoData>(data);

        var component = GetDeletionInfoComponent(extraInfo, modal.Note);

        await ((IModalInteraction)Context.Interaction).UpdateAsync(x => { x.Components = component; });
    }

    public MessageComponent GetDeletionInfoComponent(DanbooruExtraInfoData extraInfo,
        string deletionNote = "Deleted")
    {
        var components = new ComponentBuilderV2();

        // components.WithTextDisplay($"*Deleted by <@{extraInfo.MessageDeletedBy}>*");

        var deleteInfoButton = new ButtonBuilder()
            .WithCustomId(StateSerializer.SerializeObject(extraInfo, ModulePrefixes.Danbooru.DeleteButton))
            .WithEmote(emotes.DanbooruDeletedPostNote)
            .WithLabel(deletionNote)
            .WithStyle(ButtonStyle.Secondary)
            .WithDisabled(true);

        var moreInfoButton =
            new ButtonBuilder()
                .WithCustomId(StateSerializer.SerializeObject(extraInfo,
                    ModulePrefixes.Danbooru.MoreInfoButton))
                .WithEmote(emotes.DanbooruMoreInfo)
                .WithStyle(ButtonStyle.Secondary);

        var notesButton = new ButtonBuilder()
            .WithCustomId(StateSerializer.SerializeObject(extraInfo, ModulePrefixes.Danbooru.DeletionNotesButton))
            .WithEmote(emotes.DanbooruDeletedPostAddNote)
            .WithStyle(ButtonStyle.Secondary);

        var undoButton = new ButtonBuilder()
            .WithCustomId(StateSerializer.SerializeObject(extraInfo, ModulePrefixes.Danbooru.DeletionRestoreButton))
            .WithEmote(emotes.DanbooruRestoreDeletedPost)
            .WithStyle(ButtonStyle.Secondary);

        components.WithActionRow([deleteInfoButton, moreInfoButton, notesButton, undoButton]);

        return components.Build();
    }

    [ProtoContract]
    public record struct DanbooruExtraInfoData(
        uint PostId,
        string FeedTitle,
        uint EmbedColor,
        ulong MessageDeletedBy = 0ul)
    {
        [ProtoMember(1)] public uint PostId = PostId;
        [ProtoMember(2)] public string FeedTitle = FeedTitle;
        [ProtoMember(3)] public uint EmbedColor = EmbedColor;
        [ProtoMember(4)] public ulong MessageDeletedBy = MessageDeletedBy;
    }
}
