namespace KkNote.Models;

public sealed class AppSettings : ObservableObject
{
    private string _hotkey = "Ctrl+Alt+N";
    private bool _spreadPages;
    private string? _stackCurrentId;
    private double _stackX = 120;
    private double _stackY = 80;
    private double _stackWidth = NoteLimits.DefaultWidth;
    private double _stackHeight = NoteLimits.DefaultHeight;
    private bool _stackAlwaysOnTop = true;

    public string Hotkey
    {
        get => _hotkey;
        set => Set(ref _hotkey, string.IsNullOrWhiteSpace(value) ? "Ctrl+Alt+N" : value);
    }

    public bool SpreadPages
    {
        get => _spreadPages;
        set => Set(ref _spreadPages, value);
    }

    public string? StackCurrentId
    {
        get => _stackCurrentId;
        set => Set(ref _stackCurrentId, value);
    }

    public double StackX
    {
        get => _stackX;
        set => Set(ref _stackX, value);
    }

    public double StackY
    {
        get => _stackY;
        set => Set(ref _stackY, value);
    }

    public double StackWidth
    {
        get => _stackWidth;
        set => Set(ref _stackWidth, Math.Max(NoteLimits.MinWidth, value));
    }

    public double StackHeight
    {
        get => _stackHeight;
        set => Set(ref _stackHeight, Math.Max(NoteLimits.MinHeight, value));
    }

    public bool StackAlwaysOnTop
    {
        get => _stackAlwaysOnTop;
        set => Set(ref _stackAlwaysOnTop, value);
    }
}
