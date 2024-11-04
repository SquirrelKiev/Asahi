using System.ComponentModel.DataAnnotations;

namespace Asahi.Database.Models
{
    public class CachedMessageReaction
    {
        [MaxLength(64)]
        public required string Emote { get; set; }
        
        public required int Count { get; set; }
        
        public CachedHighlightedMessage HighlightedMessage { get; set; }
    }
}
