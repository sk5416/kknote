using System.Globalization;
using System.Windows.Media;

namespace KkNote.Converters;

public static class ColorUtil
{
    public static bool TryParse(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
            return false;
        var text = hex.Trim();
        if (text.StartsWith('#'))
            text = text[1..];
        if (text.Length != 6)
            return false;
        if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return false;
        color = Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        return true;
    }

    public static SolidColorBrush Brush(string? hex, string fallback = "#E7EEF9")
    {
        if (!TryParse(hex, out var color) && !TryParse(fallback, out color))
            color = Colors.White;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static SolidColorBrush Shade(string? hex, double factor)
    {
        if (!TryParse(hex, out var color))
            color = Color.FromRgb(0x15, 0x20, 0x33);
        factor = Math.Clamp(factor, 0, 1);
        var shaded = Color.FromRgb(
            (byte)(color.R * factor),
            (byte)(color.G * factor),
            (byte)(color.B * factor));
        var brush = new SolidColorBrush(shaded);
        brush.Freeze();
        return brush;
    }

    public static bool IsLight(string? hex)
    {
        if (!TryParse(hex, out var color))
            return false;
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255d;
        return luminance > 0.72;
    }
}
