using System.Numerics;
using System.Text.RegularExpressions;

namespace April.Config;

public partial class ValidationUtilities
{
    public static ulong StripAndConvertToULong(string input)
    {
        // remove any characters that are not a number
        string sanitizedInput = NotNumeric().Replace(input, "");

        if (string.IsNullOrEmpty(sanitizedInput))
        {
            sanitizedInput = "0";
        }

        // can handle numbers bigger than ulong
        var bigIntValue = BigInteger.Parse(sanitizedInput);

        if (bigIntValue > ulong.MaxValue)
        {
            return ulong.MaxValue;
        }
        else
        {
            return (ulong)bigIntValue;
        }
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex NotNumeric();
}