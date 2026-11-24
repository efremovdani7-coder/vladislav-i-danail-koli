using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarReliability.Models;

/// <summary>Отзив на потребител за конкретен автомобил (US01).
/// Марка/модел/поколение/двигател се избират от каталога, останалото се въвежда ръчно.</summary>
public class CarEntry
{
    public int Id { get; set; }

    // Попълват се автоматично от избрания двигател (за по-бързи справки)
    public int CarModelId { get; set; }
    public CarModel? CarModel { get; set; }

    public int CarGenerationId { get; set; }
    public CarGeneration? CarGeneration { get; set; }

    [Required(ErrorMessage = "Изберете двигател и скоростна кутия.")]
    [Display(Name = "Двигател")]
    public int CarEngineId { get; set; }
    public CarEngine? CarEngine { get; set; }

    [Required(ErrorMessage = "Изберете година на производство.")]
    [Range(1950, 2030, ErrorMessage = "Невалидна година.")]
    [Display(Name = "Година на производство")]
    public int Year { get; set; }

    [Required(ErrorMessage = "Пробегът е задължителен.")]
    [Range(0, 2000000, ErrorMessage = "Пробегът трябва да е между 0 и 2 000 000 км.")]
    [Display(Name = "Пробег (км)")]
    public int Mileage { get; set; }

    [Range(0, 1000000, ErrorMessage = "Разходите трябва да са между 0 и 1 000 000 €.")]
    [Display(Name = "Годишни разходи за поддръжка (€)")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaintenanceCost { get; set; }

    [Required(ErrorMessage = "Оценката е задължителна.")]
    [Range(1, 10, ErrorMessage = "Оценката трябва да е от 1 до 10.")]
    [Display(Name = "Вашата оценка за надеждност (1–10)")]
    public int ReliabilityRating { get; set; }

    [Display(Name = "Възникнали дефекти")]
    public DefectCategory Defects { get; set; }

    [StringLength(2000)]
    [Display(Name = "Описание на дефектите и ремонтите")]
    public string? CommonIssues { get; set; }

    [StringLength(2000)]
    [Display(Name = "Предимства")]
    public string? Pros { get; set; }

    [StringLength(2000)]
    [Display(Name = "Недостатъци")]
    public string? Cons { get; set; }

    [StringLength(5000)]
    [Display(Name = "Общ коментар")]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
}
