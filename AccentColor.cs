using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace SircleToSearch;

public static class AccentColor
{
    public static readonly (string Hex, string NameEn, string NameRu)[] Presets =
    [
        ("#009FAA", "Teal", "Тил"),
        ("#3E7BFA", "Blue", "Синий"),
        ("#7A5CFA", "Purple", "Фиолетовый"),
        ("#E0529C", "Pink", "Розовый"),
        ("#E8734A", "Orange", "Оранжевый"),
        ("#4CAF6D", "Green", "Зелёный"),
        ("#E5484D", "Red", "Красный"),
        ("#6B7280", "Graphite", "Графит"),
    ];

    public static Color Parse(string? hex)
    {
        if (!TryParse(hex, out var color))
            return (Color)ColorConverter.ConvertFromString("#009FAA")!;
        return color;
    }

    public static bool TryParse(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        var value = hex.Trim();
        if (!value.StartsWith('#')) value = "#" + value;
        if (value.Length != 7) return false;

        try
        {
            var converted = ColorConverter.ConvertFromString(value);
            if (converted is not Color c) return false;
            color = c;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
