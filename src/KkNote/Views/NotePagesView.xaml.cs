using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KkNote.Converters;
using KkNote.Models;

namespace KkNote.Views;

public partial class NotePagesView : UserControl
{
    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note),
        typeof(Note),
        typeof(NotePagesView),
        new PropertyMetadata(null, (d, e) => ((NotePagesView)d).Switch((Note?)e.OldValue, (Note?)e.NewValue)));

    private readonly List<NotePage> _hookedPages = [];
    private Note? _note;
    private int _index;
    private bool _internal;

    public NotePagesView()
    {
        InitializeComponent();
    }

    public Note? Note
    {
        get => (Note?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    private NotePage? Current =>
        _note != null && _index >= 0 && _index < _note.Pages.Count ? _note.Pages[_index] : null;

    private void Switch(Note? oldNote, Note? note)
    {
        if (oldNote != null)
            Unhook(oldNote);
        _note = note;
        _index = 0;
        if (note != null)
        {
            note.EnsurePage();
            Hook(note);
        }

        ApplyChrome();
        ShowCurrent();
    }

    private void Hook(Note note)
    {
        note.PropertyChanged += OnNoteProperty;
        note.Pages.CollectionChanged += OnPagesChanged;
        foreach (var page in note.Pages)
        {
            page.PropertyChanged += OnPageProperty;
            _hookedPages.Add(page);
        }
    }

    private void Unhook(Note note)
    {
        note.PropertyChanged -= OnNoteProperty;
        note.Pages.CollectionChanged -= OnPagesChanged;
        foreach (var page in _hookedPages)
            page.PropertyChanged -= OnPageProperty;
        _hookedPages.Clear();
    }

    private void OnNoteProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Note.FontFamily)
            or nameof(Note.FontSize)
            or nameof(Note.FontColor)
            or nameof(Note.Bold)
            or nameof(Note.Background))
        {
            ApplyChrome();
        }
    }

    private void OnPagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (NotePage page in e.OldItems)
            {
                page.PropertyChanged -= OnPageProperty;
                _hookedPages.Remove(page);
            }
        }

        if (e.NewItems != null)
        {
            foreach (NotePage page in e.NewItems)
            {
                if (_hookedPages.Contains(page))
                    continue;
                page.PropertyChanged += OnPageProperty;
                _hookedPages.Add(page);
            }
        }

        if (_note != null && _index >= _note.Pages.Count)
            _index = Math.Max(0, _note.Pages.Count - 1);
        ShowCurrent();
    }

    private void OnPageProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (_internal || e.PropertyName != nameof(NotePage.Text) || !ReferenceEquals(sender, Current))
            return;
        if (Body.IsKeyboardFocused)
            return;
        ShowCurrent();
    }

    private void ApplyChrome()
    {
        if (_note == null)
            return;
        Body.FontFamily = new FontFamily(FontCatalog.Normalize(_note.FontFamily));
        Body.FontSize = _note.FontSize;
        Body.FontWeight = _note.Bold ? FontWeights.Bold : FontWeights.Normal;
        var foreground = ColorUtil.Brush(_note.FontColor);
        Body.Foreground = foreground;
        Body.CaretBrush = foreground;
        Placeholder.Foreground = Fade(_note.FontColor);
        PageLabel.Foreground = ColorUtil.IsLight(_note.Background)
            ? ColorUtil.Brush("#1C2430")
            : ColorUtil.Brush("#8EA0BE");
        var buttonText = ColorUtil.Brush("#E7EEF9");
        PrevButton.Foreground = buttonText;
        NextButton.Foreground = buttonText;
        AddButton.Foreground = buttonText;
        RemoveButton.Foreground = buttonText;
    }

    private void ShowCurrent()
    {
        _internal = true;
        var text = Current?.Text ?? "";
        if (Body.Text != text)
            Body.Text = text;
        Placeholder.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var count = _note?.Pages.Count ?? 0;
        PageLabel.Text = count == 0 ? "0/0" : $"{_index + 1}/{count}";
        PrevButton.IsEnabled = _note != null && _index > 0;
        NextButton.IsEnabled = _note != null && _index < count - 1;
        AddButton.IsEnabled = _note != null;
        RemoveButton.IsEnabled = _note != null && count > 1;
        Body.IsEnabled = _note != null;
        _internal = false;
    }

    private void Body_TextChanged(object sender, TextChangedEventArgs e)
    {
        Placeholder.Visibility = Body.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_internal || Current == null)
            return;
        _internal = true;
        Current.Text = Body.Text;
        _internal = false;
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_index <= 0)
            return;
        _index--;
        ShowCurrent();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_note == null || _index >= _note.Pages.Count - 1)
            return;
        _index++;
        ShowCurrent();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_note == null)
            return;
        _note.Pages.Add(NotePage.Create());
        _index = _note.Pages.Count - 1;
        ShowCurrent();
        Body.Focus();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_note == null || _note.Pages.Count <= 1 || _index < 0 || _index >= _note.Pages.Count)
            return;
        _note.Pages.RemoveAt(_index);
        if (_index >= _note.Pages.Count)
            _index = _note.Pages.Count - 1;
        ShowCurrent();
    }

    private static SolidColorBrush Fade(string? hex)
    {
        if (!ColorUtil.TryParse(hex, out var color))
            color = Color.FromRgb(0x8E, 0xA0, 0xBE);
        var brush = new SolidColorBrush(Color.FromArgb(150, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
