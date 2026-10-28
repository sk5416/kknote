namespace KkNote.Models;

public sealed record PaletteEntry(string Name, string Background, string FontColor);

public static class Palette
{
    public const string DefaultBackground = "#152033";
    public const string DefaultFont = "#E7EEF9";

    public static IReadOnlyList<PaletteEntry> Backgrounds { get; } =
    [
        new("深空蓝", "#152033", "#E7EEF9"),
        new("墨青", "#102824", "#D7F6EF"),
        new("暖砂", "#2A241C", "#F6E7C1"),
        new("夜紫", "#22182E", "#F0E4FF"),
        new("浅纸", "#F4F0E6", "#1C2430"),
    ];

    public static IReadOnlyList<string> FontColors { get; } =
    [
        "#E7EEF9",
        "#1C2430",
        "#3DDCFF",
        "#F5D76E",
        "#FF8FA3",
        "#B8F2C8",
    ];
}
