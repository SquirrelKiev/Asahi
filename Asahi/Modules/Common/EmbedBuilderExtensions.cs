using Fergun.Interactive;

namespace Asahi.Modules;

public static class EmbedBuilderExtensions
{
    public static EmbedBuilder WithOptionalColor(this EmbedBuilder builder, Color color)
    {
        if (color != Color.Default)
            builder.WithColor(color);

        return builder;
    }

    public static PageBuilder WithOptionalColor(this PageBuilder builder, Color color)
    {
        if (color != Color.Default)
            builder.WithColor(color);

        return builder;
    }
}