using System.Text;

namespace CarReliability.Services;

/// <summary>US16: генерира линкове за търсене на обяви за избраната марка и модел.</summary>
public static class MarketplaceLinks
{
    public static string MobileBg(string brand, string model) =>
        $"https://www.mobile.bg/obiavi/avtomobili-dzhipove/{Slug(brand)}/{Slug(ModelForMobile(model))}";

    public static string MobileBgBrand(string brand) =>
        $"https://www.mobile.bg/obiavi/avtomobili-dzhipove/{Slug(brand)}";

    // Cars.bg използва вътрешни номера за марките, затова търсим в сайта чрез Google
    public static string CarsBg(string brand, string model) =>
        "https://www.google.com/search?q=" + Uri.EscapeDataString($"site:cars.bg {brand} {model}");

    public static string Olx(string brand, string model) =>
        $"https://www.olx.bg/ads/q-{Uri.EscapeDataString((brand + " " + model).ToLowerInvariant().Replace(' ', '-'))}/";

    private static string ModelForMobile(string model) => model
        .Replace(" Series", " seriya")
        .Replace("-Class", "-klasa");

    private static string Slug(string value)
    {
        var sb = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }
}
