using Asahi.BotEmoteManagement;

namespace Asahi.Modules;

public record BotEmotesSpecification
{
    public IEmoteSpecification Error { get; init; } = new UnicodeEmoteSpecification("❓");
    public IEmoteSpecification Loading { get; init; } = new UnicodeEmoteSpecification("🤔");
    public IEmoteSpecification Pixiv { get; init; } = new InternalCustomEmoteSpecification("PixivLogo");
    public IEmoteSpecification Twitter { get; init; } = new InternalCustomEmoteSpecification("TwitterLogo");
    public IEmoteSpecification Baraag { get; init; } = new InternalCustomEmoteSpecification("BaraagLogo");
    public IEmoteSpecification FanboxCc { get; init; } = new InternalCustomEmoteSpecification("FanboxLogo");
    public IEmoteSpecification Fantia { get; init; } = new InternalCustomEmoteSpecification("FantiaLogo");
    public IEmoteSpecification Misskey { get; init; } = new InternalCustomEmoteSpecification("MisskeyLogo");
}

public record BotEmotes
{
    public IEmote Error { get; init; } = null!;
    public IEmote Loading { get; init; } = null!;
    public IEmote Pixiv { get; init; } = null!;
    public IEmote Twitter { get; init; } = null!;
    public IEmote Baraag { get; init; } = null!;
    public IEmote FanboxCc { get; init; } = null!;
    public IEmote Fantia { get; init; } = null!;
    public IEmote Misskey { get; init; } = null!;
}
