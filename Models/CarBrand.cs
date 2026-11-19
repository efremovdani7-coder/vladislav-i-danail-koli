using System.ComponentModel.DataAnnotations;

namespace CarReliability.Models;

public class CarBrand
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<CarEntry> CarEntries { get; set; } = new List<CarEntry>();
}
