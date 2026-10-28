using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json.Serialization;

namespace KkNote.Models;

public sealed class Note : ObservableObject
{
    private string _id = "";
    private string _title = "";
    private bool _pinnedToDesktop;
    private bool _alwaysOnTop = true;
    private bool _spreadPages;
    private int _currentPageIndex;
    private string _background = Palette.DefaultBackground;
    private double _x = 120;
    private double _y = 80;
    private double _width = NoteLimits.DefaultWidth;
    private double _height = NoteLimits.DefaultHeight;
    private string _fontFamily = "Microsoft YaHei UI";
    private double _fontSize = 14;
    private string _fontColor = Palette.DefaultFont;
    private bool _bold;
    private ObservableCollection<NotePage> _pages = new();

    public Note()
    {
        _pages.CollectionChanged += OnPagesChanged;
    }

    public string Id
    {
        get => _id;
        set => Set(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set
        {
            if (Set(ref _title, value ?? ""))
                OnPropertyChanged(nameof(DisplayTitle));
        }
    }

    public bool PinnedToDesktop
    {
        get => _pinnedToDesktop;
        set => Set(ref _pinnedToDesktop, value);
    }

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set => Set(ref _alwaysOnTop, value);
    }

    public bool SpreadPages
    {
        get => _spreadPages;
        set => Set(ref _spreadPages, value);
    }

    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set => Set(ref _currentPageIndex, Math.Max(0, value));
    }

    public string Background
    {
        get => _background;
        set => Set(ref _background, string.IsNullOrWhiteSpace(value) ? Palette.DefaultBackground : value);
    }

    public double X
    {
        get => _x;
        set => Set(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => Set(ref _y, value);
    }

    public double Width
    {
        get => _width;
        set => Set(ref _width, Math.Max(NoteLimits.MinWidth, value));
    }

    public double Height
    {
        get => _height;
        set => Set(ref _height, Math.Max(NoteLimits.MinHeight, value));
    }

    public string FontFamily
    {
        get => _fontFamily;
        set => Set(ref _fontFamily, string.IsNullOrWhiteSpace(value) ? "Microsoft YaHei UI" : value);
    }

    public double FontSize
    {
        get => _fontSize;
        set => Set(ref _fontSize, NoteLimits.SnapFontSize(value));
    }

    public string FontColor
    {
        get => _fontColor;
        set => Set(ref _fontColor, string.IsNullOrWhiteSpace(value) ? Palette.DefaultFont : value);
    }

    public bool Bold
    {
        get => _bold;
        set => Set(ref _bold, value);
    }

    public ObservableCollection<NotePage> Pages
    {
        get => _pages;
        set
        {
            if (value == null || ReferenceEquals(value, _pages))
                return;
            _pages.CollectionChanged -= OnPagesChanged;
            _pages = value;
            _pages.CollectionChanged += OnPagesChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageCount));
        }
    }

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(_title) ? "无标题" : _title;

    [JsonIgnore]
    public int PageCount => _pages.Count;

    public static Note CreateNew(double x, double y)
    {
        var note = new Note
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "新便签",
            X = x,
            Y = y,
        };
        note.Pages.Add(NotePage.Create());
        return note;
    }

    public void EnsurePage()
    {
        if (_pages.Count == 0)
            _pages.Add(NotePage.Create());
    }

    private void OnPagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(PageCount));
}
