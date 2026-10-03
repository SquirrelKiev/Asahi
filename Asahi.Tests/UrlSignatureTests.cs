using AwesomeAssertions;
using Purpose = Asahi.UrlSignature.UrlSignaturePurposes;

namespace Asahi.Tests;

public class UrlSignatureTests
{
    private const string Resource = "https://v.animethemes.moe/Bakemonogatari-OP4.webm";
    private const string AltResource = "https://v.animethemes.moe/Bakemonogatari-OP3.webm";

    private static readonly byte[] KeyA = [.. "really-good-cool-awesome-key-001"u8];
    private static readonly byte[] KeyB = [.. "really-good-cool-awesome-key-002"u8];

    private static readonly Dictionary<string, byte[]> Keys = new()
    {
        ["a"] = KeyA,
        ["b"] = KeyB,
    };

    [Theory]
    [InlineData("a", Purpose.Thumbnail)]
    [InlineData("a", Purpose.Proxy)]
    [InlineData("b", Purpose.Proxy)]
    public void Verify_AcceptsSignatureFromCreate(string keyId, Purpose purpose)
    {
        string signature = UrlSignature.Create(keyId, Keys[keyId], purpose, Resource);

        UrlSignature.Verify(Keys, purpose, Resource, signature).Should().BeTrue();
    }

    [Fact]
    public void Verify_RejectsDifferentResource()
    {
        string signature = UrlSignature.Create("a", KeyA, Purpose.Proxy, Resource);

        UrlSignature.Verify(Keys, Purpose.Proxy, AltResource, signature).Should().BeFalse();
    }

    [Fact]
    public void Verify_RejectsDifferentPurpose()
    {
        string signature = UrlSignature.Create("a", KeyA, Purpose.Thumbnail, Resource);

        UrlSignature.Verify(Keys, Purpose.Proxy, Resource, signature).Should().BeFalse();
    }

    [Fact]
    public void Verify_RejectsSignatureMadeWithWrongKeyForKeyId()
    {
        string signature = UrlSignature.Create("a", KeyB, Purpose.Proxy, Resource);

        UrlSignature.Verify(Keys, Purpose.Proxy, Resource, signature).Should().BeFalse();
    }

    [Fact]
    public void Verify_RejectsUnknownKeyId()
    {
        string signature = UrlSignature.Create("c", KeyA, Purpose.Proxy, Resource);

        UrlSignature.Verify(Keys, Purpose.Proxy, Resource, signature).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a.")]
    [InlineData(".")]
    [InlineData(".abc")]
    [InlineData("garbage")]
    [InlineData("a.super-real-signature")]
    [InlineData("a.日本語")]
    // HMAC-SHA256("really-bad-terrible-crap-key-001", "thumbnail:" + Resource)
    [InlineData("a.48eL2Oy0Oj3YjJieY6gjVAycVoE5PytlvUkOSuRo9aU=")]
    public void Verify_RejectsInvalidSignature(string signature)
    {
        UrlSignature.Verify(Keys, Purpose.Proxy, Resource, signature).Should().BeFalse();
    }

    [Fact]
    public void Create_ReturnsKeyIdPrefixedUrlSafeSignature()
    {
        string signature = UrlSignature.Create("a", KeyA, Purpose.Proxy, Resource);

        signature.Should().StartWith("a.");
        Uri.EscapeDataString(signature).Should().Be(signature);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("T")]
    [InlineData("2026-10.prod_1")]
    public void IsValidKeyId_AcceptsUrlSafeIds(string keyId)
    {
        UrlSignature.IsValidKeyId(keyId).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a+b")]
    [InlineData("a b")]
    [InlineData("a&b")]
    [InlineData("a#b")]
    [InlineData("a:b")]
    [InlineData("a\n")]
    [InlineData("日本語")]
    public void IsValidKeyId_RejectsUnsafeIds(string keyId)
    {
        UrlSignature.IsValidKeyId(keyId).Should().BeFalse();
    }

    [Theory]
    [InlineData(Purpose.Thumbnail, "thumbnail")]
    [InlineData(Purpose.Proxy, "proxy")]
    public void PurposeToString_IsStable(Purpose purpose, string expected)
    {
        UrlSignature.PurposeToString(purpose).Should().Be(expected);
    }

    // HMAC-SHA256(KeyA, "thumbnail:" + Resource) = eY82VvkfZY7uEQuHvN+h+pUYuKOf3wOu2d6JPIQVHWg=
    private const string KnownThumbnailSignature = "a.eY82VvkfZY7uEQuHvN-h-pUYuKOf3wOu2d6JPIQVHWg";

    [Fact]
    public void Create_MatchesKnownSignature()
    {
        UrlSignature.Create("a", KeyA, Purpose.Thumbnail, Resource).Should().Be(KnownThumbnailSignature);
    }

    [Fact]
    public void Verify_AcceptsKnownSignature()
    {
        UrlSignature.Verify(Keys, Purpose.Thumbnail, Resource, KnownThumbnailSignature).Should().BeTrue();
    }
    
    [Fact]
    public void Verify_AcceptsKeyIdWithDots()
    {
        const string keyId = "my.awesome.key";
        var keys = new Dictionary<string, byte[]>()
        {
            { keyId, KeyA }
        };
        var signature = UrlSignature.Create(keyId, KeyA, Purpose.Thumbnail, Resource);
        
        UrlSignature.Verify(keys, Purpose.Thumbnail, Resource, signature).Should().BeTrue();
    }
}
