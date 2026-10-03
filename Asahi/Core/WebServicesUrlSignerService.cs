using System.Diagnostics.CodeAnalysis;

namespace Asahi;

[Inject(ServiceLifetime.Singleton)]
public class WebServicesUrlSignerService
{
    private readonly string? keyId;
    private readonly byte[]? key;
    [MemberNotNullWhen(true, nameof(keyId), nameof(key))]
    private bool SigningEnabled => keyId is not null && key is not null;

    public WebServicesUrlSignerService(BotConfig botConfig)
    {
        if(string.IsNullOrWhiteSpace(botConfig.AsahiWebServicesSigningKey) || string.IsNullOrWhiteSpace(botConfig.AsahiWebServicesSigningKeyId))
            return;
        
        keyId = botConfig.AsahiWebServicesSigningKeyId;
        key = Convert.FromBase64String(botConfig.AsahiWebServicesSigningKey);
    }

    public string Sign(UrlSignature.UrlSignaturePurposes purpose, string resource)
    {
        return SigningEnabled ? UrlSignature.Create(keyId, key, purpose, resource) : "";
    }
}
