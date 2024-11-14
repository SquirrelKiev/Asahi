using Humanizer;

namespace Asahi;

public static class StringExtensions
{
    // replace with humanizer truncate?
    public static string Truncate(this string str, int limit, bool useWordBoundary = true)
    {
        if (str.Length <= limit)
        {
            return str;
        }

        var subString = str[..limit].Trim();

        if (!useWordBoundary) return subString + '…';

        int lastSpaceIndex = subString.LastIndexOf(' ');
        if (lastSpaceIndex != -1)
        {
            subString = subString[..lastSpaceIndex].TrimEnd();
        }

        return subString + '…';
    }

    public static IEnumerable<string> SplitToLines(this string? input)
    {
        if (input == null)
        {
            yield break;
        }

        using StringReader reader = new(input);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    public static string StringOrDefault(this string? potential, string def)
    {
        return string.IsNullOrWhiteSpace(potential) ? def : potential;
    }
    
    public static string HumanizeStringArrayWithTruncation(this IEnumerable<string> strings, int maxLength = 256)
    {
        var characters = strings.ToList();
    
        for (int i = 1; i <= characters.Count; i++)
        {
            var tempList = characters.Take(i).ToList();
            var remainingCount = characters.Count - i;
        
            // check what the string would look like if we include this item and potentially the "X more" suffix
            var potentialList = remainingCount > 0 
                ? tempList.Concat(new[] { $"{remainingCount} more" }).ToList()
                : tempList;
            
            var formatted = potentialList.Humanize();
        
            if (formatted.Length > maxLength)
            {
                // go back one step and add the "more" count
                var finalList = characters.Take(i - 1)
                    .Concat(new[] { $"{characters.Count - (i - 1)} more" });

                var finalString = finalList.Humanize();
                
                if(finalString.Length > maxLength)
                    finalString = characters[0].Truncate(maxLength, false);
                
                return finalString;
            }
        
            if (i == characters.Count)
                return formatted;
        }

        //reached if the enumerable is empty
        return "";
    }
}
