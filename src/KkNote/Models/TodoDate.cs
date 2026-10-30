using System.Globalization;

namespace KkNote.Models;

public enum TodoTone
{
    None,
    Soon,
    Overdue,
}

public static class TodoDate
{
    public const int DefaultSoonDays = 3;
    public const int MinSoonDays = 1;
    public const int MaxSoonDays = 14;

    public static int ClampSoonDays(int days)
        => Math.Clamp(days, MinSoonDays, MaxSoonDays);

    public static string NormalizeFormat(string? format)
    {
        if (string.Equals(format, DateFormats.Ymd, StringComparison.OrdinalIgnoreCase))
            return DateFormats.Ymd;
        if (string.Equals(format, DateFormats.YyMd, StringComparison.OrdinalIgnoreCase))
            return DateFormats.YyMd;
        return DateFormats.Md;
    }

    public static string Format(DateTime? due, string? format)
    {
        if (due == null)
            return "";
        var date = due.Value.Date;
        var culture = CultureInfo.InvariantCulture;
        return NormalizeFormat(format) switch
        {
            DateFormats.Ymd => date.ToString("yyyy/MM/dd", culture),
            DateFormats.YyMd => date.ToString("yy/MM/dd", culture),
            _ => date.ToString("M/d", culture),
        };
    }

    public static TodoTone Tone(DateTime? due, bool done, int soonDays, DateTime today)
    {
        if (done || due == null)
            return TodoTone.None;
        var day = due.Value.Date;
        var start = today.Date;
        if (day <= start)
            return TodoTone.Overdue;
        var horizon = start.AddDays(ClampSoonDays(soonDays));
        if (day <= horizon)
            return TodoTone.Soon;
        return TodoTone.None;
    }
}
