using System.ComponentModel.DataAnnotations;
using CarReliability.Models;
using CarReliability.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CarReliability.ViewModels;

/// <summary>Форма за добавяне/редакция на отзив. Каталожните полета са падащи менюта,
/// всяко зависещо от предишното: Марка → Модел → Поколение → Година → Двигател → Скоростна кутия.</summary>
public class CarEntryFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Изберете марка.")]
    [Display(Name = "Марка")]
    public int? BrandId { get; set; }

    [Required(ErrorMessage = "Изберете модел.")]
    [Display(Name = "Модел")]
    public int? ModelId { get; set; }

    [Required(ErrorMessage = "Изберете поколение.")]
    [Display(Name = "Поколение / версия")]
    public int? GenerationId { get; set; }

    [Required(ErrorMessage = "Изберете година.")]
    [Range(1950, 2030, ErrorMessage = "Изберете година.")]
    [Display(Name = "Година на производство")]
    public int? Year { get; set; }

    [Required(ErrorMessage = "Изберете двигател.")]
    [Display(Name = "Двигател")]
    public string EngineName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Изберете скоростна кутия.")]
    [Display(Name = "Скоростна кутия и задвижване")]
    public int? CarEngineId { get; set; }

    [Required(ErrorMessage = "Пробегът е задължителен.")]
    [Range(0, 2000000, ErrorMessage = "Пробегът трябва да е между 0 и 2 000 000 км.")]
    [Display(Name = "Пробег (км)")]
    public int? Mileage { get; set; }

    [Range(0, 1000000, ErrorMessage = "Разходите трябва да са между 0 и 1 000 000 €.")]
    [Display(Name = "Годишни разходи за поддръжка (€)")]
    public decimal? MaintenanceCost { get; set; }

    [Required(ErrorMessage = "Оценката е задължителна.")]
    [Range(1, 10, ErrorMessage = "Оценката трябва да е от 1 до 10.")]
    [Display(Name = "Вашата оценка за надеждност (1–10)")]
    public int? ReliabilityRating { get; set; }

    [Display(Name = "Възникнали дефекти")]
    public List<int> SelectedDefects { get; set; } = new();

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

    // Опции за падащите менюта (попълват се от сървъра при редакция/грешка, после от JavaScript)
    public List<SelectListItem> Brands { get; set; } = new();
    public List<SelectListItem> Models { get; set; } = new();
    public List<SelectListItem> Generations { get; set; } = new();
    public List<SelectListItem> Years { get; set; } = new();
    public List<SelectListItem> Engines { get; set; } = new();
    public List<SelectListItem> Variants { get; set; } = new();
}

public class CarSearchViewModel
{
    public int? BrandId { get; set; }
    public int? ModelId { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public FuelType? Fuel { get; set; }
    public int? MinRating { get; set; }
    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 15;
    public int TotalResults { get; set; }
    public int TotalPages { get; set; }

    public List<CarEntry> Results { get; set; } = new();
    public List<SelectListItem> Brands { get; set; } = new();
    public List<SelectListItem> Models { get; set; } = new();

    /// <summary>Обобщение за избраната марка или модел.</summary>
    public string? SummaryTitle { get; set; }
    public ReliabilityStats? Summary { get; set; }
    public int? SummaryModelId { get; set; }
    public int? SummaryBrandId { get; set; }
}

public class CarListViewModel
{
    public List<CarEntry> Entries { get; set; } = new();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}
