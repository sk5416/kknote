using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using KkNote.Converters;
using KkNote.Models;
using KkNote.Services;

namespace KkNote.Views;

public partial class PaperStackWindow : Window
{
    private readonly NoteStore _store;
    private readonly List<Note> _hooked = [];
    private Note? _current;
    private bool _ready;
    private bool _closing;
    private bool _internal;
    private bool _folding;
    private double _restoredHeight;

    public PaperStackWindow(NoteStore store)
    {
        _store = store;
        InitializeComponent();
        Width = Math.Max(store.Settings.StackWidth, MinWidth);
        _restoredHeight = Math.Max(store.Settings.StackHeight, NoteLimits.MinHeight);
        Height = _restoredHeight;
        Left = store.Settings.StackX;
        Top = store.Settings.StackY;
        Topmost = store.Settings.StackAlwaysOnTop;
        _internal = true;
        PinButton.IsChecked = store.Settings.StackAlwaysOnTop;
        SpreadToggle.IsChecked = false;
        _internal = false;
        store.Settings.PropertyChanged += OnSettingsChanged;
        store.Notes.CollectionChanged += OnNotesChanged;
        foreach (var note in store.Notes)
            Hook(note);
        SelectCurrent(FindInitial());
        ApplyCollapsed();
    }

    public bool SuppressUnpin { get; set; }

    public void ShowPage(Note note)
    {
        if (note.PinnedToDesktop && !note.Detached)
            SelectCurrent(note);
        Show();
        Activate();
    }

    public void RequestClose()
    {
        if (_closing)
            return;
        Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowEffects.MakeToolWindow(this);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_ready)
            return;
        if (!WindowEffects.IsOnScreen(Left, Top, Width, Height))
        {
            Left = SystemParameters.WorkArea.Left + 80;
            Top = SystemParameters.WorkArea.Top + 80;
        }

        _ready = true;
        SaveBounds();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_closing)
            return;
        _closing = true;
        _store.Settings.PropertyChanged -= OnSettingsChanged;
        _store.Notes.CollectionChanged -= OnNotesChanged;
        foreach (var note in _hooked.ToList())
            Unhook(note);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (_ready && !_folding)
            SaveBounds();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_ready && !_folding)
            SaveBounds();
    }

    private List<Note> Stacked => _store.StackedPages().ToList();

    private Note? FindInitial()
    {
        var stacked = Stacked;
        if (stacked.Count == 0)
            return null;
        return stacked.FirstOrDefault(item => item.Id == _store.Settings.StackCurrentId) ?? stacked[0];
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.Opacity))
            Opacity = _store.Settings.Opacity;
    }

    private void OnNotesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (Note note in e.NewItems)
                Hook(note);
        }

        if (e.OldItems != null)
        {
            foreach (Note note in e.OldItems)
                Unhook(note);
        }

        RefreshPinned();
    }

    private void Hook(Note note)
    {
        if (_hooked.Contains(note))
            return;
        _hooked.Add(note);
        note.PropertyChanged += OnNoteProperty;
    }

    private void Unhook(Note note)
    {
        if (!_hooked.Remove(note))
            return;
        note.PropertyChanged -= OnNoteProperty;
    }

    private void OnNoteProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Note note)
            return;
        if (e.PropertyName is nameof(Note.PinnedToDesktop) or nameof(Note.Detached))
        {
            RefreshPinned();
            return;
        }

        if (!ReferenceEquals(note, _current))
            return;
        if (e.PropertyName == nameof(Note.Title) && !_internal)
        {
            TitleLabel.Text = note.DisplayTitle;
            Title = note.DisplayTitle;
        }
        else if (e.PropertyName == nameof(Note.Text) && !_internal && !Body.IsKeyboardFocused)
        {
            if (Body.Text != note.Text)
                Body.Text = note.Text;
            Placeholder.Visibility = string.IsNullOrEmpty(note.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (e.PropertyName is nameof(Note.Background) or nameof(Note.FontColor)
                 or nameof(Note.FontFamily) or nameof(Note.FontSize) or nameof(Note.Bold)
                 or nameof(Note.Kind))
        {
            ApplyChrome();
        }
    }

    private void RefreshPinned()
    {
        var stacked = Stacked;
        if (stacked.Count == 0)
        {
            if (!_closing)
            {
                SuppressUnpin = true;
                RequestClose();
            }
            return;
        }

        if (_current == null || !stacked.Contains(_current))
            SelectCurrent(stacked[0]);
        else
        {
            ApplyChrome();
            RebuildPeeks();
        }
    }

    private void SelectCurrent(Note? note)
    {
        _current = note;
        if (note == null)
            return;
        _store.Settings.StackCurrentId = note.Id;
        _internal = true;
        TitleLabel.Text = note.DisplayTitle;
        Body.Text = note.Text;
        Placeholder.Visibility = string.IsNullOrEmpty(note.Text) ? Visibility.Visible : Visibility.Collapsed;
        SpreadToggle.IsChecked = false;
        _internal = false;
        Title = note.DisplayTitle;
        StyleBar.Store = _store;
        StyleBar.Note = note;
        TodoHost.Store = _store;
        TodoHost.Note = note;
        ApplyChrome();
        RebuildPeeks();
    }

    private void ApplyChrome()
    {
        if (_current == null)
            return;
        var light = ColorUtil.IsLight(_current.Background);
        var paper = ColorUtil.Brush(_current.Background, Palette.DefaultBackground);
        var desk = ColorUtil.ShiftBrush(_current.Background, light ? -0.06 : 0.08);
        Root.Background = desk;
        Header.Background = paper;
        FrontPaper.Background = paper;
        Root.BorderBrush = light ? ColorUtil.Brush("#D9CDBB") : ColorUtil.Brush("#34506A");
        FrontPaper.BorderBrush = light ? ColorUtil.Brush("#E2D6C4") : ColorUtil.Brush("#34506A");
        var ink = ColorUtil.Brush(_current.FontColor, Palette.DefaultFont);
        TitleLabel.Foreground = ink;
        Body.Foreground = ink;
        Body.CaretBrush = ink;
        Body.FontFamily = new FontFamily(FontCatalog.Normalize(_current.FontFamily));
        Body.FontSize = _current.FontSize;
        Body.FontWeight = _current.Bold ? FontWeights.Bold : FontWeights.Normal;
        var muted = light ? ColorUtil.Brush("#9A8F80") : ColorUtil.Brush("#8EA0BE");
        Placeholder.Foreground = muted;
        PageBadge.Foreground = muted;
        var buttonInk = light ? ColorUtil.Brush("#3A3228") : ColorUtil.Brush("#E7EEF9");
        CloseButton.Foreground = buttonInk;
        CollapseButton.Foreground = buttonInk;
        SpreadToggle.Foreground = buttonInk;
        Opacity = _store.Settings.Opacity;
        var todo = _current.IsTodo;
        TextHost.Visibility = todo ? Visibility.Collapsed : Visibility.Visible;
        TodoHost.Visibility = todo ? Visibility.Visible : Visibility.Collapsed;
        if (_store.Settings.StackCollapsed)
            ApplyCollapsed();
    }

    private void RebuildPeeks()
    {
        PeekLayer.Children.Clear();
        var stacked = Stacked;
        var count = stacked.Count;
        PageBadge.Text = count <= 1 ? "" : $"{stacked.IndexOf(_current!) + 1} / {count}";
        var many = count > 1;
        if (!many || _current == null)
        {
            FrontPaper.Margin = new Thickness(0);
            return;
        }

        var index = Math.Max(0, stacked.IndexOf(_current));
        var behind = Math.Min(count - 1, 3);
        // Right-edge tabs only. The sheet fills the deck vertically so the body stays tall.
        const double gutter = 28;
        var step = gutter / behind;
        FrontPaper.Margin = new Thickness(0, 0, gutter, 0);
        for (var i = behind; i >= 1; i--)
        {
            var page = stacked[(index + i) % count];
            var depth = behind - i;
            PeekLayer.Children.Add(CreatePeek(page, gutter - i * step, depth));
        }
    }

    private Border CreatePeek(Note note, double right, int depth)
    {
        var light = ColorUtil.IsLight(note.Background);
        var amount = light ? -0.06 - depth * 0.05 : 0.08 + depth * 0.05;
        var border = new Border
        {
            Background = ColorUtil.ShiftBrush(note.Background, amount),
            BorderBrush = light ? ColorUtil.Brush("#D9CDBB") : ColorUtil.Brush("#4A6080"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Cursor = Cursors.Hand,
            Tag = note,
            Margin = new Thickness(0, 0, right, 0),
            ToolTip = note.DisplayTitle,
            ClipToBounds = true,
        };
        border.MouseLeftButtonDown += (_, e) =>
        {
            SelectCurrent(note);
            e.Handled = true;
        };
        AutomationProperties.SetName(border, note.DisplayTitle);
        return border;
    }

    private void Body_TextChanged(object sender, TextChangedEventArgs e)
    {
        Placeholder.Visibility = Body.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_internal || _current == null)
            return;
        _internal = true;
        _current.Text = Body.Text;
        _internal = false;
    }

    private void SetMenuTopmost(bool value)
    {
        _internal = true;
        PinButton.IsChecked = value;
        _internal = false;
        Topmost = value;
        _store.Settings.StackAlwaysOnTop = value;
    }

    private void PinButton_Changed(object sender, RoutedEventArgs e)
    {
        if (_internal)
            return;
        SetMenuTopmost(PinButton.IsChecked == true);
    }

    private void SpreadToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_internal || _current == null || SpreadToggle.IsChecked != true)
            return;
        _current.X = Left + 40;
        _current.Y = Top + 40;
        if (ActualWidth >= NoteLimits.MinWidth)
            _current.Width = ActualWidth;
        if (ActualHeight >= NoteLimits.MinHeight)
            _current.Height = ActualHeight;
        _current.Detached = true;
    }

    private void PageMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
            PageListMenu.Fill(menu, _store, _current, Topmost, SetMenuTopmost);
    }

    private void Window_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<TextBox>(e.OriginalSource as DependencyObject) != null)
            return;
        if (FindParent<System.Windows.Controls.Primitives.ButtonBase>(e.OriginalSource as DependencyObject) != null)
            return;
        if (FindParent<ComboBox>(e.OriginalSource as DependencyObject) != null)
            return;
        if (FindParent<Calendar>(e.OriginalSource as DependencyObject) != null)
            return;
        PageListMenu.Fill(PageMenu, _store, _current, Topmost, SetMenuTopmost);
        PageMenu.Placement = PlacementMode.MousePoint;
        PageMenu.IsOpen = true;
        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject? start) where T : DependencyObject
    {
        while (start != null)
        {
            if (start is T match)
                return match;
            start = VisualTreeHelper.GetParent(start);
        }
        return null;
    }

    private void ApplyCollapsed()
    {
        var folded = _store.Settings.StackCollapsed;
        _internal = true;
        CollapseButton.IsChecked = folded;
        _internal = false;
        var vis = folded ? Visibility.Collapsed : Visibility.Visible;
        StyleBar.Visibility = vis;
        Deck.Visibility = vis;
        Footer.Visibility = vis;
        CollapseButton.Content = folded ? "▾" : "▴";
        CollapseButton.ToolTip = folded ? "展开" : "折叠成标题栏";
        _folding = true;
        try
        {
            if (folded)
            {
                if (ActualHeight >= NoteLimits.MinHeight)
                {
                    _restoredHeight = ActualHeight;
                    _store.Settings.StackHeight = ActualHeight;
                }
                MinHeight = NoteLimits.CollapsedHeight;
                MaxHeight = NoteLimits.CollapsedHeight;
                Height = NoteLimits.CollapsedHeight;
            }
            else
            {
                var restore = Math.Max(_restoredHeight, Math.Max(_store.Settings.StackHeight, NoteLimits.MinHeight));
                MaxHeight = double.PositiveInfinity;
                MinHeight = NoteLimits.MinHeight;
                Height = restore;
            }
        }
        finally
        {
            _folding = false;
        }
    }

    private void CollapseButton_Changed(object sender, RoutedEventArgs e)
    {
        if (_internal)
            return;
        _store.Settings.StackCollapsed = CollapseButton.IsChecked == true;
        ApplyCollapsed();
    }

    private void AddText_Click(object sender, RoutedEventArgs e) => AddPinned(NoteKind.Text);

    private void AddTodo_Click(object sender, RoutedEventArgs e) => AddPinned(NoteKind.Todo);

    private void AddPinned(string kind)
    {
        var note = _store.AddNew(kind);
        note.PinnedToDesktop = true;
        SelectCurrent(note);
    }

    private void Unpin_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null)
            return;
        _current.PinnedToDesktop = false;
    }

    private void SaveBounds()
    {
        _store.Settings.StackX = Left;
        _store.Settings.StackY = Top;
        if (ActualWidth >= NoteLimits.MinWidth)
            _store.Settings.StackWidth = ActualWidth;
        if (!_store.Settings.StackCollapsed && ActualHeight >= NoteLimits.MinHeight)
        {
            _store.Settings.StackHeight = ActualHeight;
            _restoredHeight = ActualHeight;
        }
    }
}
