using Asahi.Database.Models;
using Discord;
using FluentAssertions;

namespace Asahi.Tests;

public class CachedHighlightedMessageTests
{
    private const uint DefaultHighlightedMessageId = 123;

    private CachedMessageReaction CreateReaction(string emoteName, int count = 1) =>
        new()
        {
            EmoteName = emoteName,
            EmoteId = 0ul,
            IsAnimated = false,
            Count = count,
            HighlightedMessageId = DefaultHighlightedMessageId,
        };

    private CachedMessageReaction CreateReaction(string emoteName, ulong emoteId, bool isAnimated, int count = 1) =>
        new()
        {
            EmoteName = emoteName,
            EmoteId = emoteId,
            IsAnimated = isAnimated,
            Count = count,
            HighlightedMessageId = DefaultHighlightedMessageId,
        };
    
    [Fact]
    public void UpdateReactions_EmptyInputs_ShouldNotModifyList()
    {
        // arrange
        var emoteUserMap = new Dictionary<IEmote, HashSet<ulong>>();
        var existingReactions = new List<CachedMessageReaction>();

        // act
        CachedHighlightedMessage.UpdateReactions(emoteUserMap, existingReactions);

        // assert
        existingReactions.Should().BeEmpty();
    }

    [Fact]
    public void UpdateReactions_EmptyMapWithExistingReactions_ShouldClearAll()
    {
        // arrange
        var emoteUserMap = new Dictionary<IEmote, HashSet<ulong>>();
        var existingReactions = new List<CachedMessageReaction>
        {
            CreateReaction("😭"),
            CreateReaction("🗣️")
        };

        // act
        CachedHighlightedMessage.UpdateReactions(emoteUserMap, existingReactions);

        // assert
        existingReactions.Should().BeEmpty();
    }

    [Fact]
    public void UpdateReactions_Gauntlet()
    {
        // arrange
        var emojiWillChangeCount = new Emoji("🦐");
        var emojiNewlyAdded = new Emoji("🗣️");
        var customEmoteCountStaysSame = new Emote(123UL, "custom1", false);
        var customEmoteWillBeRemoved = new Emote(456UL, "custom2", false);
        var customEmoteNewlyAdded = new Emote(789UL, "custom3", false);
        var customEmoteAnimatedNewlyAdded = new Emote(1234UL, "custom4animated", true);

        var originalChangeCountEmoji = CreateReaction(emojiWillChangeCount.Name, count: 5); // will decrease
        var originalUnchangedCustomEmote = CreateReaction(customEmoteCountStaysSame.Name, // will remain unchanged
            customEmoteCountStaysSame.Id, isAnimated: customEmoteCountStaysSame.Animated, count: 4);
        var originalRemovedEmote = CreateReaction(customEmoteWillBeRemoved.Name, // will be removed
            customEmoteWillBeRemoved.Id, isAnimated: customEmoteWillBeRemoved.Animated, count: 1);

        var oldReactions = new[]
        {
            originalChangeCountEmoji,
            originalUnchangedCustomEmote,
            originalRemovedEmote
        };

        var newReactions = new List<CachedMessageReaction>
        {
            originalChangeCountEmoji,
            originalUnchangedCustomEmote,
            originalRemovedEmote
        };

        var emoteUserMap = new Dictionary<IEmote, HashSet<ulong>>
        {
            // updated reactions
            { emojiWillChangeCount, new HashSet<ulong> { 1, 2 } }, // count 5 -> 2
            { customEmoteCountStaysSame, new HashSet<ulong> { 1, 2, 3, 4 } }, // stays at 4

            // new reactions
            { emojiNewlyAdded, new HashSet<ulong> { 1, 2, 3, 4, 5 } }, // new with count 5
            { customEmoteNewlyAdded, new HashSet<ulong> { 1, 2 } }, // new with count 2
            { customEmoteAnimatedNewlyAdded, new HashSet<ulong> { 1, 2 } }  // new with count 3
        };

        // act
        CachedHighlightedMessage.UpdateReactions(emoteUserMap, newReactions);

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

        newReactions.Should().NotContain(r => r.EmoteName == customEmoteWillBeRemoved.Name);
    }
}