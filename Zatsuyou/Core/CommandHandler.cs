using System.Reflection;
using BotBase.Modules.RedButton;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Logging;
using Zatsuyou.Database;
using Zatsuyou.Database.Models;

// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

namespace Zatsuyou
{
    public class CommandHandler(
        InteractionService interactionService,
        CommandService commandService,
        DiscordSocketClient client,
        BotConfigBase botConfig,
        IServiceProvider services,
        DbService dbService,
        InteractiveService interactiveService,
        ILogger<CommandHandler> logger)
    {
        protected readonly InteractionService interactionService = interactionService;
        protected readonly CommandService commandService = commandService;
        protected readonly DiscordSocketClient client = client;
        protected readonly BotConfigBase botConfig = botConfig;
        protected readonly IServiceProvider services = services;

        public async Task OnReady(params Assembly[] assemblies)
        {
            await InitializeInteractionService(assemblies);
            await InitializeCommandService(assemblies);
        }

        #region Prefix Command Handling

        protected async Task MessageReceived(SocketMessage msg)
        {
            if (msg.Author.IsBot)
                return;

            if (msg is not SocketUserMessage userMessage)
                return;

            try
            {
                await RunCommand(userMessage);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Command failed: ");
            }
        }

        protected async Task RunCommand(SocketUserMessage userMessage)
        {
            var prefix = await GetPrefix(userMessage.Channel);

            var argPos = 0;
            if (!userMessage.HasStringPrefix(prefix, ref argPos))
            {
                return;
            }

            {
                await using var dbContext = dbService.GetDbContext();

                if (userMessage.Channel is SocketTextChannel textChannel)
                {
                    var potentialTitle = userMessage.Content[argPos..];

                    var command = await dbContext.GetCustomCommand(textChannel.Guild.Id, potentialTitle);

                    if (command != null)
                    {
                        await textChannel.SendMessageAsync(command.Contents);
                        return;
                    }
                }
            }

            var context = new SocketCommandContext(client, userMessage);

            await commandService.ExecuteAsync(context, argPos, services);
        }

        protected async Task<string> GetPrefix(ISocketMessageChannel? channel)
        {
            var prefix = GuildConfig.DefaultPrefix;

            if (channel is SocketTextChannel textChannel)
            {
                await using var context = dbService.GetDbContext();
                var config = await context.GetGuildConfig(textChannel.Guild.Id);

                prefix = config.Prefix;
            }

            return prefix;
        }

        protected async Task CommandExecuted(Optional<CommandInfo> cmdInfoOpt, ICommandContext ctx, Discord.Commands.IResult res)
        {
            var cmdInfo = cmdInfoOpt.IsSpecified ? cmdInfoOpt.Value : null;

            if (res.IsSuccess)
            {
                logger.LogInformation("Command {ModuleName}.{MethodName} successfully executed. Message contents: {contents}",
                    cmdInfo?.Module.Name, cmdInfo?.Name, ctx.Message.CleanContent);
            }
            else
            {
                if (res.Error == CommandError.UnknownCommand)
                    return;

                if (res is Discord.Commands.ExecuteResult executeResult)
                {
                    logger.LogError(executeResult.Exception, "Command {ModuleName}.{MethodName} failed. {Error}, {ErrorReason}. Message contents: {contents}",
                        cmdInfo?.Module?.Name, cmdInfo?.Name, executeResult.Error, executeResult.ErrorReason, ctx.Message.CleanContent);
                }
                else
                {
                    logger.LogError("Command {ModuleName}.{MethodName} failed. {Error}, {ErrorReason}. Message contents: {contents}",
                        cmdInfo?.Module?.Name, cmdInfo?.Name, res.Error, res.ErrorReason, ctx.Message.CleanContent);
                }

                try
                {
                    if (res is Discord.Commands.PreconditionResult precondResult)
                    {
                        var messageBody = $"Condition to use the command not met. {precondResult.ErrorReason}";
                        await ctx.Message.ReplyAsync(messageBody);
                    }
                    else
                    {
                        IEmote emote;

                        if (Emote.TryParse(botConfig.ErrorEmote, out var result))
                        {
                            emote = result;
                        }
                        else
                        {
                            emote = Emoji.Parse(botConfig.ErrorEmote);
                        }


                        await ctx.Message.AddReactionAsync(emote);
                    }
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Failed to add the error reaction!");
                }
            }
        }

        #endregion

        #region Interaction Handling

        protected Task InteractionExecuted(ICommandInfo cmdInfo, IInteractionContext ctx, Discord.Interactions.IResult res)
        {
            if (res.IsSuccess)
            {
                logger.LogInformation("Interaction {ModuleName}.{MethodName} successfully executed.", 
                    cmdInfo.Module.Name, cmdInfo.MethodName);
            }
            else
            {
                if (res is Discord.Interactions.ExecuteResult executeResult)
                {
                    logger.LogError(executeResult.Exception, "Interaction {ModuleName}.{MethodName} failed. {Error}, {ErrorReason}.",
                        cmdInfo?.Module?.Name, cmdInfo?.MethodName, executeResult.Error, executeResult.ErrorReason);
                }
                else
                {
                    logger.LogError("Interaction {ModuleName}.{MethodName} failed. {Error}, {ErrorReason}.",
                        cmdInfo?.Module?.Name, cmdInfo?.MethodName, res.Error, res.ErrorReason);
                }

                var messageBody = $"{res.Error}, {res.ErrorReason}";

                if (res is Discord.Interactions.PreconditionResult precondResult)
                {
                    messageBody = $"Condition to use this command not met. {precondResult.ErrorReason}";
                }

                if (ctx.Interaction.HasResponded)
                {
                    ctx.Interaction.ModifyOriginalResponseAsync(new MessageContents(messageBody, embed: null, null));
                }
                else
                {
                    ctx.Interaction.RespondAsync(messageBody, ephemeral: true);
                }
            }

            return Task.CompletedTask;
        }


        protected async Task InteractionCreated(SocketInteraction arg)
        {
            var ctx = new SocketInteractionContext(client, arg);

            if (ctx.Interaction is SocketMessageComponent componentInteraction)
            {
                if (interactiveService.Callbacks.ContainsKey(componentInteraction.Message.Id))
                    return;

                var ogRes = componentInteraction.Message;

                var ogAuthor = ogRes.Interaction?.User.Id;

                // horrible
                if (ogAuthor == null)
                {
                    var channel = (ISocketMessageChannel)await client.GetChannelAsync(ogRes.Reference.ChannelId);
                    var message = await channel.GetMessageAsync(ogRes.Reference.MessageId.Value);
                    ogAuthor = message?.Author?.Id;
                }

                if (ogAuthor != null && ogAuthor != ctx.Interaction.User.Id)
                {
                    await componentInteraction.RespondAsync("You did not originally trigger this. Please run the command yourself.", ephemeral: true);

                    return;
                }
            }

            await interactionService.ExecuteCommandAsync(ctx, services);
        }

        #endregion

        protected async Task InitializeInteractionService(params Assembly[] assemblies)
        {
            foreach (var assembly in assemblies.Append(typeof(RedButtonModule).Assembly))
            {
                var modules = await interactionService.AddModulesAsync(assembly, services);

                foreach (var moduleInfo in modules)
                {
                    logger.LogDebug("Registered Interaction Module: {moduleName}", moduleInfo.Name);
                }
            }

            await interactionService.RegisterCommandsGloballyAsync();

            client.InteractionCreated += InteractionCreated;
            interactionService.InteractionExecuted += InteractionExecuted;
        }

        protected async Task InitializeCommandService(params Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                var modules = await commandService.AddModulesAsync(assembly, services);

                foreach (var moduleInfo in modules)
                {
                    logger.LogDebug("Registered Prefix Module: {moduleName}", moduleInfo.Name);
                }
            }

            client.MessageReceived += MessageReceived;
            commandService.CommandExecuted += CommandExecuted;
        }
    }
}
