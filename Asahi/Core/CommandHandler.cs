using System.Reflection;
using Asahi.Database;
using Asahi.Database.Models;
using Asahi.Modules;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Asahi;

public class CommandHandler(
    InteractionService interactionService,
    // CommandService commandService,
    DiscordSocketClient client,
    IServiceProvider services,
    // IDbContextFactory<BotDbContext> dbService,
    InteractiveService interactiveService,
    // BotEmoteService emotes,
    ILogger<CommandHandler> logger
)
{
    private bool runOnce = false;

    public async Task OnReady(params Assembly[] assemblies)
    {
        if (runOnce)
            return;

        try
        {
            await InitializeInteractionService(assemblies);
            // await InitializeCommandService(assemblies);

            runOnce = true;
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Failed to register commands/interactions!");
        }
    }

    #region Prefix Command Handling - Temp disabled due to not being used

    // private async Task MessageReceived(SocketMessage msg)
    // {
    //     if (msg.Author.IsBot)
    //         return;
    //
    //     if (msg is not SocketUserMessage userMessage)
    //         return;
    //
    //     try
    //     {
    //         await RunCommand(userMessage);
    //     }
    //     catch (Exception ex)
    //     {
    //         logger.LogError(ex, "Command failed: ");
    //     }
    // }
    //
    // private async Task RunCommand(SocketUserMessage userMessage)
    // {
    //     var prefix = await GetPrefix(userMessage.Channel);
    //
    //     var argPos = 0;
    //     if (!userMessage.HasStringPrefix(prefix, ref argPos))
    //     {
    //         return;
    //     }
    //
    //     {
    //         await using var dbContext = await dbService.CreateDbContextAsync();
    //
    //         if (userMessage.Channel is SocketTextChannel textChannel)
    //         {
    //             var potentialTitle = userMessage.Content[argPos..];
    //
    //             var command = await dbContext.GetCustomCommand(
    //                 textChannel.Guild.Id,
    //                 potentialTitle
    //             );
    //
    //             if (command != null)
    //             {
    //                 await textChannel.SendMessageAsync(command.Contents);
    //                 return;
    //             }
    //         }
    //     }
    //
    //     var context = new SocketCommandContext(client, userMessage);
    //
    //     await commandService.ExecuteAsync(context, argPos, services);
    // }
    //
    // private async Task<string> GetPrefix(ISocketMessageChannel? channel)
    // {
    //     var prefix = GuildConfig.DefaultPrefix;
    //
    //     if (channel is SocketTextChannel textChannel)
    //     {
    //         await using var context = await dbService.CreateDbContextAsync();
    //         var config = await context.GetGuildConfig(textChannel.Guild.Id);
    //
    //         prefix = config.Prefix;
    //     }
    //
    //     return prefix;
    // }
    //
    // private async Task CommandExecuted(
    //     Optional<CommandInfo> cmdInfoOpt,
    //     ICommandContext ctx,
    //     Discord.Commands.IResult res
    // )
    // {
    //     var cmdInfo = cmdInfoOpt.IsSpecified ? cmdInfoOpt.Value : null;
    //
    //     if (res.IsSuccess)
    //     {
    //         //logger.LogTrace("Command {ModuleName}.{MethodName} successfully executed. Message contents: {contents}",
    //         //    cmdInfo?.Module.Name, cmdInfo?.Name, ctx.Message.CleanContent);
    //     }
    //     else
    //     {
    //         if (res.Error == CommandError.UnknownCommand)
    //             return;
    //
    //         if (res is Discord.Commands.ExecuteResult executeResult)
    //         {
    //             //logger.LogError(executeResult.Exception, "Command {ModuleName}.{MethodName} failed. {Error}, {ErrorReason}. Message contents: {contents}",
    //             //    cmdInfo?.Module?.Name, cmdInfo?.Name, executeResult.Error, executeResult.ErrorReason, ctx.Message.CleanContent);
    //         }
    //         else
    //         {
    //             //logger.LogError("Command {ModuleName}.{MethodName} failed. {Error}, {ErrorReason}. Message contents: {contents}",
    //             //    cmdInfo?.Module?.Name, cmdInfo?.Name, res.Error, res.ErrorReason, ctx.Message.CleanContent);
    //         }
    //
    //         try
    //         {
    //             if (res is Discord.Commands.PreconditionResult precondResult)
    //             {
    //                 var messageBody =
    //                     $"Condition to use the command not met. {precondResult.ErrorReason}";
    //                 await ctx.Message.ReplyAsync(messageBody);
    //             }
    //             else
    //             {
    //                 await ctx.Message.AddReactionAsync(emotes.Error);
    //             }
    //         }
    //         catch (Exception e)
    //         {
    //             logger.LogWarning(e, "Failed to add the error reaction!");
    //         }
    //     }
    // }

    #endregion

    #region Interaction Handling

    private async Task InteractionExecuted(
        ICommandInfo cmdInfo,
        IInteractionContext ctx,
        Discord.Interactions.IResult res
    )
    {
        if (res.IsSuccess)
            return;

        var messageBody = $"{res.Error}, {res.ErrorReason}";

        if (res is Discord.Interactions.PreconditionResult precondResult)
        {
            messageBody = $"Condition to use this command not met. {precondResult.ErrorReason}";
        }

        if (ctx.Interaction.HasResponded)
        {
            var ogRes = await ctx.Interaction.GetOriginalResponseAsync();

            if (ogRes != null && (ogRes.Flags & MessageFlags.ComponentsV2) != 0)
            {
                await ctx.Interaction.FollowupAsync(messageBody, ephemeral: true);
            }
            else
            {
                await ctx.Interaction.ModifyOriginalResponseAsync(
                    new MessageContents(messageBody, embed: null, null)
                );
            }
        }
        else
        {
            await ctx.Interaction.RespondAsync(messageBody, ephemeral: true);
        }
    }

    private async Task InteractionCreated(SocketInteraction arg)
    {
        // note to future me - IsManaged message override is forbidden, breaks other stuff
        var ctx = new SocketInteractionContext(client, arg);

        var handledByInteractiveService = interactiveService.IsManaged(ctx.Interaction) ||
                                          interactiveService.TriggersAnyFilter(ctx.Interaction);

        var componentIsNotRedButton = ctx.Interaction is not SocketMessageComponent component ||
                                   component.Data.CustomId != ModulePrefixes.RedButton;
            
        if (handledByInteractiveService && componentIsNotRedButton)
        {
            return;
        }

        await interactionService.ExecuteCommandAsync(ctx, services);
    }

    #endregion

    private async Task InitializeInteractionService(params Assembly[] assemblies)
    {
        interactionService.AddTypeConverter<Color>(new ColorTypeConverter());
        interactionService.AddTypeConverter<IEmote>(new EmoteTypeConverter());

        foreach (var assembly in assemblies)
        {
            var modules = await interactionService.AddModulesAsync(assembly, services);

            foreach (var moduleInfo in modules)
            {
                logger.LogTrace("Registered Interaction Module: {moduleName}", moduleInfo.Name);
            }
        }

        await interactionService.RegisterCommandsGloballyAsync();

        client.InteractionCreated += InteractionCreated;
        interactionService.InteractionExecuted += InteractionExecuted;
    }

    // private async Task InitializeCommandService(params Assembly[] assemblies)
    // {
    //     foreach (var assembly in assemblies)
    //     {
    //         var modules = await commandService.AddModulesAsync(assembly, services);
    //
    //         foreach (var moduleInfo in modules)
    //         {
    //             logger.LogTrace("Registered Prefix Module: {moduleName}", moduleInfo.Name);
    //         }
    //     }
    //
    //     client.MessageReceived += MessageReceived;
    //     commandService.CommandExecuted += CommandExecuted;
    // }
}
