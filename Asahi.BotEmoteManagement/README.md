# Emote management

This library provides a service for managing (custom) emotes used by your Discord bot.

The idea is it allows you to define the emotes your bot will use via a specification class, e.g.
```csharp
public record BotEmoteSpecifications
{
    public IEmoteSpecification ThumbsUp { get; init; } = new UnicodeEmoteSpecification("👍");
    public IEmoteSpecification Loading { get; init; } = new ExternalCustomEmoteSpecification("loading", 1234311316848640011, true);
    public IEmoteSpecification TwitterLogo { get; init; } = new InternalCustomEmoteSpecification("Twitter");
}
```
And then the service will resolve these specs into usable `IEmote`s on the model.
After initialisation, you can then simply get the desired emote via the service. E.g.
```csharp
public class SomeModule(BotEmoteManagerService emoteService) : BotModule
{
    [SlashCommand("send-emote", "Sends a test emote.")]
    public async Task SendEmoteSlash()
    {
        await RespondAsync($"{emoteService.Emotes.TwitterLogo}"); // will send "<:Twitter:1381909783925358602>"
    }
}
```

* `UnicodeEmoteSpecification` is simply resolved to `Emoji`.
* `ExternalCustomEmoteSpecification` is resolved to `Emote`.

`InternalCustomEmoteSpecification` however is the primary point of this library.
For any `InternalCustomEmoteSpecification`, the service will scan the internal emotes folder
(defined in `BotEmoteManagerConfig.InternalEmoteImagesFolder`) for an image file matching the emote key specified,
and synchronise it with your application's emotes. If the specification changes, the service will update Discord
accordingly, adding, removing, and updating (i.e. the image file changes) depending on what's changed in your spec.

---

Writing READMEs is not my specialty if you couldn't tell 😅