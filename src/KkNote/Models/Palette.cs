namespace KkNote.Models;

public sealed record PaletteEntry(string Name, string Background, string FontColor);

public static class Palette
{
    public const string DefaultBackground = "#F6F0E4";
    public const string DefaultFont = "#2A3340";

    public static IReadOnlyList<PaletteEntry> Backgrounds { get; } =
    [
        new("浅纸", "#F6F0E4", "#2A3340"),
        new("暖白", "#FFF9F1", "#2A3340"),
        new("雾青", "#E7F1EE", "#1E3A34"),
        new("粉笺", "#F8ECE8", "#3A2A28"),
        new("深空蓝", "#152033", "#E7EEF9"),
    ];

    public static IReadOnlyList<string> FontColors { get; } =
    [
        "#2A3340",
        "#1C2430",
        "#3A6B8C",
        "#B4532A",
        "#C45C6A",
        "#E7EEF9",
    ];
}
