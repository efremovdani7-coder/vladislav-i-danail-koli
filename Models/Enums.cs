using System.ComponentModel.DataAnnotations;

namespace CarReliability.Models;

public enum FuelType
{
    [Display(Name = "Бензин")] Petrol,
    [Display(Name = "Дизел")] Diesel,
    [Display(Name = "Хибрид")] Hybrid,
    [Display(Name = "Plug-in хибрид")] PlugInHybrid,
    [Display(Name = "Електрически")] Electric,
    [Display(Name = "Газ (LPG)")] LPG,
    [Display(Name = "Метан (CNG)")] CNG
}

public enum BodyType
{
    [Display(Name = "Седан")] Sedan,
    [Display(Name = "Хечбек")] Hatchback,
    [Display(Name = "Комби")] Wagon,
    [Display(Name = "SUV / джип")] Suv,
    [Display(Name = "Купе")] Coupe,
    [Display(Name = "Кабрио")] Convertible,
    [Display(Name = "Ван / миниван")] Van,
    [Display(Name = "Пикап")] Pickup
}

/// <summary>Категории дефекти (може да се изберат няколко) — използват се в кръговата графика (US04).</summary>
[Flags]
public enum DefectCategory
{
    None = 0,
    [Display(Name = "Двигател")] Engine = 1,
    [Display(Name = "Скоростна кутия")] Transmission = 2,
    [Display(Name = "Ходова част")] Suspension = 4,
    [Display(Name = "Електроника")] Electronics = 8,
    [Display(Name = "Спирачки")] Brakes = 16,
    [Display(Name = "Купе и корозия")] Body = 32,
    [Display(Name = "Климатик и отопление")] Climate = 64,
    [Display(Name = "Горивна система")] FuelSystem = 128,
    [Display(Name = "Друго")] Other = 256
}

public static class EnumText
{
    public static readonly (DefectCategory Value, string Name)[] Defects =
    {
        (DefectCategory.Engine, "Двигател"),
        (DefectCategory.Transmission, "Скоростна кутия"),
        (DefectCategory.Suspension, "Ходова част"),
        (DefectCategory.Electronics, "Електроника"),
        (DefectCategory.Brakes, "Спирачки"),
        (DefectCategory.Body, "Купе и корозия"),
        (DefectCategory.Climate, "Климатик и отопление"),
        (DefectCategory.FuelSystem, "Горивна система"),
        (DefectCategory.Other, "Друго")
    };

    public static string Fuel(FuelType fuel) => fuel switch
    {
        FuelType.Petrol => "Бензин",
        FuelType.Diesel => "Дизел",
        FuelType.Hybrid => "Хибрид",
        FuelType.PlugInHybrid => "Plug-in хибрид",
        FuelType.Electric => "Електрически",
        FuelType.LPG => "Газ (LPG)",
        FuelType.CNG => "Метан (CNG)",
        _ => fuel.ToString()
    };

    public static string Body(BodyType body) => body switch
    {
        BodyType.Sedan => "Седан",
        BodyType.Hatchback => "Хечбек",
        BodyType.Wagon => "Комби",
        BodyType.Suv => "SUV / джип",
        BodyType.Coupe => "Купе",
        BodyType.Convertible => "Кабрио",
        BodyType.Van => "Ван / миниван",
        BodyType.Pickup => "Пикап",
        _ => body.ToString()
    };

    public static IEnumerable<string> DefectNames(DefectCategory defects) =>
        Defects.Where(d => defects.HasFlag(d.Value)).Select(d => d.Name);

    public static int DefectCount(DefectCategory defects) =>
        Defects.Count(d => defects.HasFlag(d.Value));
}
