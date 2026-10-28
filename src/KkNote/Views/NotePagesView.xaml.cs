using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
            _index = Math.Clamp(note.CurrentPageIndex, 0, Math.Max(0, note.Pages.Count - 1));
            Hook(note);
        }

        ApplyChrome();
        ShowCurrent();
        RebuildDeck();
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
            if (e.PropertyName == nameof(Note.Background))
                RebuildDeck();
        }
        else if (e.PropertyName == nameof(Note.SpreadPages))
        {
            ShowCurrent();
            RebuildDeck();
        }
        else if (e.PropertyName == nameof(Note.CurrentPageIndex) && _note != null)
        {
            var idx = Math.Clamp(_note.CurrentPageIndex, 0, Math.Max(0, _note.Pages.Count - 1));
            if (idx == _index)
                return;
            _index = idx;
            ShowCurrent();
            RebuildDeck();
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
        SyncIndexToNote();
        ShowCurrent();
        RebuildDeck();
    }

    private void OnPageProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotePage.Text) && _note?.SpreadPages == true)
            RebuildSpreadTabs();
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
        var foreground = ColorUtil.Brush(_note.FontColor, Palette.DefaultFont);
        Body.Foreground = foreground;
        Body.CaretBrush = foreground;
        Placeholder.Foreground = Fade(_note.FontColor);
        FrontPaper.Background = ColorUtil.Brush(_note.Background, Palette.DefaultBackground);
        var light = ColorUtil.IsLight(_note.Background);
        FrontPaper.BorderBrush = light ? ColorUtil.Brush("#E2D6C4") : ColorUtil.Brush("#34506A");
        var ink = light ? ColorUtil.Brush("#3A3228") : ColorUtil.Brush("#E7EEF9");
        var muted = light ? ColorUtil.Brush("#9A8F80") : ColorUtil.Brush("#8EA0BE");
        PageBadge.Foreground = muted;
        SpreadToggle.Foreground = ink;
        AddButton.Foreground = ink;
        RemoveButton.Foreground = ink;
        if (light)
        {
            SpreadToggle.Background = ColorUtil.Brush("#FBF6EE");
            AddButton.Background = ColorUtil.Brush("#FBF6EE");
            RemoveButton.Background = ColorUtil.Brush("#FBF6EE");
        }
        else
        {
            var raised = ColorUtil.ShiftBrush(_note.Background, 0.12);
            SpreadToggle.Background = raised;
            AddButton.Background = raised;
            RemoveButton.Background = raised;
        }
    }

    private void ShowCurrent()
    {
        _internal = true;
        var text = Current?.Text ?? "";
        if (Body.Text != text)
            Body.Text = text;
        Placeholder.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var count = _note?.Pages.Count ?? 0;
        PageBadge.Text = count <= 1 ? "" : $"{_index + 1} / {count}";
        AddButton.IsEnabled = _note != null;
        RemoveButton.IsEnabled = _note != null && count > 1;
        Body.IsEnabled = _note != null;
        if (SpreadToggle.IsChecked != (_note?.SpreadPages == true))
            SpreadToggle.IsChecked = _note?.SpreadPages == true;
        _internal = false;
    }

    private void RebuildDeck()
    {
        PeekLayer.Children.Clear();
        SpreadTabs.Children.Clear();
        if (_note == null)
            return;

        var spread = _note.SpreadPages;
        SpreadStrip.Visibility = spread ? Visibility.Visible : Visibility.Collapsed;
        if (spread)
        {
            FrontPaper.Margin = new Thickness(0);
            RebuildSpreadTabs();
            return;
        }

        var count = _note.Pages.Count;
        var stacked = count > 1;
        FrontPaper.Margin = stacked ? new Thickness(0, 0, 28, 24) : new Thickness(2, 2, 6, 6);
        if (!stacked)
            return;

        var behind = Math.Min(count - 1, 3);
        double[] angles = [4, 8, 12];
        double[] shift = [8, 14, 20];
        for (var i = behind; i >= 1; i--)
        {
            var pageIndex = (_index + i) % count;
            var depth = behind - i;
            var sheet = CreatePeekSheet(pageIndex, angles[Math.Min(depth, 2)], shift[Math.Min(depth, 2)], depth);
            PeekLayer.Children.Add(sheet);
        }
    }

    private Border CreatePeekSheet(int pageIndex, double angle, double offset, int depth)
    {
        var light = ColorUtil.IsLight(_note!.Background);
        var amount = light ? -0.06 - depth * 0.05 : 0.08 + depth * 0.05;
        var border = new Border
        {
            Background = ColorUtil.ShiftBrush(_note.Background, amount),
            BorderBrush = light ? ColorUtil.Brush("#D9CDBB") : ColorUtil.Brush("#4A6080"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Cursor = Cursors.Hand,
            Tag = pageIndex,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new TransformGroup
            {
                Children =
                {
                    new RotateTransform(angle),
                    new TranslateTransform(offset, offset * 0.75),
                },
            },
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 1,
                Direction = 250,
                Opacity = 0.12,
                Color = Color.FromRgb(0x6B, 0x5A, 0x40),
            },
        };
        var label = new TextBlock
        {
            Text = (pageIndex + 1).ToString(),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = light ? ColorUtil.Brush("#8A7B68") : ColorUtil.Brush("#8EA0BE"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            IsHitTestVisible = false,
        };
        border.Child = label;
        border.MouseLeftButtonDown += Peek_MouseLeftButtonDown;
        AutomationProperties.SetName(border, "第" + (pageIndex + 1) + "页");
        AutomationProperties.SetAutomationId(border, "PeekPage" + (pageIndex + 1));
        return border;
    }

    private void Peek_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int pageIndex })
            return;
        SelectIndex(pageIndex);
        e.Handled = true;
    }

    private void RebuildSpreadTabs()
    {
        SpreadTabs.Children.Clear();
        if (_note == null)
            return;
        var light = ColorUtil.IsLight(_note.Background);
        for (var i = 0; i < _note.Pages.Count; i++)
        {
            var selected = i == _index;
            var tab = new Button
            {
                Tag = i,
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(10, 6, 10, 6),
                MinWidth = 56,
                Cursor = Cursors.Hand,
                Style = (Style)FindResource("PaperButton"),
                Background = selected
                    ? (light ? ColorUtil.Brush("#EFE4D2") : ColorUtil.ShiftBrush(_note.Background, 0.18))
                    : (light ? ColorUtil.Brush("#FBF6EE") : ColorUtil.ShiftBrush(_note.Background, 0.08)),
                BorderBrush = selected ? ColorUtil.Brush("#C4A574") : ColorUtil.Brush("#D9CDBB"),
                Foreground = light ? ColorUtil.Brush("#3A3228") : ColorUtil.Brush("#E7EEF9"),
                Content = $"{i + 1}  {Snippet(_note.Pages[i].Text)}",
            };
            var captured = i;
            tab.Click += (_, _) => SelectIndex(captured);
            AutomationProperties.SetName(tab, "第" + (i + 1) + "页");
            AutomationProperties.SetAutomationId(tab, "TabPage" + (i + 1));
            SpreadTabs.Children.Add(tab);
            if (selected)
                tab.Loaded += Tab_BringIntoView;
        }
    }

    private void SelectIndex(int index)
    {
        if (_note == null || index < 0 || index >= _note.Pages.Count || index == _index)
            return;
        _index = index;
        SyncIndexToNote();
        ShowCurrent();
        RebuildDeck();
        Body.Focus();
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

    private void SpreadToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_internal || _note == null)
            return;
        _note.SpreadPages = SpreadToggle.IsChecked == true;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_note == null)
            return;
        _index = _note.Pages.Count;
        _note.Pages.Add(NotePage.Create());
        SyncIndexToNote();
        Body.Focus();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_note == null || _note.Pages.Count <= 1 || _index < 0 || _index >= _note.Pages.Count)
            return;
        _note.Pages.RemoveAt(_index);
    }

    private static void Tab_BringIntoView(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;
        element.Loaded -= Tab_BringIntoView;
        element.BringIntoView();
    }

    private void SyncIndexToNote()
    {
        if (_note != null && _note.CurrentPageIndex != _index)
            _note.CurrentPageIndex = _index;
    }

    private static string Snippet(string? text)
    {
        var line = (text ?? "").Replace("\r", "").Split('\n')[0].Trim();
        if (line.Length > 6)
            line = line[..6] + "…";
        return string.IsNullOrEmpty(line) ? "空白页" : line;
    }

    private static SolidColorBrush Fade(string? hex)
    {
        if (!ColorUtil.TryParse(hex, out var color))
            color = Color.FromRgb(0x9A, 0x8F, 0x80);
        return ColorUtil.From(Color.FromArgb(150, color.R, color.G, color.B));
    }
}
