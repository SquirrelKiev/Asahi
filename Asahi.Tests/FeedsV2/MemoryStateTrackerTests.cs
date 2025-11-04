using System.Security.Cryptography;
using Asahi.Modules.FeedsV2;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Asahi.Tests.FeedsV2;

public class MemoryStateTrackerTests
{
    private const string FeedSource = "foobar";
    private const int ArticleId = 123;
    private const ulong ChannelId = 456;
    
    private static MemoryStateTracker CreateNewTracker()
    {
        return new MemoryStateTracker(NullLogger<MemoryStateTracker>.Instance);
    }
    
    [Fact]
    public void IsFirstTimeSeeingSource_SourceHasMarkedArticle_False()
    {
        // arrange
        var stateTracker = new MemoryStateTracker(NullLogger<MemoryStateTracker>.Instance);
        
        stateTracker.MarkArticleAsRead(FeedSource, ArticleId);
        
        // act
        var result = stateTracker.IsFirstTimeSeeingFeedSource(FeedSource);
        
        // assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void IsFirstTimeSeeingSource_SourceIsUnseen_True()
    {
        // arrange
        var stateTracker = CreateNewTracker();
        
        // act
        var result = stateTracker.IsFirstTimeSeeingFeedSource(FeedSource);
        
        // assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNewArticleFeedSource_ArticleIsSeen_False()
    {
        // arrange
        var stateTracker = CreateNewTracker();

        stateTracker.MarkArticleAsRead(FeedSource, ArticleId);
        
        //act
        var result = stateTracker.IsNewArticle(FeedSource, ArticleId);
        
        // assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void IsNewArticleFeedSource_ArticleAndFeedIsNew_True()
    {
        // arrange
        var stateTracker = CreateNewTracker();
        
        //act
        var result = stateTracker.IsNewArticle(FeedSource, ArticleId);
        
        // assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void IsNewArticleFeedSource_ArticleIsNewFeedIsSeen_True()
    {
        // arrange
        var stateTracker = CreateNewTracker();
        stateTracker.MarkArticleAsRead(FeedSource, 789);
        
        // act
        var result = stateTracker.IsNewArticle(FeedSource, ArticleId);
        
        // assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void IsNewArticleChannelId_ArticleIsSeenForChannel_False()
    {
        // arrange
        var stateTracker = CreateNewTracker();

        stateTracker.MarkArticleAsRead(ChannelId, ArticleId);
        
        //act
        var result = stateTracker.IsNewArticle(ChannelId, ArticleId);
        
        // assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void IsNewArticleChannelId_ArticleAndFeedIsNew_True()
    {
        // arrange
        var stateTracker = CreateNewTracker();
        
        //act
        var result = stateTracker.IsNewArticle(ChannelId, ArticleId);
        
        // assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNewArticleChannelId_ArticleIsNewFeedIsSeen_True()
    {
        // arrange
        var stateTracker = CreateNewTracker();
        stateTracker.MarkArticleAsRead(ChannelId, 789);
        
        // act
        var result = stateTracker.IsNewArticle(ChannelId, ArticleId);
        
        // assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetCachedDefaultFeedTitle_NotSet_Null()
    {
        // arrange
        var stateTracker = CreateNewTracker();
        
        // act
        var result = stateTracker.GetCachedDefaultFeedTitle(FeedSource);
        
        // assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetBestDefaultFeedTitle_NotSet_RawFeedSource()
    {
        // arrange
        var stateTracker = CreateNewTracker();
        
        // act
        var result = stateTracker.GetBestDefaultFeedTitle(FeedSource);
        
        // assert
        result.Should().Be(FeedSource);
    }

    [Fact]
    public void UpdateDefaultFeedTitleCache_StoringTitle_TitleShouldBeStored()
    {
        // arrange
        var stateTracker = CreateNewTracker();
        
        // act
        const string title = "some title";
        stateTracker.UpdateDefaultFeedTitleCache(FeedSource, title);
        
        // assert
        stateTracker.GetCachedDefaultFeedTitle(FeedSource).Should().Be(title);
        stateTracker.GetBestDefaultFeedTitle(FeedSource).Should().Be(title);
    }

    [Fact]
    public void GetFeedContinuationToken_NotSet_Null()
    {
        // arrange
        var stateTracker = CreateNewTracker();
        
        // act
        var result = stateTracker.GetFeedSourceContinuationToken(FeedSource);
        
        // assert
        result.Should().BeNull();
    }

    [Fact]
    public void SetFeedSourceContinuationToken_SetToken_TokenIsStored()
    {
        // arrange
        var stateTracker = CreateNewTracker();

        var token = new { Id = 123 };
        
        // act
        stateTracker.SetFeedSourceContinuationToken(FeedSource, token);
        
        // assert
        stateTracker.GetFeedSourceContinuationToken(FeedSource).Should().Be(token);
    }
    
    [Fact]
    public void SetFeedSourceContinuationToken_ClearToken_TokenIsCleared()
    {
        // arrange
        var stateTracker = CreateNewTracker();

        var token = new { Id = 123 };
        stateTracker.SetFeedSourceContinuationToken(FeedSource, token);
        
        // act
        stateTracker.SetFeedSourceContinuationToken(FeedSource, null);
        
        // assert
        stateTracker.GetFeedSourceContinuationToken(FeedSource).Should().BeNull();
    }

    [Fact]
    public void PruneMissingArticles_MissingArticlesInNewState_RemovesArticles()
    {
        // arrange
        var stateTracker = CreateNewTracker();

        const int articleToBePruned = 100;
        const int articleToStay = 200;
        stateTracker.MarkArticleAsRead(FeedSource, articleToBePruned);
        stateTracker.MarkArticleAsRead(FeedSource, articleToStay);
        
        var feedProvider = Substitute.For<IFeedProvider>();
        feedProvider.FeedSource.Returns(FeedSource);
        feedProvider.ListArticleIds().Returns([articleToStay]);
        
        // act
        stateTracker.PruneMissingArticles(feedProvider);
        
        // assert
        stateTracker.IsNewArticle(FeedSource, articleToStay).Should().BeFalse();
        stateTracker.IsNewArticle(FeedSource, articleToBePruned).Should().BeTrue();
    }

    [Fact]
    public void PruneMissingFeeds_MissingFeedsInNewState_RemovesFeeds()
    {
        // arrange
        var stateTracker = CreateNewTracker();

        const string feedToBePruned = FeedSource;
        const string feedToStay = "feed-to-stay";
        
        stateTracker.MarkArticleAsRead(feedToBePruned, ArticleId);
        stateTracker.MarkArticleAsRead(feedToStay, ArticleId);
        
        // act
        stateTracker.PruneMissingFeeds([feedToStay]);
        
        // assert
        stateTracker.IsFirstTimeSeeingFeedSource(feedToBePruned).Should().BeTrue();
        stateTracker.IsFirstTimeSeeingFeedSource(feedToStay).Should().BeFalse();
        
    }

    [Fact]
    public void ClearChannelArticleList_WhenCalled_ClearsArticleList()
    {
        // arrange
        var stateTracker = CreateNewTracker();
        
        stateTracker.MarkArticleAsRead(ChannelId, ArticleId);
        
        // act
        stateTracker.ClearChannelArticleList();
        
        // assert
        stateTracker.IsNewArticle(ChannelId, ArticleId).Should().BeTrue();
    }
}