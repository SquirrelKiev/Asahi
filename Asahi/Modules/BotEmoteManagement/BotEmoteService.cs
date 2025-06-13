using Asahi.BotEmoteManagement;

namespace Asahi.Modules;

public record BotEmotesSpecification
{
    public IEmoteSpecification Error { get; init; } = new UnicodeEmoteSpecification("❓");
    public IEmoteSpecification Loading { get; init; } = new InternalCustomEmoteSpecification("Loading");
    public IEmoteSpecification Pixiv { get; init; } = new InternalCustomEmoteSpecification("PixivLogo");
    public IEmoteSpecification Twitter { get; init; } = new InternalCustomEmoteSpecification("TwitterLogo");
    public IEmoteSpecification Baraag { get; init; } = new InternalCustomEmoteSpecification("BaraagLogo");
    public IEmoteSpecification FanboxCc { get; init; } = new InternalCustomEmoteSpecification("FanboxLogo");
    public IEmoteSpecification Fantia { get; init; } = new InternalCustomEmoteSpecification("FantiaLogo");
    public IEmoteSpecification Misskey { get; init; } = new InternalCustomEmoteSpecification("MisskeyLogo");
    public IEmoteSpecification ArcaLive { get; init; } = new InternalCustomEmoteSpecification("ArcaLiveLogo");
}

[GenerateEmoteManager(typeof(BotEmotesSpecification))]
public partial class BotEmoteService;
