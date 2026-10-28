using System.ComponentModel;
using System.Windows;
using KkNote.Converters;
using KkNote.Models;
using KkNote.Services;

namespace KkNote.Views;

public partial class StickyWindow : Window
{
    private readonly Note _note;
    private bool _ready;
    private bool _closing;

    public StickyWindow(NoteStore store, Note note)
    {
        _ = store;
        _note = note;
        DataContext = note;
        InitializeComponent();
        Width = Math.Max(note.Width, MinWidth);
        Height = Math.Max(note.Height, MinHeight);
        Left = note.X;
        Top = note.Y;
        Title = string.IsNullOrWhiteSpace(note.Title) ? "便签" : note.Title;
        note.PropertyChanged += OnNoteProperty;
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
        WindowEffects.ApplyRoundedDark(this);
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
            Title = string.IsNullOrWhiteSpace(_note.Title) ? "便签" : _note.Title;
        else if (e.PropertyName is nameof(Note.Background) or nameof(Note.FontColor))
            ApplyChrome();
    }

    private void ApplyChrome()
    {
        var light = ColorUtil.IsLight(_note.Background);
        Root.Background = ColorUtil.Brush(_note.Background);
        Header.Background = ColorUtil.Shade(_note.Background, light ? 0.94 : 0.72);
        Root.BorderBrush = light ? ColorUtil.Brush("#D5D0C4") : ColorUtil.Brush("#34506A");
        TitleBox.Foreground = ColorUtil.Brush(_note.FontColor);
        TitleBox.CaretBrush = ColorUtil.Brush(_note.FontColor);
    }

    private void Unpin_Click(object sender, RoutedEventArgs e)
    {
        _note.PinnedToDesktop = false;
    }
}
