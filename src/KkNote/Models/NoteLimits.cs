namespace KkNote.Models;

public static class NoteLimits
{
    public static readonly double[] FontSizes = [12, 13, 14, 16, 18, 20, 24, 28];
    public const double MinWidth = 240;
    public const double MinHeight = 210;
    public const double DefaultWidth = 320;
    public const double DefaultHeight = 380;
    public const double MinOpacity = 0.2;
    public const double MaxOpacity = 1.0;
    public const double DefaultOpacity = 0.9;
    public const double CollapsedHeight = 42;

    public static double ClampOpacity(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            return DefaultOpacity;
        return Math.Clamp(value, MinOpacity, MaxOpacity);
    }

    public static double SnapFontSize(double size)
    {
        if (double.IsNaN(size) || double.IsInfinity(size))
            return 14;
        var best = FontSizes[0];
        var bestDistance = double.MaxValue;
        foreach (var candidate in FontSizes)
        {
            var distance = Math.Abs(candidate - size);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }
        return best;
    }
}
