using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarReliability.Models;

public class CarEntry
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Марката е задължителна.")]
    [Display(Name = "Марка")]
    public int CarBrandId { get; set; }

    [ForeignKey("CarBrandId")]
    public CarBrand? CarBrand { get; set; }

    [Required(ErrorMessage = "Моделът е задължителен.")]
    [StringLength(100)]
    [Display(Name = "Модел")]
    public string Model { get; set; } = string.Empty;

    [Required(ErrorMessage = "Годината е задължителна.")]
    [Range(1900, 2030, ErrorMessage = "Годината трябва да е между 1900 и 2030.")]
    [Display(Name = "Година на производство")]
    public int Year { get; set; }

    [Required(ErrorMessage = "Пробегът е задължителен.")]
    [Range(0, 2000000, ErrorMessage = "Невалиден пробег.")]
    [Display(Name = "Пробег (км)")]
    public int Mileage { get; set; }

    [Required(ErrorMessage = "Типът двигател е задължителен.")]
    [Display(Name = "Тип двигател")]
    public EngineType EngineType { get; set; }

    [Range(0.1, 20.0, ErrorMessage = "Обемът на двигателя трябва да е между 0.1 и 20.0 литра.")]
    [Display(Name = "Обем на двигателя (л)")]
    public double? EngineDisplacement { get; set; }

    [Display(Name = "Скоростна кутия")]
    public TransmissionType Transmission { get; set; }

    [Required(ErrorMessage = "Оценката за надеждност е задължителна.")]
    [Range(1, 10, ErrorMessage = "Оценката трябва да е от 1 до 10.")]
    [Display(Name = "Оценка за надеждност (1-10)")]
    public int ReliabilityRating { get; set; }

    [Range(0, 1000000, ErrorMessage = "Разходите трябва да са между 0 и 1 000 000 €.")]
    [Display(Name = "Годишни разходи за поддръжка (€)")]
    public decimal? MaintenanceCost { get; set; }

    [Display(Name = "Чести проблеми")]
    [StringLength(2000)]
    public string? CommonIssues { get; set; }

    [Display(Name = "Предимства")]
    [StringLength(2000)]
    public string? Pros { get; set; }

    [Display(Name = "Недостатъци")]
    [StringLength(2000)]
    public string? Cons { get; set; }

    [Display(Name = "Общ коментар")]
    [StringLength(5000)]
    public string? Comment { get; set; }

    [Display(Name = "Дата на добавяне")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Последна редакция")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser? User { get; set; }
}

public enum EngineType
{
    [Display(Name = "Бензин")]
    Petrol,
    [Display(Name = "Дизел")]
    Diesel,
    [Display(Name = "Хибрид")]
    Hybrid,
    [Display(Name = "Електрически")]
    Electric,
    [Display(Name = "Газ/LPG")]
    LPG,
    [Display(Name = "Plug-in Хибрид")]
    PlugInHybrid
}

public enum TransmissionType
{
    [Display(Name = "Ръчна")]
    Manual,
    [Display(Name = "Автоматична")]
    Automatic,
    [Display(Name = "Роботизирана")]
    Automated,
    [Display(Name = "CVT (безстепенна)")]
    CVT
}
