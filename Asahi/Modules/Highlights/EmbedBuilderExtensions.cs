namespace Asahi.Modules.Highlights;

public static class EmbedBuilderExtensions
{
    public static EmbedBuilder WithOptionalColor(this EmbedBuilder builder, Color color)
    {
        if (color != Color.Default)
            builder.WithColor(color);

        return builder;
    }
}