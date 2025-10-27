using Asahi.Database.Models;
using Discord;
using AwesomeAssertions;

namespace Asahi.Tests;

// I need to redo these tests at some point
public class CachedHighlightedMessageTests
{
    private static CachedMessageReaction
        CreateReaction(CachedHighlightedMessage chm, string emoteName, int count = 1) =>
        new()
        {
            EmoteName = emoteName,
            EmoteId = 0ul,
            IsAnimated = false,
            Count = count,
            HighlightedMessage = chm,
        };

    private static CachedMessageReaction CreateReaction(CachedHighlightedMessage chm, string emoteName, ulong emoteId,
        bool isAnimated, int count = 1) =>
        new()
        {
            EmoteName = emoteName,
            EmoteId = emoteId,
            IsAnimated = isAnimated,
            Count = count,
            HighlightedMessage = chm,
        };

    private static CachedHighlightedMessage CreateHighlightMessage() =>
        new CachedHighlightedMessage
        {
            OriginalMessageChannelId = 123ul,
            OriginalMessageId = 123ul,
            // just the time I'm writing this test :chatting:
            HighlightedMessageSendDate = new DateTime(2024, 11, 11, 8, 45, 00, DateTimeKind.Utc),
            AuthorId = 123ul,
            AssistAuthorId = null,
            TotalUniqueReactions = 1,
            HighlightMessageIds = [123ul],
            CachedMessageReactions = []
        };

    [Fact]
    public void UpdateReactions_EmptyInputs_ShouldNotModifyList()
    {
        // arrange
        var chm = CreateHighlightMessage();

        var emoteUserMap = new Dictionary<IEmote, HashSet<ulong>>();
        var existingReactions = new List<CachedMessageReaction>();

        chm.CachedMessageReactions = existingReactions;

        // act
        chm.UpdateReactions(emoteUserMap);

        // assert
        existingReactions.Should().BeEmpty();
    }

    [Fact]
    public void UpdateReactions_PopulatedMapWithEmptyReactions()
    {
        // arrange
        var chm = CreateHighlightMessage();

        var emoteUserMap = new Dictionary<IEmote, HashSet<ulong>>()
        {
            { new Emoji("😭"), [123ul, 456ul] },
            { new Emoji("🗣️"), [123ul, 456ul, 789ul] }
        };
        var existingReactions = new List<CachedMessageReaction>();

        chm.CachedMessageReactions = existingReactions;

        // act
        chm.UpdateReactions(emoteUserMap);

        // assert
        existingReactions.Should()
            .Contain(x => x.EmoteName == "😭")
            .Which.Count.Should().Be(2);
        existingReactions.Should()
            .Contain(x => x.EmoteName == "🗣️")
            .Which.Count.Should().Be(3);

        existingReactions.Should().AllSatisfy(x => x.HighlightedMessage.Should().BeSameAs(chm));

        existingReactions.Should()
            .HaveCount(2);
    }

    [Fact]
    public void UpdateReactions_EmptyMapWithExistingReactions_ShouldClearAll()
    {
        // arrange
        var chm = CreateHighlightMessage();

        var emoteUserMap = new Dictionary<IEmote, HashSet<ulong>>();
        var existingReactions = new List<CachedMessageReaction>
        {
            CreateReaction(chm, "😭"),
            CreateReaction(chm, "🗣️")
        };

        chm.CachedMessageReactions = existingReactions;

        // act
        chm.UpdateReactions(emoteUserMap);

        // assert
        existingReactions.Should().BeEmpty();
    }

    [Fact]
    public void UpdateReactions_Gauntlet()
    {
        // arrange
        var chm = CreateHighlightMessage();

        var emojiWillChangeCount = new Emoji("🦐");
        var emojiNewlyAdded = new Emoji("🗣️");
        var customEmoteCountStaysSame = new Emote(123UL, "custom1", false);
        var customEmoteWillBeRemoved = new Emote(456UL, "custom2", false);
        var customEmoteNewlyAdded = new Emote(789UL, "custom3", false);
        var customEmoteAnimatedNewlyAdded = new Emote(1234UL, "custom4animated", true);

        var originalChangeCountEmoji = CreateReaction(chm, emojiWillChangeCount.Name, count: 5); // will decrease
        var originalUnchangedCustomEmote = CreateReaction(chm, customEmoteCountStaysSame.Name, // will remain unchanged
            customEmoteCountStaysSame.Id, isAnimated: customEmoteCountStaysSame.Animated, count: 4);
        var originalRemovedEmote = CreateReaction(chm, customEmoteWillBeRemoved.Name, // will be removed
            customEmoteWillBeRemoved.Id, isAnimated: customEmoteWillBeRemoved.Animated, count: 1);

        var oldReactions = new List<CachedMessageReaction>()
        {
            originalChangeCountEmoji,
            originalUnchangedCustomEmote,
            originalRemovedEmote
        };

        var newReactions = new List<CachedMessageReaction>(oldReactions);

        var emoteUserMap = new Dictionary<IEmote, HashSet<ulong>>
        {
            // updated reactions
            { emojiWillChangeCount, new HashSet<ulong> { 1, 2 } }, // count 5 -> 2
            { customEmoteCountStaysSame, new HashSet<ulong> { 1, 2, 3, 4 } }, // stays at 4

            // new reactions
            { emojiNewlyAdded, new HashSet<ulong> { 1, 2, 3, 4, 5 } }, // new with count 5
            { customEmoteNewlyAdded, new HashSet<ulong> { 1, 2 } }, // new with count 2
            { customEmoteAnimatedNewlyAdded, new HashSet<ulong> { 1, 2 } } // new with count 3
        };

        chm.CachedMessageReactions = newReactions;

        // act
        chm.UpdateReactions(emoteUserMap);

        // assert
        newReactions.Should().Contain(r =>
                r.EmoteName == emojiWillChangeCount.Name &&
                r.EmoteId == 0 &&
                r.IsAnimated == false &&
                r.Count == 2)
            .Which.Should().BeSameAs(originalChangeCountEmoji);

        newReactions.Should().Contain(r =>
                r.EmoteName == customEmoteCountStaysSame.Name &&
                r.EmoteId == customEmoteCountStaysSame.Id &&
                r.IsAnimated == customEmoteCountStaysSame.Animated &&
                r.Count == 4)
            .Which.Should().BeSameAs(originalUnchangedCustomEmote);

        newReactions.Should().Contain(r =>
                r.EmoteName == emojiNewlyAdded.Name &&
                r.EmoteId == 0 &&
                r.IsAnimated == false &&
                r.Count == 5)
            .Which.Should().NotBeSameAs(oldReactions.Single(x => x == originalChangeCountEmoji))
            .And.NotBeSameAs(oldReactions.Single(x => x == originalUnchangedCustomEmote))
            .And.NotBeSameAs(oldReactions.Single(x => x == originalRemovedEmote));

        newReactions.Should().Contain(r =>
                r.EmoteName == customEmoteNewlyAdded.Name &&
                r.EmoteId == customEmoteNewlyAdded.Id &&
                r.IsAnimated == customEmoteNewlyAdded.Animated &&
                r.Count == 2)
            .Which.Should().NotBeSameAs(oldReactions.Single(x => x == originalChangeCountEmoji))
            .And.NotBeSameAs(oldReactions.Single(x => x == originalUnchangedCustomEmote))
            .And.NotBeSameAs(oldReactions.Single(x => x == originalRemovedEmote));

        newReactions.Should().Contain(r =>
                r.EmoteName == customEmoteAnimatedNewlyAdded.Name &&
                r.EmoteId == customEmoteAnimatedNewlyAdded.Id &&
                r.IsAnimated == customEmoteAnimatedNewlyAdded.Animated &&
                r.Count == 2)
            .Which.Should().NotBeSameAs(oldReactions.Single(x => x == originalChangeCountEmoji))
            .And.NotBeSameAs(oldReactions.Single(x => x == originalUnchangedCustomEmote))
            .And.NotBeSameAs(oldReactions.Single(x => x == originalRemovedEmote));

        newReactions.Should().AllSatisfy(x => x.HighlightedMessage.Should().BeSameAs(chm));

        newReactions.Should().NotContain(r => r.EmoteName == customEmoteWillBeRemoved.Name);
    }
}