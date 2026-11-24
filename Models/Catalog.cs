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

    public ICollection<CarEngine> Engines { get; set; } = new List<CarEngine>();

    public string DisplayName => YearFrom == YearTo ? $"{Name} ({YearFrom})" : $"{Name} ({YearFrom}–{YearTo})";
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
}
