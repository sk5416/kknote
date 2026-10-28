namespace KkNote.Models;

public static class NoteLimits
{
    public static readonly double[] FontSizes = [12, 13, 14, 16, 18, 20, 24, 28];
    public const double MinWidth = 240;
    public const double MinHeight = 210;
    public const double DefaultWidth = 320;
    public const double DefaultHeight = 380;

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
