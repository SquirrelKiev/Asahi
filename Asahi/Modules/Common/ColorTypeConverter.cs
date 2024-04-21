using System.Diagnostics.Contracts;
using Discord.Interactions;

namespace Asahi.Modules;

public class ColorTypeConverter : TypeConverter<Color>
{
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        try
        {
            var value = (string)option.Value;

            var color = ParseColorInput(value);

            return Task.FromResult(TypeConverterResult.FromSuccess(color));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TypeConverterResult.FromError(ex));
        }
    }

    [Pure]
    private static Color ParseColorInput(string input)
    {
        var color = System.Drawing.ColorTranslator.FromHtml(input);

        return new Color(color.R, color.G, color.B);
    }
}