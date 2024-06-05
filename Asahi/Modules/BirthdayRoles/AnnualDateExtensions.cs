using System.Globalization;
using Humanizer;
using NodaTime;

namespace Asahi.Modules.BirthdayRoles;

public static class AnnualDateExtensions
{
    public static string ToStringOrdinalized(this AnnualDate date)
    {
        return
            $"{date.Day.Ordinalize(CultureInfo.GetCultureInfo("en-US"))} of {date.ToString("MMMM", CultureInfo.InvariantCulture)}";
    }
}