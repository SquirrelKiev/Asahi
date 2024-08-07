using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using Asahi.Database;
using Asahi.Database.Models;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Asahi.Modules.BotManagement;

[Group("bot", "Commands relating to configuring the bot.")]
public class BotManagementModule(DbService dbService, CustomStatusService css, BotConfig config,
    InteractiveService interactive, DiscordSocketClient client, HttpClient http, IServiceProvider services, ILogger<BotManagementModule> logger) : BotModule
{
    [TrustedMember(TrustedId.TrustedUserPerms.StatusPerms)]
    [SlashCommand("toggle-activity", "Toggles the bot activity.")]
    public async Task ToggleBotActivitySlash([Summary(description: "Whether the bot should have a status or not.")] bool isActive)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        botWideConfig.ShouldHaveActivity = isActive;

        await context.SaveChangesAsync();

        await css.UpdateStatus();

        await FollowupAsync("Toggled.");
    }

    [TrustedMember(TrustedId.TrustedUserPerms.StatusPerms)]
    [SlashCommand("activity", "Sets the bot's current activity.")]
    public async Task SetBotStatusSlash(
        [Summary(description: "The activity type the bot should have.")]
        ActivityType activityType,
        [MaxLength(128)]
        [Summary(description: "The activity text.")]
        string activity,
        [Summary(description: $"Streaming URL. This will only need to be set if the activity type is {nameof(ActivityType.Streaming)}.")]
        string streamingUrl = "")
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        botWideConfig.ShouldHaveActivity = true;
        botWideConfig.ActivityType = activityType;
        botWideConfig.BotActivity = activity;
        if (!string.IsNullOrWhiteSpace(streamingUrl))
            botWideConfig.ActivityStreamingUrl = streamingUrl;

        await context.SaveChangesAsync();

        await FollowupAsync($"{config.LoadingEmote} Setting status on bot... (May take a minute depending on current rate-limits)");

        await css.UpdateStatus();

        await ModifyOriginalResponseAsync(new MessageContents("Successfully set activity."));
    }

    [TrustedMember(TrustedId.TrustedUserPerms.StatusPerms)]
    [SlashCommand("status", "Sets the bot's status.")]
    public async Task SetBotStatusSlash([Summary(description: "The status.")] UserStatus status)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        botWideConfig.UserStatus = status;

        await context.SaveChangesAsync();

        await css.UpdateStatus();

        await FollowupAsync($"Successfully set status to {status}.");
    }

    [TrustedMember(TrustedId.TrustedUserPerms.TrustedUserEditPerms)]
    [SlashCommand("add-trusted-id", "Adds a user to the trusted user list. This is a dangerous permission to grant.")]
    public async Task AddTrustedIdSlash([Summary(description: "The user ID of the user.")] string idStr,
        [MaxLength(TrustedId.CommentMaxLength), Summary(description: "A note to put beside the user.")] string comment,
        [Summary(description: "Should the user have permission to use Wolfram?")]
        bool wolframPerms,
        [Summary(description: "Should the user have permission to add or remove other trusted users?")]
        bool trustedUserPerms,
        [Summary(description: "Should the user have permission to change the bot's status/profile?")]
        bool statusPerms,
        [Summary(description: "Should the user have permission to view the guilds the bot is in?")]
        bool guildManagementPerms,
        [Summary(description: "Should the user have permission to execute C# code?")]
        bool codeExecutionPerms)
    {
        await ConfigUtilities.CommonConfig(Context, dbService, async (context, eb) =>
        {
            if (!ulong.TryParse(idStr, out var id))
            {
                return new ConfigChangeResult(false, "Not valid.");
            }

            var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

            var existingEntry = botWideConfig.TrustedIds.FirstOrDefault(x => x.Id == id);

            var runningUserPermissionFlags = config.ManagerUserIds.Contains(Context.User.Id) ? (TrustedId.TrustedUserPerms)int.MaxValue :
                botWideConfig.TrustedIds.First(x => x.Id == Context.User.Id).PermissionFlags;

            var permissionFlags = TrustedId.TrustedUserPerms.None;
            var failedPermissionFlags = TrustedId.TrustedUserPerms.None;

            UpdatePermissionFlags(wolframPerms, TrustedId.TrustedUserPerms.WolframPerms);
            UpdatePermissionFlags(trustedUserPerms, TrustedId.TrustedUserPerms.TrustedUserEditPerms);
            UpdatePermissionFlags(statusPerms, TrustedId.TrustedUserPerms.StatusPerms);
            UpdatePermissionFlags(guildManagementPerms, TrustedId.TrustedUserPerms.BotGuildManagementPerms);
            UpdatePermissionFlags(codeExecutionPerms, TrustedId.TrustedUserPerms.CodeExecutionPerms);

            // why does it break if I just add to the botWideConfig.TrustedIds list?? but only on the 2nd time??? wtf????
            // weird ass concurrency error, but it shouldn't be a concurrency issue as nothing will be getting modified
            // and the contents is there cuz if i json serialize it and log it, I get the correct results?
            // why is it cursed? why? im tearing my hair out here, this better not happen for anything else I swear
            if (existingEntry == null)
            {
                context.Add(new TrustedId()
                {
                    Id = id,
                    Comment = comment,
                    PermissionFlags = permissionFlags,
                    BotWideConfig = botWideConfig,
                });
            }
            else
            {
                existingEntry.Comment = comment;
                existingEntry.PermissionFlags = permissionFlags;
            }

            return new ConfigChangeResult(true,
                $"Updated <@{id}>'s permissions. they now have `{permissionFlags.Humanize()}`. (No permission to give them `{failedPermissionFlags.Humanize()}`).");

            void UpdatePermissionFlags(bool condition, TrustedId.TrustedUserPerms permFlag)
            {
                if (condition)
                {
                    if (runningUserPermissionFlags.HasFlag(permFlag))
                    {
                        permissionFlags |= permFlag;
                    }
                    else
                    {
                        failedPermissionFlags |= permFlag;
                    }
                }
            }
        });
    }

    [TrustedMember(TrustedId.TrustedUserPerms.TrustedUserEditPerms)]
    [SlashCommand("rm-trusted-id", "Removes a user from the trusted user list.")]
    public async Task RemoveTrustedIdSlash([Summary(description: "The user ID of the user.")] string idStr)
    {
        await DeferAsync();

        if (!ulong.TryParse(idStr, out var id))
        {
            await FollowupAsync("Not valid.");
            return;
        }

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        var trusted = botWideConfig.TrustedIds.FirstOrDefault(x => x.Id == id);
        if (trusted == null)
        {
            await FollowupAsync("ID doesn't exist, can't remove.");
            return;
        }

        botWideConfig.TrustedIds.Remove(trusted);

        await context.SaveChangesAsync();

        await FollowupAsync("Removed ID.");
    }

    [TrustedMember(TrustedId.TrustedUserPerms.TrustedUserEditPerms)]
    [SlashCommand("list-trusted-ids", "Lists the trusted IDs.")]
    public async Task ListTrustedIdsSlash()
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        await FollowupAsync($"```json\n{JsonConvert.SerializeObject(botWideConfig.TrustedIds, Formatting.Indented)}\n```");
    }

    [TrustedMember(TrustedId.TrustedUserPerms.BotGuildManagementPerms)]
    [SlashCommand("list-guilds", "Lists the guilds the bot is currently in.")]
    public async Task ListGuildsSlash()
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var botWideConfig = await context.GetBotWideConfig(Context.Client.CurrentUser.Id);

        List<PageBuilder> pages = [];

        foreach (var guild in client.Guilds)
        {
            await guild.DownloadUsersAsync();

            var eb = new PageBuilder()
                .WithOptionalColor(QuotingHelpers.GetUserRoleColorWithFallback(guild.CurrentUser, Color.Default));

            var trustedIds = botWideConfig.TrustedIds.Select(x => x.Id);
            foreach (var managerUserId in config.ManagerUserIds)
            {
                if (botWideConfig.TrustedIds.All(x => x.Id != managerUserId))
                {
                    trustedIds = trustedIds.Append(managerUserId);
                }
            }

            var trustedMembers = guild.Users.Where(x => trustedIds.Any(y => y == x.Id))
                .Select(x => $"* {x.Mention} ({x.Username}#{x.Discriminator}) - In trusted list")
                .Aggregate(new StringBuilder(), (x, y) => x.AppendLine(y)).ToString();

            if (trustedMembers.Length == 0)
            {
                trustedMembers = "None!";
            }

            var rssFeedsCount = await context.RssFeedListeners.Where(x => x.GuildId == guild.Id).CountAsync();

            eb.WithAuthor("Server Info");
            eb.WithTitle(guild.Name);
            eb.WithThumbnailUrl(guild.IconUrl);
            eb.WithFields([
                new EmbedFieldBuilder().WithName("Id").WithValue(guild.Id),
                new EmbedFieldBuilder().WithName("Owner").WithValue($"{guild.Owner.Mention} ({guild.Owner.Username}#{guild.Owner.Discriminator})"),
                new EmbedFieldBuilder().WithName("Members").WithValue(guild.MemberCount),
                new EmbedFieldBuilder().WithName("RSS Feeds").WithValue($"{rssFeedsCount} feed(s)"),
                new EmbedFieldBuilder().WithName("Known Members").WithValue(trustedMembers),
            ]);

            pages.Add(eb);
        }

        var paginator = new StaticPaginatorBuilder()
            .WithOptions(
            [
                new PaginatorButton("<", PaginatorAction.Backward, ButtonStyle.Secondary),
                new PaginatorButton("Jump", PaginatorAction.Jump, ButtonStyle.Secondary),
                new PaginatorButton(">", PaginatorAction.Forward, ButtonStyle.Secondary),
                new PaginatorButton(ModulePrefixes.RED_BUTTON, null, "X", ButtonStyle.Danger),
            ])
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .WithUsers(Context.User)
            .WithFooter(PaginatorFooter.PageNumber)
            .WithPages(pages);

        await interactive.SendPaginatorAsync(paginator.Build(), Context.Interaction, TimeSpan.FromMinutes(2), InteractionResponseType.DeferredChannelMessageWithSource);
    }

    //private readonly string[] classNames =
    //[
    //    "HoshimachiSuisei",
    //    "WatchMadeInAbyss",
    //    "HomuraBestGirl",
    //    "HisuiBestGirl",
    //    "KohakuBestGirl",
    //    "ArcueidBestGirl",
    //    "AkihaBestGirl",
    //    "AkaneBestGirl",
    //    "ZundamonBestVocaloid",
    //    "HatsuneMikuMySaviour",
    //];

    const string className = "KohakuBestGirl";
    const string methodName = "Execute";

    [SlashCommand("eval", "Runs a C# script.")]
    [TrustedMember(TrustedId.TrustedUserPerms.CodeExecutionPerms)]
    public async Task EvalSlash()
    {
        logger.LogInformation("{user} has started an eval!", Context.User);

        await DeferAsync();

        //var className = classNames[new Random().Next(classNames.Length)];

        var slashCommandMsg = await FollowupAsync(
            $@"**Reply** to this message with the code either in a text file or wrapped in a \`\`\`cs code block." +
            "Alternatively, say `cancel`.\n" +
            "Here's a handy template:" +
            $@"
```cs
using Discord;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

// context here is a required parameter. Class is activated via DI.
public class {className}(IInteractionContext context)
{{
    public async Task {methodName}()
    {{
        await context.Channel.SendMessageAsync(""hello!"");
    }}
}}
```
");

        var res =
            await interactive.NextMessageAsync(x =>
                                                    x.Channel.Id == Context.Channel.Id &&
                                                    x.Author.Id == Context.User.Id &&
                                                    x.Reference.MessageId.GetValueOrDefault() == slashCommandMsg.Id, timeout: TimeSpan.FromMinutes(5));

        if (res.IsCanceled || res.IsSuccess && res.Value.Content == "cancel")
        {
            await ModifyOriginalResponseAsync(new MessageContents("Canceled."));
            return;
        }

        if (res.IsTimeout)
        {
            await ModifyOriginalResponseAsync(new MessageContents("Timed out."));
            return;
        }

        if (!res.IsSuccess)
        {
            await ModifyOriginalResponseAsync(new MessageContents("Failed for some other reason."));
            return;
        }

        var msg = res.Value;

        logger.LogTrace("Searching for C# code.");

        var regexRes = CompiledRegex.CsharpCodeBlock().Match(msg.Content);

        string csharpCode;

        if (regexRes.Success)
        {
            logger.LogTrace("Found in message as code block.");
            csharpCode = regexRes.Groups[1].Value;

            logger.LogTrace("Code is\n{csharpCode}", csharpCode);
        }
        else if (msg.Attachments.Count != 0)
        {
            logger.LogTrace("Found in message as attachment.");
            var attachment = msg.Attachments.First();

            using var req = await http.GetAsync(attachment.Url);

            csharpCode = await req.Content.ReadAsStringAsync();
            logger.LogTrace("Code is\n{csharpCode}", csharpCode);
        }
        else
        {
            logger.LogTrace("Couldn't find any C# code.");
            await ModifyOriginalResponseAsync(new MessageContents("Couldn't find any code in your message."));
            return;
        }

        await ModifyOriginalResponseAsync(new MessageContents($"{config.LoadingEmote} Compiling..."));

        logger.LogTrace("Parsing.");
        var syntaxTree = CSharpSyntaxTree.ParseText(csharpCode);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(x => x.Location != "")
            .Select(x => MetadataReference.CreateFromFile(x.Location))
            .ToArray();

        logger.LogTrace("Creating a new compilation.");
        var compilation = CSharpCompilation.Create("SuperDangerousCode", [syntaxTree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();

        logger.LogTrace("Emitting.");
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Failed to compile! {result.Diagnostics.Length} Diagnostics.");

            foreach (var diagnostic in result.Diagnostics.OrderByDescending(x => x.Severity))
            {
                sb.AppendLine($"`({diagnostic.Severity.Humanize()})` `{diagnostic.Id}`: \n```{diagnostic.GetMessage()}```");
            }

            await ModifyOriginalResponseAsync(new MessageContents(sb.ToString()));
            logger.LogTrace("Failed to compile. {sbRes}", sb.ToString());

            return;
        }

        logger.LogTrace("Loading assembly.");
        await ModifyOriginalResponseAsync(new MessageContents($"{config.LoadingEmote} Executing..."));

        ms.Seek(0, SeekOrigin.Begin);

        WeakReference? reference = null;
        try
        {
            reference = await ExecuteMemoryStream(ms, className);
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Failed to create object. {exception}", ex.Message);
            await ModifyOriginalResponseAsync(new MessageContents($"Creation of object failed:\n```\n{ex}\n```"));
        }
        finally
        {
            if (reference != null)
            {
                for (int i = 0; reference.IsAlive && (i < 10); i++)
                {
                    logger.LogTrace("GC {i}", i);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                if (reference.IsAlive)
                {
                    logger.LogWarning("Reference is still alive!!");
                    try
                    {
                        await Context.User.SendMessageAsync($"Assembly did not unload! " +
                                                            $"This isn't great as this means that the assembly will most likely be permanently loaded until next restart of bot.");
                    }
                    catch
                    {
                        // ignored
                    }
                }
                else
                {
                    logger.LogInformation("Successfully cleaned up.");
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<WeakReference> ExecuteMemoryStream(MemoryStream ms, string className)
    {
        var assemblyLoadContext = new AssemblyLoadContext(null, true);
        var weakRef = new WeakReference(assemblyLoadContext, trackResurrection: true);
        try
        {
            var assembly = assemblyLoadContext.LoadFromStream(ms);
            var type = assembly.GetType(className);
            if (type == null)
            {
                var types = assembly.ExportedTypes.Select(x => x.FullName).Humanize();

                logger.LogTrace("Could not find class {className}. Found types: {types}", className, types);
                await ModifyOriginalResponseAsync(new MessageContents($"Could not find class `{className}`."));
                return weakRef;
            }

            var obj = ActivatorUtilities.CreateInstance(services, type, Context);

            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);

            if (method == null)
            {
                logger.LogTrace("Could not find method {methodName}.", methodName);
                await ModifyOriginalResponseAsync(new MessageContents($"Could not find class `{className}`."));
                return weakRef;
            }

            try
            {
                if (method.ReturnType == typeof(void))
                {
                    method.Invoke(obj, null);
                }
                else if (method.ReturnType == typeof(Task))
                {
                    var task = (Task)method.Invoke(obj, null)!;

                    await task;
                }
            }
            catch (Exception ex)
            {
                logger.LogTrace(ex, "Execution failed, exception {exception}", ex.Message);
                await ModifyOriginalResponseAsync(new MessageContents($"Execution failed:\n```\n{ex}\n```"));
                return weakRef;
            }

            logger.LogTrace("Finished execution.");
            await ModifyOriginalResponseAsync(new MessageContents($"Execution complete."));
        }
        finally
        {
            assemblyLoadContext.Unload();
            logger.LogTrace("Set assemblyLoadContext to unload.");
            assemblyLoadContext = null;
        }

        return weakRef;
    }
}