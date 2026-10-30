using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        ApplyBodyType();
        ResetDeleteConfirm();
    }

    private void OnSelectedProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Note.Bold))
            ApplyBodyType();
    }

    private void ShowSelection(Note? note)
    {
        Editor.DataContext = note;
        StyleBar.Store = _store;
        StyleBar.Note = note;
        Editor.Visibility = note == null ? Visibility.Collapsed : Visibility.Visible;
        EmptyHint.Visibility = note == null ? Visibility.Visible : Visibility.Collapsed;
        var todo = note is { IsTodo: true };
        TextHost.Visibility = todo ? Visibility.Collapsed : Visibility.Visible;
        TodoHost.Visibility = todo ? Visibility.Visible : Visibility.Collapsed;
        TodoHost.Store = _store;
        TodoHost.Note = note;
    }

    private void ApplyBodyType()
    {
        if (_selected == null)
            return;
        BodyBox.FontWeight = _selected.Bold ? FontWeights.Bold : FontWeights.Normal;
    }

    private void New_Click(object sender, RoutedEventArgs e) => CreatePage(NoteKind.Text);

    private void NewTodo_Click(object sender, RoutedEventArgs e) => CreatePage(NoteKind.Todo);

    private void OpenTodos_Click(object sender, RoutedEventArgs e) => _store.RequestOpenTodos();

    private void CreatePage(string kind)
    {
        var note = _store.AddNew(kind);
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
