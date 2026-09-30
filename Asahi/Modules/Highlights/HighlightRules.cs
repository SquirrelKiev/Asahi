using Asahi.Database.Models;

namespace Asahi.Modules.Highlights
{
    public class HighlightRules
    {
        public readonly record struct HighlightCandidate(
            ulong ChannelId,
            ulong ParentChannelId,
            IReadOnlyCollection<ulong> AuthorRoleIds,
            TimeSpan Age,
            bool IsChannelLocked);
        
        public static bool IsEligible(HighlightBoard board, HighlightCandidate candidate, bool isForced)
        {
            if (isForced)
                return true;
            
            if(board.HighlightsMuteRole != 0 && candidate.AuthorRoleIds.Contains(board.HighlightsMuteRole))
                return false;
            
            if(board.MaxMessageAgeSeconds != 0 && candidate.Age > TimeSpan.FromSeconds(board.MaxMessageAgeSeconds))
                return false;
            
            if(board.IgnoreLockedChannels && candidate.IsChannelLocked)
                return false;

            var inFilter = board.FilteredChannels.Contains(candidate.ChannelId) || board.FilteredChannels.Contains(candidate.ParentChannelId);
            
            return board.FilteredChannelsIsBlockList ? !inFilter : inFilter;
        }
    }
}
