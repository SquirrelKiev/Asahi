using Asahi.BotEmoteManagement;
using Discord;
using FluentAssertions;
using NSubstitute;

namespace Asahi.Tests;

public sealed class BotEmoteManagerServiceTests
{
    public record SingleEmoteSpec
    {
        public IEmoteSpecification Emote { get; init; }
    }
    
    public record SingleEmoteModel
    {
        public IEmote Emote { get; init; }
    }
    
    public record MultipleEmotesSpec
    {
        public IEmoteSpecification Emote1 { get; init; }
        public IEmoteSpecification Emote2 { get; init; }
    }
    
    public record MultipleEmotesModel
    {
        public IEmote Emote1 { get; init; }
        public IEmote Emote2 { get; init; }
    }
    
    [Fact]
    public async Task InitializeAsync_UnicodeEmoteSpec_ResolvesCorrectly()
    {
        // arrange
        var discordClient = Substitute.For<IDiscordClient>();
        var internalEmoteSource = Substitute.For<IInternalEmoteSource>();
        
        var resolverService = new DiscordEmoteResolverService(discordClient, internalEmoteSource);
        var service = new ReflectionBasedBotEmoteManagerService<SingleEmoteSpec, SingleEmoteModel>(resolverService);
        var spec = new SingleEmoteSpec()
        {
            Emote = new UnicodeEmoteSpecification("🤔"),
        };
        var tracking = new List<InternalCustomEmoteTracking>();
        
        // act
        await service.InitializeAsync(spec, tracking);
        
        //assert
        service.Emotes.Emote.Should().BeOfType<Emoji>();
        service.Emotes.Emote.Name.Should().Be("🤔");
    }

    [Fact]
    public async Task InitializeAsync_ExternalEmoteSpec_ResolvesCorrectly()
    {
        // arrange
        var discordClient = Substitute.For<IDiscordClient>();
        var internalEmoteSource = Substitute.For<IInternalEmoteSource>();
        
        var resolverService = new DiscordEmoteResolverService(discordClient, internalEmoteSource);
        var service = new ReflectionBasedBotEmoteManagerService<SingleEmoteSpec, SingleEmoteModel>(resolverService);
        const string emoteName = "okay";
        const ulong emoteId = 123ul;
        var spec = new SingleEmoteSpec
        {
            Emote = new ExternalCustomEmoteSpecification(emoteName, emoteId, false),
        };
        var tracking = new List<InternalCustomEmoteTracking>();
        
        // act
        await service.InitializeAsync(spec, tracking);
        
        //assert
        service.Emotes.Emote.Should().BeOfType<Emote>();
        var emote = (Emote)service.Emotes.Emote;
        emote.Name.Should().Be(emoteName);
        emote.Id.Should().Be(emoteId);
        emote.Animated.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_InternalEmoteSpecWithMissingKey_Throws()
    {
        // arrange
        var discordClient = Substitute.For<IDiscordClient>();
        var internalEmoteSource = Substitute.For<IInternalEmoteSource>();
        
        internalEmoteSource.GetAvailableEmoteKeys().Returns([]);

        var resolverService = new DiscordEmoteResolverService(discordClient, internalEmoteSource);
        var service = new ReflectionBasedBotEmoteManagerService<SingleEmoteSpec, SingleEmoteModel>(resolverService);
        var spec = new SingleEmoteSpec
        {
            Emote = new InternalCustomEmoteSpecification("okay"),
        };
        var tracking = new List<InternalCustomEmoteTracking>();
        
        // act
        var act = () => service.InitializeAsync(spec, tracking);
        
        // assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task InitializeAsync_EmoteAddedToInternalEmoteSpec_AddsNewEmoteToDiscord()
    {
        // arrange
        var discordClient = Substitute.For<IDiscordClient>();
        var internalEmoteSource = Substitute.For<IInternalEmoteSource>();

        const string emoteKey = "okay";
        const ulong emoteId = 123ul;
        byte[] emoteData = [0xDE, 0xAD, 0xBE, 0xEF];
        
        internalEmoteSource.GetAvailableEmoteKeys().Returns([emoteKey]);
        internalEmoteSource.GetEmoteDataStream(emoteKey).Returns(_ => new MemoryStream(emoteData));
        internalEmoteSource.GetEmoteDataIdentifierAsync(emoteKey).Returns(Task.FromResult(emoteData));

        discordClient.GetApplicationEmotesAsync().Returns(Task.FromResult<IReadOnlyCollection<Emote>>([]));
        var createdEmote = new Emote(emoteId, emoteKey, false);
        discordClient.CreateApplicationEmoteAsync(emoteKey, Arg.Any<Image>()).Returns(Task.FromResult(createdEmote));
        
        var resolverService = new DiscordEmoteResolverService(discordClient, internalEmoteSource);
        var service = new ReflectionBasedBotEmoteManagerService<SingleEmoteSpec, SingleEmoteModel>(resolverService);

        var spec = new SingleEmoteSpec
        {
            Emote = new InternalCustomEmoteSpecification(emoteKey)
        };
        
        var tracking = new List<InternalCustomEmoteTracking>();
        
        // act
        await service.InitializeAsync(spec, tracking);
        
        // assert
        await discordClient.Received(1).CreateApplicationEmoteAsync(emoteKey, Arg.Any<Image>());
        tracking.Should().ContainSingle(e =>
            e.EmoteKey == emoteKey &&
            e.EmoteId == emoteId &&
            e.EmoteDataIdentifier.SequenceEqual(emoteData));
        
        service.Emotes.Emote.Should().BeOfType<Emote>();
        
        var emote = (Emote)service.Emotes.Emote;
        emote.Name.Should().Be(emoteKey);
        emote.Id.Should().Be(emoteId);
        emote.Animated.Should().BeFalse();
    }
    
    [Fact]
    public async Task InitializeAsync_EmoteRemovedFromInternalEmoteSpec_RemovesEmoteFromDiscord()
    {
        // arrange
        var discordClient = Substitute.For<IDiscordClient>();
        var internalEmoteSource = Substitute.For<IInternalEmoteSource>();
        
        internalEmoteSource.GetAvailableEmoteKeys().Returns([]);
        
        const string emoteKey = "okay";
        const ulong emoteId = 123ul;
        byte[] emoteData = [0xDE, 0xAD, 0xBE, 0xEF];
        
        var oldEmote = new Emote(emoteId, emoteKey, false);
        discordClient.GetApplicationEmotesAsync().Returns(Task.FromResult<IReadOnlyCollection<Emote>>([oldEmote]));
        discordClient.DeleteApplicationEmoteAsync(emoteId).Returns(Task.CompletedTask).AndDoes(_ =>
        {
            discordClient.GetApplicationEmotesAsync().Returns(Task.FromResult<IReadOnlyCollection<Emote>>([]));
        });
        
        var resolverService = new DiscordEmoteResolverService(discordClient, internalEmoteSource);
        var service = new ReflectionBasedBotEmoteManagerService<SingleEmoteSpec, SingleEmoteModel>(resolverService);

        var spec = new SingleEmoteSpec
        {
            Emote = new UnicodeEmoteSpecification("🤔")
        };
        
        var tracking = new List<InternalCustomEmoteTracking>
        {
            new()
            {
                EmoteKey = emoteKey,
                EmoteId = emoteId,
                EmoteDataIdentifier = emoteData,
                IsAnimated = oldEmote.Animated
            }
        };
        
        // act
        await service.InitializeAsync(spec, tracking);
        
        // assert
        await discordClient.Received(1).DeleteApplicationEmoteAsync(emoteId);
        tracking.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_InternalEmoteIdentifierChanges_UpdatesEmoteOnDiscord()
    {
        // arrange
        var discordClient = Substitute.For<IDiscordClient>();
        var internalEmoteSource = Substitute.For<IInternalEmoteSource>();
        
        const string emoteKey = "okay";
        const ulong oldEmoteId = 123ul;
        const ulong newEmoteId = 456ul;
        
        byte[] oldEmoteData = [0xDE, 0xAD, 0xBE, 0xEF];
        byte[] newEmoteData = [0xCA, 0xFE, 0xDE, 0xAD];
        
        internalEmoteSource.GetAvailableEmoteKeys().Returns([emoteKey]);
        internalEmoteSource.GetEmoteDataStream(emoteKey).Returns(_ => new MemoryStream(oldEmoteData));
        internalEmoteSource.GetEmoteDataIdentifierAsync(emoteKey).Returns(newEmoteData);
        
        var oldEmote = new Emote(oldEmoteId, emoteKey, false);
        var newEmote = new Emote(newEmoteId, emoteKey, false);
        
        discordClient.GetApplicationEmotesAsync().Returns(Task.FromResult<IReadOnlyCollection<Emote>>([oldEmote]));
        discordClient.DeleteApplicationEmoteAsync(oldEmoteId).Returns(Task.CompletedTask);
        discordClient.CreateApplicationEmoteAsync(emoteKey, Arg.Any<Image>()).Returns(Task.FromResult(newEmote));
        
        var resolverService = new DiscordEmoteResolverService(discordClient, internalEmoteSource);
        var service = new ReflectionBasedBotEmoteManagerService<SingleEmoteSpec, SingleEmoteModel>(resolverService);

        var spec = new SingleEmoteSpec
        {
            Emote = new InternalCustomEmoteSpecification(emoteKey)
        };
        
        var tracking = new List<InternalCustomEmoteTracking>
        {
            new()
            {
                EmoteKey = emoteKey,
                EmoteId = oldEmoteId,
                EmoteDataIdentifier = oldEmoteData,
                IsAnimated = oldEmote.Animated
            }
        };
        
        // act
        await service.InitializeAsync(spec, tracking);
        
        // assert
        await discordClient.Received(1).DeleteApplicationEmoteAsync(oldEmoteId);
        await discordClient.Received(1).CreateApplicationEmoteAsync(emoteKey, Arg.Any<Image>());
        tracking.Should().ContainSingle(x => x.EmoteId == newEmoteId && x.EmoteDataIdentifier.SequenceEqual(newEmoteData));
    }

    [Fact]
    public async Task InitializeAsync_DuplicateInternalEmoteKeys_ResolvesToSameEmote()
    {
        // arrange
        var discordClient = Substitute.For<IDiscordClient>();
        var internalEmoteSource = Substitute.For<IInternalEmoteSource>();
        
        const ulong emoteId = 123ul;
        const string emoteKey = "okay";
        byte[] emoteData = [0xDE, 0xAD, 0xBE, 0xEF];
        
        internalEmoteSource.GetAvailableEmoteKeys().Returns([emoteKey]);
        internalEmoteSource.GetEmoteDataStream(emoteKey).Returns(_ => new MemoryStream(emoteData));
        internalEmoteSource.GetEmoteDataIdentifierAsync(emoteKey).Returns(Task.FromResult(emoteData));

        var spec = new MultipleEmotesSpec()
        {
            Emote1 = new InternalCustomEmoteSpecification(emoteKey),
            Emote2 = new InternalCustomEmoteSpecification(emoteKey),
        };
        
        var emote = new Emote(emoteId, emoteKey, false);
        
        discordClient.GetApplicationEmotesAsync().Returns(Task.FromResult<IReadOnlyCollection<Emote>>([]));
        discordClient.CreateApplicationEmoteAsync(emoteKey, Arg.Any<Image>()).Returns(Task.FromResult(emote));
        
        var resolverService = new DiscordEmoteResolverService(discordClient, internalEmoteSource);
        var service = new ReflectionBasedBotEmoteManagerService<MultipleEmotesSpec, MultipleEmotesModel>(resolverService);
        
        var tracking = new List<InternalCustomEmoteTracking>();
        
        // act
        await service.InitializeAsync(spec, tracking);
        
        // assert
        await discordClient.Received(1).CreateApplicationEmoteAsync(emoteKey, Arg.Any<Image>());
        tracking.Should().ContainSingle(e =>
            e.EmoteKey == emoteKey &&
            e.EmoteId == emoteId &&
            e.EmoteDataIdentifier.SequenceEqual(emoteData));

        service.Emotes.Emote1.Should().Be(emote);
        service.Emotes.Emote2.Should().Be(emote);
    }
}