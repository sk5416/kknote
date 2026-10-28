using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KkNote.Converters;
using KkNote.Models;
using KkNote.Services;

namespace KkNote.Views;

public partial class ControlPanelWindow : Window
{
    private readonly NoteStore _store;
    private Note? _selected;
    private bool _confirmDelete;

    public ControlPanelWindow(NoteStore store)
    {
        _store = store;
        InitializeComponent();
        NoteList.ItemsSource = store.Notes;
        BuildSwatches();
        if (store.Notes.Count > 0)
            NoteList.SelectedIndex = 0;
        else
            ShowSelection(null);
    }

    public bool AllowClose { get; set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowEffects.ApplyRoundedDark(this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Topmost = false;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            New_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void BuildSwatches()
    {
        foreach (var entry in Palette.Backgrounds)
        {
            var button = MakeSwatch(entry.Background);
            button.ToolTip = entry.Name + "。选择后会搭配默认识字色，字色仍可再改。";
            button.Tag = entry;
            AutomationProperties.SetName(button, "底色" + entry.Name);
            AutomationProperties.SetAutomationId(button, "Bg_" + entry.Name);
            button.Click += (_, _) =>
            {
                if (_selected == null)
                    return;
                _selected.Background = entry.Background;
                _selected.FontColor = entry.FontColor;
                RefreshSwatches();
            };
            BgSwatches.Children.Add(button);
        }

        foreach (var hex in Palette.FontColors)
        {
            var button = MakeSwatch(hex);
            button.Width = 16;
            button.Height = 16;
            button.Tag = hex;
            button.ToolTip = hex;
            AutomationProperties.SetName(button, "字色 " + hex);
            AutomationProperties.SetAutomationId(button, "Fg_" + hex.TrimStart('#'));
            button.Click += (_, _) =>
            {
                if (_selected == null)
                    return;
                _selected.FontColor = hex;
                RefreshSwatches();
            };
            FgSwatches.Children.Add(button);
        }
    }

    private Button MakeSwatch(string hex)
    {
        return new Button
        {
            Style = (Style)FindResource("SwatchButton"),
            Background = ColorUtil.Brush(hex),
            BorderBrush = ColorUtil.Brush("#2A3A56"),
            BorderThickness = new Thickness(1),
        };
    }

    private void NoteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selected != null)
            _selected.PropertyChanged -= OnSelectedProperty;
        _selected = NoteList.SelectedItem as Note;
        if (_selected != null)
        {
            var normalized = FontCatalog.Normalize(_selected.FontFamily);
            if (!string.Equals(normalized, _selected.FontFamily, StringComparison.Ordinal))
                _selected.FontFamily = normalized;
            _selected.PropertyChanged += OnSelectedProperty;
        }

        ShowSelection(_selected);
        ResetDeleteConfirm();
        RefreshSwatches();
    }

    private void OnSelectedProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Note.Background) or nameof(Note.FontColor))
            RefreshSwatches();
    }

    private void ShowSelection(Note? note)
    {
        Editor.DataContext = note;
        Editor.Visibility = note == null ? Visibility.Collapsed : Visibility.Visible;
        EmptyHint.Visibility = note == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshSwatches()
    {
        foreach (Button button in BgSwatches.Children)
        {
            var entry = (PaletteEntry)button.Tag;
            var selected = _selected != null
                && string.Equals(_selected.Background, entry.Background, StringComparison.OrdinalIgnoreCase);
            MarkSwatch(button, selected);
        }

        foreach (Button button in FgSwatches.Children)
        {
            var hex = (string)button.Tag;
            var selected = _selected != null
                && string.Equals(_selected.FontColor, hex, StringComparison.OrdinalIgnoreCase);
            MarkSwatch(button, selected);
        }
    }

    private void MarkSwatch(Button button, bool selected)
    {
        button.BorderBrush = selected ? (Brush)FindResource("AccentBrush") : ColorUtil.Brush("#2A3A56");
        button.BorderThickness = new Thickness(selected ? 2 : 1);
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var note = _store.AddNew();
        NoteList.SelectedItem = note;
        NoteList.ScrollIntoView(note);
        TitleBox.Focus();
        TitleBox.SelectAll();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null)
            return;
        if (!_confirmDelete)
        {
            _confirmDelete = true;
            DeleteButton.Content = "确认删除";
            return;
        }

        var note = _selected;
        ResetDeleteConfirm();
        _store.Delete(note);
        if (NoteList.SelectedIndex < 0 && NoteList.Items.Count > 0)
            NoteList.SelectedIndex = 0;
    }

    private void ResetDeleteConfirm()
    {
        _confirmDelete = false;
        if (DeleteButton != null)
            DeleteButton.Content = "删除";
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => Close();
}
