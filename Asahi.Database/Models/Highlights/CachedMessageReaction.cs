using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asahi.Database.Models
{
    public class CachedMessageReaction
    {
        [MaxLength(32)]
        public required string EmoteName { get; set; }

        public ulong EmoteId { get; set; } = 0;
        
        public required bool IsAnimated { get; set; }
        
        public required int Count { get; set; }
        
        public uint HighlightedMessageId { get; set; }

        [ForeignKey(nameof(HighlightedMessageId))]
        public CachedHighlightedMessage HighlightedMessage { get; set; } = null!;
    }
}
