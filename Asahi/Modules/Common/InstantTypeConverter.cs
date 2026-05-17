using Discord.Interactions;
using NodaTime;

namespace Asahi.Modules;

public class InstantTypeConverter : TypeConverter<Instant>
{
    public override ApplicationCommandOptionType GetDiscordType()
    {
        return ApplicationCommandOptionType.String;
    }

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        if (option.Value is not string val)
        {
            return Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ConvertFailed,
                "input is not a string"));
        }

        if (long.TryParse(val, out var result))
        {
            return Task.FromResult(TypeConverterResult.FromSuccess(Instant.FromUnixTimeSeconds(result)));
        }

        var match = CompiledRegex.DiscordTimestampToUnixTimestamp().Match(val);

        if (!match.Success)
        {
            return Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, "input wasn't a unix timestamp or discord timestamp"));
        }

        return Task.FromResult(TypeConverterResult.FromSuccess(Instant.FromUnixTimeSeconds(long.Parse(match.Groups[1].ValueSpan))));
    }
}
