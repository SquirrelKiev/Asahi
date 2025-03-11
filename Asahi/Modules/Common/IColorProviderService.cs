namespace Asahi.Modules;

public interface IColorProviderService
{
    public ValueTask<Color> GetEmbedColor(ulong guildId);
}
