﻿using Asahi.Database;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.ModSpoilers;

[Group("spoiler", "Commands relating to mod spoiler tagging.")]
[DefaultMemberPermissions(GuildPermission.ManageMessages)]
[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
public class ModSpoilerModule(ModSpoilerService mss, IDbContextFactory<BotDbContext> dbService) : BotModule
{
    public class SetContextModal : IModal
    {
        public string Title => "Spoiler Message";

        [ModalTextInput(ModulePrefixes.SPOILER_MODAL_CONTEXT_INPUT, maxLength: 500)]
        [RequiredInput(false)]
        public string? Context { get; set; } = null;
    }

    public struct MessageReference
    {
        public ulong guildId, channelId, messageId;
    }

    [MessageCommand("Spoiler Message")]
    public async Task SpoilerMessageSlash(IMessage message)
    {
        if (message.Channel is not ITextChannel channel)
        {
            await FollowupAsync("Not in a Guild or text channel.", ephemeral: true);
            return;
        }

        var messageRef = new MessageReference()
        {
            guildId = channel.GuildId,
            channelId = channel.Id,
            messageId = message.Id
        };

        await RespondWithModalAsync<SetContextModal>(StateSerializer.SerializeObject(messageRef,
            ModulePrefixes.SPOILER_MODAL));
    }

    [SlashCommand("spoiler-emote", "Sets the reaction emote that mods use to mark a message as spoiler.")]
    public async Task SetSpoilerEmoteSlash([Summary(description: "The emote mods will react with.")] IEmote emote)
    {
        await DeferAsync();

        await using var context = await dbService.CreateDbContextAsync();

        var config = await context.GetGuildConfig(Context.Guild.Id);

        config.SpoilerReactionEmote = emote.ToString() ?? "";

        await context.SaveChangesAsync();

        await FollowupAsync($"Set spoiler emote to {emote}", allowedMentions: AllowedMentions.None);
    }

    [SlashCommand("delete-og", "Whether the bot should delete the message being tagged or not.")]
    public async Task SetDeleteOgSlash([Summary(description: "Whether the bot should delete the message being tagged or not.")] bool delete)
    {
        await DeferAsync();

        await using var context = await dbService.CreateDbContextAsync();

        var config = await context.GetGuildConfig(Context.Guild.Id);

        config.SpoilerBotAutoDeleteOriginal = delete;

        await context.SaveChangesAsync();

        await FollowupAsync($"Bot will now {(delete ? "delete" : "not delete")} the message being tagged.", allowedMentions: AllowedMentions.None);
    }

    [SlashCommand("delete-context", "Whether the bot should delete the messages for setting context or not. (Reaction only)")]
    public async Task SetDeleteContextSlash([Summary(description: "Whether the bot should delete the message being tagged or not.")] bool delete)
    {
        await DeferAsync();

        await using var context = await dbService.CreateDbContextAsync();

        var config = await context.GetGuildConfig(Context.Guild.Id);

        config.SpoilerBotAutoDeleteContextSetting = delete;

        await context.SaveChangesAsync();

        await FollowupAsync($"Bot will now {(delete ? "delete" : "not delete")} the context messages.", allowedMentions: AllowedMentions.None);
    }

    [ModalInteraction(ModulePrefixes.SPOILER_MODAL + "*", true)]
    public async Task SpoilerModal(string state, SetContextModal modal)
    {
        await DeferAsync(ephemeral: true);

        var messageRef = StateSerializer.DeserializeObject<MessageReference>(state);

        var guild = await Context.Client.GetGuildAsync(messageRef.guildId);
        if (guild == null)
        {
            await FollowupAsync("Invalid Guild? tf?", ephemeral: true);
            return;
        }
        var channel = await guild.GetTextChannelAsync(messageRef.channelId);
        if (channel == null)
        {
            await FollowupAsync("Invalid channel? tf?", ephemeral: true);
            return;
        }
        var message = await channel.GetMessageAsync(messageRef.messageId);
        if (message == null)
        {
            await FollowupAsync("Couldn't find the message to spoil.", ephemeral: true);
            return;
        }

        await using var context = await dbService.CreateDbContextAsync();
        var guildConfig = await context.GetGuildConfig(Context.Guild.Id);

        var response = await mss.SpoilerMessage(message, guildConfig.SpoilerBotAutoDeleteOriginal, context, modal.Context);

        if(response.wasSuccess)
            await context.SaveChangesAsync();

        var eb = new EmbedBuilder()
            .WithColor(response.wasSuccess ? Color.Green : Color.Red)
            .WithDescription(response.response);
        await FollowupAsync(embed: eb.Build());
    }
}
