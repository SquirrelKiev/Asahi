using System.ComponentModel.DataAnnotations.Schema;

namespace Asahi.Database.Models;

public class TrackedUser : DbModel
{
    public virtual uint TrackableId { get; set; }
    [ForeignKey(nameof(TrackableId))]
    public required Trackable Trackable { get; set; }

    public ulong UserId { get; set; }
}