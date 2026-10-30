using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KkNote.Converters;
using KkNote.Models;
using KkNote.Services;

namespace KkNote.Views;

public partial class NoteStyleBar : UserControl
{
    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note),
        typeof(Note),
        typeof(NoteStyleBar),
        new PropertyMetadata(null, (d, e) => ((NoteStyleBar)d).OnNoteChanged((Note?)e.OldValue, (Note?)e.NewValue)));

    public static readonly DependencyProperty CollapsibleProperty = DependencyProperty.Register(
        nameof(Collapsible),
        typeof(bool),
        typeof(NoteStyleBar),
        new PropertyMetadata(true, (d, _) => ((NoteStyleBar)d).ApplyLayout()));

    public static readonly DependencyProperty DarkProperty = DependencyProperty.Register(
        nameof(Dark),
        typeof(bool),
        typeof(NoteStyleBar),
        new PropertyMetadata(false, (d, _) => ((NoteStyleBar)d).SyncFromNote()));

    public static readonly DependencyProperty StoreProperty = DependencyProperty.Register(
        nameof(Store),
        typeof(NoteStore),
        typeof(NoteStyleBar),
        new PropertyMetadata(null, (d, e) => ((NoteStyleBar)d).OnStoreChanged((NoteStore?)e.OldValue, (NoteStore?)e.NewValue)));

    private bool _internal;

    public NoteStyleBar()
    {
        InitializeComponent();
        BuildSwatches();
        ApplyLayout();
    }

    public Note? Note
    {
        get => (Note?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public bool Collapsible
    {
        get => (bool)GetValue(CollapsibleProperty);
        set => SetValue(CollapsibleProperty, value);
    }

    public bool Dark
    {
        get => (bool)GetValue(DarkProperty);
        set => SetValue(DarkProperty, value);
    }

    public NoteStore? Store
    {
        get => (NoteStore?)GetValue(StoreProperty);
        set => SetValue(StoreProperty, value);
    }

    private void OnStoreChanged(NoteStore? oldStore, NoteStore? store)
    {
        if (oldStore != null)
            oldStore.Settings.PropertyChanged -= OnSettingsChanged;
        if (store != null)
            store.Settings.PropertyChanged += OnSettingsChanged;
        SyncFromNote();
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.Opacity))
            SyncFromNote();
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
            var button = MakeSwatch(entry.Background, 18);
            button.ToolTip = entry.Name;
            AutomationProperties.SetName(button, "底色" + entry.Name);
            button.Click += (_, _) =>
            {
                if (Note == null)
                    return;
                Note.Background = entry.Background;
                Note.FontColor = entry.FontColor;
                PaperChip.IsChecked = false;
            };
            BgSwatches.Children.Add(button);
        }

        foreach (var hex in Palette.FontColors)
        {
            var button = MakeSwatch(hex, 16);
            button.ToolTip = hex;
            AutomationProperties.SetName(button, "字色 " + hex);
            button.Click += (_, _) =>
            {
                if (Note != null)
                    Note.FontColor = hex;
                InkChip.IsChecked = false;
            };
            FgSwatches.Children.Add(button);
        }
    }

    private Button MakeSwatch(string hex, double size) => new()
    {
        Style = (Style)FindResource("SwatchButton"),
        Width = size,
        Height = size,
        Background = ColorUtil.Brush(hex),
        BorderBrush = ColorUtil.Brush("#D9CDBB"),
        BorderThickness = new Thickness(1),
        Tag = hex,
    };

    private void ApplyLayout()
    {
        if (Toggle == null)
            return;

        Toggle.Visibility = Collapsible ? Visibility.Visible : Visibility.Collapsed;
        if (!Collapsible)
        {
            Hint.Visibility = Visibility.Collapsed;
            Tools.Visibility = Visibility.Visible;
            Strip.BorderThickness = new Thickness(0);
            Strip.Padding = new Thickness(0);
            return;
        }

        Strip.BorderThickness = new Thickness(0, 0, 0, 1);
        Strip.Padding = new Thickness(8, 2, 6, 2);
        var open = Toggle.IsChecked == true;
        Tools.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        Hint.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        Toggle.Content = open ? "▴" : "▾";
    }

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
        var opacity = Store?.Settings.Opacity ?? NoteLimits.DefaultOpacity;
        OpacitySlider.Value = opacity;
        OpacityChip.Content = $"{opacity * 100:0}%";
        OpacityLabel.Text = $"透明度  {opacity * 100:0}%（全局）";
        PaperDot.Background = ColorUtil.Brush(note.Background, Palette.DefaultBackground);
        InkDot.Background = ColorUtil.Brush(note.FontColor, Palette.DefaultFont);
        PaperChip.ToolTip = Palette.Backgrounds.FirstOrDefault(item =>
            string.Equals(item.Background, note.Background, StringComparison.OrdinalIgnoreCase))?.Name ?? "纸色";
        MarkSwatches(BgSwatches, note.Background);
        MarkSwatches(FgSwatches, note.FontColor);
        ApplyChrome(note);
        _internal = false;
    }

    private void ApplyChrome(Note note)
    {
        var panel = Dark;
        var light = !panel && ColorUtil.IsLight(note.Background);
        Brush ink;
        Brush muted;
        Brush fill;
        Brush line;
        Brush chip;
        if (panel)
        {
            ink = (Brush)FindResource("TextBrush");
            muted = (Brush)FindResource("MutedBrush");
            fill = (Brush)FindResource("SurfaceBrush");
            line = (Brush)FindResource("LineBrush");
            chip = (Brush)FindResource("RaisedBrush");
            Strip.Background = Brushes.Transparent;
        }
        else if (light)
        {
            ink = ColorUtil.Brush("#3A3228");
            muted = ColorUtil.Brush("#9A8F80");
            fill = ColorUtil.Brush("#FBF6EE");
            line = ColorUtil.Brush("#D9CDBB");
            chip = fill;
            Strip.Background = ColorUtil.Brush(note.Background, Palette.DefaultBackground);
        }
        else
        {
            ink = ColorUtil.Brush("#E7EEF9");
            muted = ColorUtil.Brush("#8EA0BE");
            fill = ColorUtil.ShiftBrush(note.Background, 0.1);
            line = ColorUtil.Brush("#34506A");
            chip = fill;
            Strip.Background = ColorUtil.Brush(note.Background, Palette.DefaultBackground);
        }

        Strip.BorderBrush = line;
        Hint.Foreground = muted;
        Toggle.Foreground = ink;
        Toggle.Background = chip;
        Toggle.BorderBrush = line;
        BoldBox.Foreground = ink;
        BoldBox.Background = chip;
        BoldBox.BorderBrush = line;
        PaintChip(PaperChip, chip, line);
        PaintChip(InkChip, chip, line);
        PaintChip(OpacityChip, chip, line);
        OpacityChip.Foreground = ink;
        OpacityPopupBorder.Background = fill;
        OpacityPopupBorder.BorderBrush = line;
        OpacityLabel.Foreground = muted;
        PaperDot.BorderBrush = line;
        InkDot.BorderBrush = line;
        PaperCaret.Stroke = muted;
        InkCaret.Stroke = muted;
        FontBox.Foreground = ink;
        FontBox.Background = fill;
        FontBox.BorderBrush = line;
        SizeBox.Foreground = ink;
        SizeBox.Background = fill;
        SizeBox.BorderBrush = line;
        PaperPopupBorder.Background = fill;
        PaperPopupBorder.BorderBrush = line;
        InkPopupBorder.Background = fill;
        InkPopupBorder.BorderBrush = line;
    }

    private static void PaintChip(ToggleButton button, Brush background, Brush border)
    {
        button.Background = background;
        button.BorderBrush = border;
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
        if (!Collapsible)
            return;
        ApplyLayout();
        if (Toggle.IsChecked != true)
        {
            PaperChip.IsChecked = false;
            InkChip.IsChecked = false;
            OpacityChip.IsChecked = false;
        }
    }

    private void Strip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!Collapsible || Toggle.IsChecked == true)
            return;
        if (FindParent<ButtonBase>(e.OriginalSource as DependencyObject) != null)
            return;
        if (FindParent<ComboBox>(e.OriginalSource as DependencyObject) != null)
            return;
        Toggle.IsChecked = true;
    }

    private void PaperChip_Changed(object sender, RoutedEventArgs e)
    {
        if (PaperChip.IsChecked == true)
        {
            InkChip.IsChecked = false;
            OpacityChip.IsChecked = false;
            PaperPopup.IsOpen = true;
        }
        else
            PaperPopup.IsOpen = false;
    }

    private void InkChip_Changed(object sender, RoutedEventArgs e)
    {
        if (InkChip.IsChecked == true)
        {
            PaperChip.IsChecked = false;
            OpacityChip.IsChecked = false;
            InkPopup.IsOpen = true;
        }
        else
            InkPopup.IsOpen = false;
    }

    private void OpacityChip_Changed(object sender, RoutedEventArgs e)
    {
        if (OpacityChip.IsChecked == true)
        {
            PaperChip.IsChecked = false;
            InkChip.IsChecked = false;
            OpacityPopup.IsOpen = true;
        }
        else
            OpacityPopup.IsOpen = false;
    }

    private void OpacitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_internal || Store == null)
            return;
        Store.Settings.Opacity = OpacitySlider.Value;
        OpacityChip.Content = $"{Store.Settings.Opacity * 100:0}%";
        OpacityLabel.Text = $"透明度  {Store.Settings.Opacity * 100:0}%（全局）";
    }

    private void PaperPopup_Closed(object? sender, EventArgs e) => PaperChip.IsChecked = false;

    private void InkPopup_Closed(object? sender, EventArgs e) => InkChip.IsChecked = false;

    private void OpacityPopup_Closed(object? sender, EventArgs e) => OpacityChip.IsChecked = false;

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
