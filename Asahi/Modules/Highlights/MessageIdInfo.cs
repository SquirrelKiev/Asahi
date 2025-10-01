namespace Asahi.Modules.Highlights
{
    public readonly record struct MessageIdInfo(ulong GuildId, ulong ChannelId, ulong MessageId)
    {
        public bool Equals(MessageIdInfo? other)
        {
            return other.HasValue && other.Value.MessageId == MessageId;
        }
    };
}
