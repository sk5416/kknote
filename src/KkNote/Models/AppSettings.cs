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
    private bool _stackCollapsed;
    private string _dateFormat = DateFormats.Md;
    private int _dueSoonDays = TodoDate.DefaultSoonDays;
    private double _opacity = NoteLimits.DefaultOpacity;
    private bool _todoManualOrder;

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

    public bool StackCollapsed
    {
        get => _stackCollapsed;
        set => Set(ref _stackCollapsed, value);
    }

    public string DateFormat
    {
        get => _dateFormat;
        set => Set(ref _dateFormat, TodoDate.NormalizeFormat(value));
    }

    public int DueSoonDays
    {
        get => _dueSoonDays;
        set => Set(ref _dueSoonDays, TodoDate.ClampSoonDays(value));
    }

    public double Opacity
    {
        get => _opacity;
        set => Set(ref _opacity, NoteLimits.ClampOpacity(value));
    }

    public bool TodoManualOrder
    {
        get => _todoManualOrder;
        set => Set(ref _todoManualOrder, value);
    }
}
