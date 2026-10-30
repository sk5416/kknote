using KkNote.Models;
using Xunit;

namespace KkNote.Tests;

public sealed class TodoDateTests
{
    private static readonly DateTime Today = new(2026, 9, 18);

    [Theory]
    [InlineData(null, false, TodoTone.None)]
    [InlineData("2026-09-17", false, TodoTone.Overdue)]
    [InlineData("2026-09-18", false, TodoTone.Overdue)]
    [InlineData("2026-09-19", false, TodoTone.Soon)]
    [InlineData("2026-09-21", false, TodoTone.Soon)]
    [InlineData("2026-09-22", false, TodoTone.None)]
    [InlineData("2026-09-18", true, TodoTone.None)]
    [InlineData("2026-09-19", true, TodoTone.None)]
    public void Tone_UsesInclusiveTodayAsOverdue(string? dueText, bool done, TodoTone expected)
    {
        DateTime? due = dueText == null ? null : DateTime.Parse(dueText);
        Assert.Equal(expected, TodoDate.Tone(due, done, 3, Today));
    }

    [Fact]
    public void Format_MatchesChosenPattern()
    {
        var due = new DateTime(2026, 9, 8);
        Assert.Equal("9/8", TodoDate.Format(due, DateFormats.Md));
        Assert.Equal("26/09/08", TodoDate.Format(due, DateFormats.YyMd));
        Assert.Equal("2026/09/08", TodoDate.Format(due, DateFormats.Ymd));
        Assert.Equal("", TodoDate.Format(null, DateFormats.Md));
    }

    [Fact]
    public void SoonDays_AreClamped()
    {
        Assert.Equal(1, TodoDate.ClampSoonDays(0));
        Assert.Equal(14, TodoDate.ClampSoonDays(99));
        Assert.Equal("md", TodoDate.NormalizeFormat("nope"));
        Assert.Equal(DateFormats.Ymd, TodoDate.NormalizeFormat("YMD"));
    }
}
