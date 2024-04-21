using System.Diagnostics.CodeAnalysis;
using Discord.Interactions;

namespace Asahi.Modules;

public class EmoteTypeConverter : TypeConverter<IEmote>
{
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        try
        {
            var value = (string)option.Value;

            return Task.FromResult(TryParse(value, out var parsedEmote) ? 
                TypeConverterResult.FromSuccess(parsedEmote) :
                TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, "Emote could not be parsed."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TypeConverterResult.FromError(ex));
        }
    }

    public static bool TryParse(string text, [NotNullWhen(true)]out IEmote? result)
    {
        if (Emoji.TryParse(text, out var outEmoji))
        {
            result = outEmoji;
            return true;
        }

        if (Emote.TryParse(text, out var outEmote))
        {
            result = outEmote;
            return true;
        }
        result = null;
        return false;
    }
}