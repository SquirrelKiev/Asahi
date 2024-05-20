using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NodaTime;

namespace Asahi.Database.Models;

public class BirthdayEntry
{
    public required ulong UserId { get; set; }

    [NotMapped]
    public required AnnualDate BirthDayDate
    {
        get => new(Month, Day);
        set
        {
            Day = value.Day;
            Month = value.Month;
        }
    }


    public int Day { get; set; }
    public int Month { get; set; }

    /// <summary>
    /// The user's timezone in TZDB format.
    /// </summary>
    [MaxLength(32)]
    public required string TimeZone { get; set; }

    /// <summary>
    /// The time the entry was created, in UTC. Used for abuse prevention.
    /// </summary>
    public required LocalDateTime TimeCreatedUtc { get; set; }

    public required BirthdayConfig BirthdayConfig { get; set; }
}