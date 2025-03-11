using System.Diagnostics.Contracts;

namespace Asahi.Modules.FeedsV2
{
    public interface IFeedProviderFactory
    {
        /// <summary>
        /// Returns the best feed provider for the source specified. Will return null if no good sources found.
        /// </summary>
        /// <param name="feedSource">The source. Most likely a URL.</param>
        /// <returns>The feed provider.</returns>
        [Pure]
        public IFeedProvider? GetFeedProvider(string feedSource);
    }
}
