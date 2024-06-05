using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Globalization;
using Humanizer;
using NodaTime;

namespace Asahi.Modules.BirthdayRoles;

public static class AnnualDateExtensions
{
    public static string ToStringOrdinalized(this AnnualDate date)
    {
        return
            $"{date.Day.Ordinalize(CultureInfo.GetCultureInfo("en-US"))} {date.ToString("MMMM", CultureInfo.InvariantCulture)}";
    }
}