using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KkNote.Converters;
using KkNote.Models;
using KkNote.Services;

namespace KkNote.Views;

public partial class StickyWindow : Window
{
    private readonly NoteStore _store;
    private readonly Note _note;
    private bool _ready;
    private bool _closing;
    private bool _internal;

    public StickyWindow(NoteStore store, Note note)
    {
        _store = store;
        _note = note;
        DataContext = note;
        InitializeComponent();
        Width = Math.Max(note.Width, MinWidth);
        Height = Math.Max(note.Height, MinHeight);
        Left = note.X;
        Top = note.Y;
        Title = note.DisplayTitle;
        _internal = true;
        SpreadToggle.IsChecked = true;
        _internal = false;
        note.PropertyChanged += OnNoteProperty;
        StyleBar.Note = note;
        ApplyChrome();
    }

    public bool SuppressUnpin { get; set; }

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
        WindowEffects.ApplyRounded(this, dark: !ColorUtil.IsLight(_note.Background));
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
        _note.X = Left;
        _note.Y = Top;
        if (ActualWidth >= NoteLimits.MinWidth)
            _note.Width = ActualWidth;
        if (ActualHeight >= NoteLimits.MinHeight)
            _note.Height = ActualHeight;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_closing)
            return;
        _closing = true;
        _note.PropertyChanged -= OnNoteProperty;
        if (!SuppressUnpin && _note.PinnedToDesktop)
            _note.PinnedToDesktop = false;
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
        if (!_ready)
            return;
        _note.X = Left;
        _note.Y = Top;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (!_ready)
            return;
        _note.Width = ActualWidth;
        _note.Height = ActualHeight;
    }

    private void OnNoteProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Note.Title))
            Title = _note.DisplayTitle;
        else if (e.PropertyName is nameof(Note.Background) or nameof(Note.FontColor)
                 or nameof(Note.FontFamily) or nameof(Note.FontSize) or nameof(Note.Bold))
            ApplyChrome();
    }

    private void ApplyChrome()
    {
        var light = ColorUtil.IsLight(_note.Background);
        var paper = ColorUtil.Brush(_note.Background, Palette.DefaultBackground);
        var desk = ColorUtil.ShiftBrush(_note.Background, light ? -0.06 : 0.08);
        Root.Background = desk;
        Header.Background = paper;
        Root.BorderBrush = light ? ColorUtil.Brush("#D9CDBB") : ColorUtil.Brush("#34506A");
        var ink = ColorUtil.Brush(_note.FontColor, Palette.DefaultFont);
        TitleLabel.Foreground = ink;
        Body.Foreground = ink;
        Body.CaretBrush = ink;
        Body.FontFamily = new FontFamily(FontCatalog.Normalize(_note.FontFamily));
        Body.FontSize = _note.FontSize;
        Body.FontWeight = _note.Bold ? FontWeights.Bold : FontWeights.Normal;
        Paper.Background = paper;
        Paper.BorderBrush = light ? ColorUtil.Brush("#E2D6C4") : ColorUtil.Brush("#34506A");
        PinButton.Foreground = light ? ColorUtil.Brush("#3A3228") : ColorUtil.Brush("#E7EEF9");
        CloseButton.Foreground = PinButton.Foreground;
        SpreadToggle.Foreground = PinButton.Foreground;
        if (IsLoaded)
            WindowEffects.ApplyRounded(this, dark: !light);
    }

    private void SetMenuTopmost(bool value) => _note.AlwaysOnTop = value;

    private void Unpin_Click(object sender, RoutedEventArgs e)
    {
        _note.PinnedToDesktop = false;
    }

    private void SpreadToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_internal)
            return;
        _note.Detached = SpreadToggle.IsChecked == true;
    }

    private void PageMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
            PageListMenu.Fill(menu, _store, _note, _note.AlwaysOnTop, SetMenuTopmost);
    }

    private void Window_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<TextBox>(e.OriginalSource as DependencyObject) != null)
            return;
        PageListMenu.Fill(PageMenu, _store, _note, _note.AlwaysOnTop, SetMenuTopmost);
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
}
