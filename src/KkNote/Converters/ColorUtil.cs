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
        return From(shaded);
    }

    public static Color Mix(Color a, Color b, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * amount),
            (byte)(a.G + (b.G - a.G) * amount),
            (byte)(a.B + (b.B - a.B) * amount));
    }

    public static Color Shift(Color color, double amount)
    {
        amount = Math.Clamp(amount, -1, 1);
        return amount >= 0 ? Mix(color, Colors.White, amount) : Mix(color, Colors.Black, -amount);
    }

    public static SolidColorBrush ShiftBrush(string? hex, double amount, string fallback = "#F6F0E4")
    {
        if (!TryParse(hex, out var color) && !TryParse(fallback, out color))
            color = Color.FromRgb(0xF6, 0xF0, 0xE4);
        return From(Shift(color, amount));
    }

    public static SolidColorBrush From(Color color)
    {
        var brush = new SolidColorBrush(color);
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
