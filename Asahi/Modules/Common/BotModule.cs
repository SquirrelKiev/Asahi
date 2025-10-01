using Discord.Interactions;
using Discord.WebSocket;
using JetBrains.Annotations;

namespace Asahi.Modules;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class BotModule : InteractionModuleBase<IInteractionContext>
{
    protected virtual Task<IUserMessage> FollowupAsync(MessageContents contents, bool ephemeral = false)
    {
        return FollowupAsync(text: contents.body, embeds: contents.embeds, components: contents.components, ephemeral: ephemeral);
    }

    protected virtual Task RespondAsync(MessageContents contents, bool ephemeral = false)
    {
        return RespondAsync(text: contents.body, embeds: contents.embeds, components: contents.components, ephemeral: ephemeral);
    }

    protected virtual Task<IUserMessage> ModifyOriginalResponseAsync(MessageContents contents, RequestOptions? options = null)
    {
        return Context.Interaction.ModifyOriginalResponseAsync(contents, options);
    }

    protected virtual IChannel GetParentChannel()
    {
        IChannel channel = Context.Channel;

        if (Context.Channel is SocketThreadChannel thread)
        {
            channel = thread.ParentChannel;
        }

        return channel;
    }
    
    /// <summary>
    /// Parses a given message link and returns the message it resolves to if the user specified has permission to view it.
    /// </summary>
    /// <param name="messageLink">A link to the message.</param>
    /// <param name="userId">The user ID to check permissions for.</param>
    /// <returns>The resolved message.</returns>
    protected async Task<Result<IMessage>> ResolveMessageLinkAsync(string messageLink, ulong userId)
    {
        var messageReferenceNullable = CompiledRegex.ParseMessageLink(messageLink);

        if (messageReferenceNullable == null)
            throw new ArgumentException("Not a valid message link!", nameof(messageLink));
        
        var messageReference = messageReferenceNullable.Value;
            
        var guild = await Context.Client.GetGuildAsync(messageReference.GuildId);

        var executorGuildUser = await guild.GetUserAsync(userId);
        if (executorGuildUser == null)
        {
            return Result<IMessage>.Fail("User cannot view this message.");
        }

        var channel = await guild.GetTextChannelAsync(messageReference.ChannelId);
        var perms = executorGuildUser.GetPermissions(channel);
        if (!perms.ViewChannel || !perms.ReadMessageHistory)
        {
            return Result<IMessage>.Fail("User cannot view this message.");
        }

        if (channel is SocketThreadChannel { Type: ThreadType.PrivateThread } threadChannel)
        {
            _ = await threadChannel.GetUsersAsync();
            if (threadChannel.GetUser(userId) == null)
            {
                return Result<IMessage>.Fail("User cannot view this message.");
            }
        }
        var message = await channel.GetMessageAsync(messageReference.MessageId);
            
        return Result<IMessage>.Ok(message);
    }

    public virtual bool IsDm()
    {
        return Context.Guild == null;
    }
}
