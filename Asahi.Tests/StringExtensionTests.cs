using System.Globalization;
using FluentAssertions;
using Xunit.Abstractions;

namespace Asahi.Tests;

public class StringExtensionTests
{
    public StringExtensionTests(ITestOutputHelper output)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    [Theory]
    [InlineData(new[] { "Gotoh Hitori" }, 256, "Gotoh Hitori")] // single item
    [InlineData(new[] { "Ijichi Nijika" }, 5, "Ijic…")] // single item that's too long (will this be an issue?)
    [InlineData(new string[] { }, 256, "")] // empty array
    [InlineData(new[] { "Gotoh Hitori", "Ijichi Nijika" }, 256, "Gotoh Hitori & Ijichi Nijika")] // multiple items
    [InlineData(new[] { "Gotoh Hitori", "Ijichi Nijika", "Kita Ikuyo" }, 256, "Gotoh Hitori, Ijichi Nijika & Kita Ikuyo")] // multiple items 2 (am I testing an already tested library by accident here?)
    [InlineData(new[] { "Gotoh Hitori", "Ijichi Nijika", "Kita Ikuyo" }, 30, "Gotoh Hitori & 2 more")] // truncation
    public void HumanizeStringArrayWithTruncation_Various(
        string[] input,
        int maxLength,
        string expected
    )
    {
        var result = input.HumanizeStringArrayWithTruncation(maxLength);
        result.Should().Be(expected);
    }
}
