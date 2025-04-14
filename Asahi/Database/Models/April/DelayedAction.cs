namespace Asahi.Database.Models.April;

public class DelayedAction : DbModel
{
    public required string ActionJson { get; set; }
    public required DateTimeOffset WhenToExecute { get; set; }

    public required ulong GuildId { get; set; }
    public required ulong ChannelId { get; set; }
    public required ulong UserId { get; set; }
}
