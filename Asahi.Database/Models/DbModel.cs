using System.ComponentModel.DataAnnotations;

namespace Asahi.Database.Models;

public abstract class DbModel
{
    [Key]
    public uint Id { get; set; }
}
