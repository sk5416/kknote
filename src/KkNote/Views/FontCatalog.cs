using KkNote.Models;

namespace KkNote.Views;

public static class FontCatalog
{
    // A short list keeps the process from loading every installed font.
    public static IReadOnlyList<string> Families { get; } =
    [
        "Microsoft YaHei UI",
        "Microsoft YaHei",
        "等线",
        "宋体",
        "黑体",
        "楷体",
        "仿宋",
        "Segoe UI",
        "Segoe UI Variable Display",
        "Cascadia Mono",
        "Consolas",
        "Arial",
        "Calibri",
        "Cambria",
        "Georgia",
        "Verdana",
        "Tahoma",
        "Times New Roman",
    ];

    public static IReadOnlyList<double> Sizes { get; } = NoteLimits.FontSizes;

    public static string Normalize(string? family)
    {
        if (!string.IsNullOrWhiteSpace(family))
        {
            var exact = Families.FirstOrDefault(item => string.Equals(item, family, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;
            return family;
        }

        foreach (var name in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" })
        {
            var hit = Families.FirstOrDefault(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
            if (hit != null)
                return hit;
        }

        return Families[0];
    }
}
