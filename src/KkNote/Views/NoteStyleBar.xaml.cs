using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KkNote.Converters;
using KkNote.Models;

namespace KkNote.Views;

public partial class NoteStyleBar : UserControl
{
    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note),
        typeof(Note),
        typeof(NoteStyleBar),
        new PropertyMetadata(null, (d, e) => ((NoteStyleBar)d).OnNoteChanged((Note?)e.OldValue, (Note?)e.NewValue)));

    private bool _internal;

    public NoteStyleBar()
    {
        InitializeComponent();
        BuildSwatches();
    }

    public Note? Note
    {
        get => (Note?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    private void OnNoteChanged(Note? oldNote, Note? note)
    {
        if (oldNote != null)
            oldNote.PropertyChanged -= OnNoteProperty;
        if (note != null)
            note.PropertyChanged += OnNoteProperty;
        SyncFromNote();
    }

    private void OnNoteProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Note.Background) or nameof(Note.FontColor)
            or nameof(Note.FontFamily) or nameof(Note.FontSize) or nameof(Note.Bold))
            SyncFromNote();
    }

    private void BuildSwatches()
    {
        foreach (var entry in Palette.Backgrounds)
        {
            var button = MakeSwatch(entry.Background);
            button.ToolTip = entry.Name;
            button.Click += (_, _) =>
            {
                if (Note == null)
                    return;
                Note.Background = entry.Background;
                Note.FontColor = entry.FontColor;
            };
            BgSwatches.Children.Add(button);
        }

        foreach (var hex in Palette.FontColors)
        {
            var button = MakeSwatch(hex);
            button.Width = 16;
            button.Height = 16;
            button.Click += (_, _) =>
            {
                if (Note != null)
                    Note.FontColor = hex;
            };
            FgSwatches.Children.Add(button);
        }
    }

    private Button MakeSwatch(string hex) => new()
    {
        Style = (Style)FindResource("SwatchButton"),
        Background = ColorUtil.Brush(hex),
        BorderBrush = ColorUtil.Brush("#D9CDBB"),
        BorderThickness = new Thickness(1),
        Tag = hex,
    };

    private void SyncFromNote()
    {
        _internal = true;
        var note = Note;
        if (note == null)
        {
            IsEnabled = false;
            _internal = false;
            return;
        }

        IsEnabled = true;
        FontBox.SelectedItem = FontCatalog.Normalize(note.FontFamily);
        SizeBox.SelectedItem = note.FontSize;
        BoldBox.IsChecked = note.Bold;
        MarkSwatches(BgSwatches, note.Background);
        MarkSwatches(FgSwatches, note.FontColor);
        var light = ColorUtil.IsLight(note.Background);
        var ink = light ? ColorUtil.Brush("#3A3228") : ColorUtil.Brush("#E7EEF9");
        var muted = light ? ColorUtil.Brush("#9A8F80") : ColorUtil.Brush("#8EA0BE");
        Toggle.Foreground = ink;
        PaperLabel.Foreground = muted;
        InkLabel.Foreground = muted;
        Panel.Background = light ? ColorUtil.Brush("#FBF6EE") : ColorUtil.ShiftBrush(note.Background, 0.1);
        Panel.BorderBrush = light ? ColorUtil.Brush("#D9CDBB") : ColorUtil.Brush("#34506A");
        _internal = false;
    }

    private void MarkSwatches(Panel host, string selected)
    {
        foreach (Button button in host.Children)
        {
            var hex = (string)button.Tag;
            var on = string.Equals(hex, selected, StringComparison.OrdinalIgnoreCase);
            button.BorderBrush = on ? ColorUtil.Brush("#C4A574") : ColorUtil.Brush("#D9CDBB");
            button.BorderThickness = new Thickness(on ? 2 : 1);
        }
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        Panel.Visibility = Toggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        Toggle.Content = Toggle.IsChecked == true ? "格式 ▲" : "格式";
    }

    private void FontBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_internal || Note == null || FontBox.SelectedItem is not string family)
            return;
        Note.FontFamily = family;
    }

    private void SizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_internal || Note == null || SizeBox.SelectedItem is not double size)
            return;
        Note.FontSize = size;
    }

    private void BoldBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_internal || Note == null)
            return;
        Note.Bold = BoldBox.IsChecked == true;
    }
}
