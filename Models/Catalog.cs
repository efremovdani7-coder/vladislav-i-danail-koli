using System.ComponentModel.DataAnnotations;

namespace CarReliability.Models;

// Каталог: Марка → Модел → Поколение → Двигател (+ скоростна кутия и задвижване).
// Данните се зареждат от Data/Seed/car-catalog.json (източник: autoevolution.com).

public class CarBrand
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Марка")]
    public string Name { get; set; } = string.Empty;

    public ICollection<CarModel> Models { get; set; } = new List<CarModel>();
}

public class CarModel
{
    public int Id { get; set; }

    public int CarBrandId { get; set; }
    public CarBrand? CarBrand { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Модел")]
    public string Name { get; set; } = string.Empty;

    public ICollection<CarGeneration> Generations { get; set; } = new List<CarGeneration>();

    public string FullName => $"{CarBrand?.Name} {Name}".Trim();
}

public class CarGeneration
{
    public int Id { get; set; }

    public int CarModelId { get; set; }
    public CarModel? CarModel { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Поколение / версия")]
    public string Name { get; set; } = string.Empty;

    public int YearFrom { get; set; }
    public int YearTo { get; set; }

    [Display(Name = "Купе")]
    public BodyType BodyType { get; set; }

    // Размери (за сравнението на размерите)
    public int? LengthMm { get; set; }
    public int? WidthMm { get; set; }
    public int? HeightMm { get; set; }
    public int? WheelbaseMm { get; set; }
    public int? GroundClearanceMm { get; set; }
    public int? CargoLiters { get; set; }
    public int? WeightKg { get; set; }

    /// <summary>true = размерите са взети от съседно поколение на същия модел.</summary>
    public bool SizesApproximate { get; set; }

    /// <summary>Откъде са данните: "autoevolution" или "EPA" (FuelEconomy.gov, САЩ).</summary>
    [StringLength(30)]
    public string Source { get; set; } = CatalogSources.AutoEvolution;

    public ICollection<CarEngine> Engines { get; set; } = new List<CarEngine>();

    public string Years => YearFrom == YearTo ? $"{YearFrom}" : $"{YearFrom}–{YearTo}";
    public string DisplayName => $"{Name} ({Years})" + (Source == CatalogSources.Epa ? " · САЩ" : "");
    public bool HasSizes => LengthMm.HasValue && WidthMm.HasValue && HeightMm.HasValue;
}

/// <summary>Един ред = двигател + скоростна кутия + задвижване (вариант от каталога).</summary>
public class CarEngine
{
    public int Id { get; set; }

    public int CarGenerationId { get; set; }
    public CarGeneration? CarGeneration { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Двигател")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Гориво")]
    public FuelType FuelType { get; set; }

    [Display(Name = "Работен обем (куб. см)")]
    public int? DisplacementCc { get; set; }

    [Display(Name = "Мощност (к.с.)")]
    public int? PowerHp { get; set; }

    [Display(Name = "Въртящ момент (Nm)")]
    public int? TorqueNm { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Скоростна кутия")]
    public string Transmission { get; set; } = string.Empty;

    [Required, StringLength(50)]
    [Display(Name = "Задвижване")]
    public string DriveType { get; set; } = string.Empty;

    /// <summary>Години, в които е предлаган този двигател (null = през цялото поколение).</summary>
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
}

public static class CatalogSources
{
    public const string AutoEvolution = "autoevolution";
    public const string Epa = "EPA";
}

/// <summary>Дневник на импортите от публичните бази данни.</summary>
public class CatalogImport
{
    public int Id { get; set; }

    [StringLength(100)]
    public string Source { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "Работи";

    [StringLength(1000)]
    public string? Message { get; set; }

    public int BrandsAdded { get; set; }
    public int ModelsAdded { get; set; }
    public int GenerationsAdded { get; set; }
    public int EnginesAdded { get; set; }
}
