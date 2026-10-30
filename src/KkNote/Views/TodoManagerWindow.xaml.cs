using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KkNote.Models;
using KkNote.Services;

namespace KkNote.Views;

public partial class TodoManagerWindow : Window
{
    private readonly NoteStore _store;
    private readonly List<TodoItem> _hooked = [];
    private bool _internal;
    private DateTime? _addDue;
    private bool _placeholder = true;

    public TodoManagerWindow(NoteStore store)
    {
        _store = store;
        InitializeComponent();
        for (var i = TodoDate.MinSoonDays; i <= TodoDate.MaxSoonDays; i++)
            SoonBox.Items.Add(i);
        _internal = true;
        SelectFormat(_store.Settings.DateFormat);
        SoonBox.SelectedItem = _store.Settings.DueSoonDays;
        _internal = false;
        ShowAddPlaceholder();
        _store.Todos.CollectionChanged += OnTodosChanged;
        _store.Settings.PropertyChanged += OnSettingsChanged;
        foreach (var todo in _store.Todos)
            Hook(todo);
        Rebuild();
        DayClock.DayRolled += OnDayRolled;
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

        DayClock.DayRolled -= OnDayRolled;
        _store.Todos.CollectionChanged -= OnTodosChanged;
        _store.Settings.PropertyChanged -= OnSettingsChanged;
        foreach (var todo in _hooked.ToList())
            Unhook(todo);
        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
    }

    private void OnDayRolled() => Dispatcher.BeginInvoke(Rebuild);

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.DateFormat) or nameof(AppSettings.DueSoonDays))
            Rebuild();
    }

    private void OnTodosChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (TodoItem todo in e.NewItems)
                Hook(todo);
        }

        if (e.OldItems != null)
        {
            foreach (TodoItem todo in e.OldItems)
                Unhook(todo);
        }

        Rebuild();
    }

    private void Hook(TodoItem todo)
    {
        if (_hooked.Contains(todo))
            return;
        _hooked.Add(todo);
        todo.PropertyChanged += OnTodoProperty;
    }

    private void Unhook(TodoItem todo)
    {
        if (!_hooked.Remove(todo))
            return;
        todo.PropertyChanged -= OnTodoProperty;
    }

    private void OnTodoProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TodoItem.Done) or nameof(TodoItem.Due))
            Rebuild();
    }

    private void Rebuild()
    {
        var showDone = ShowDoneBox.IsChecked == true;
        var items = _store.ManagerList(showDone);
        Rows.Children.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            var row = new TodoRow
            {
                Store = _store,
                Item = items[i],
                Index = i + 1,
                Dark = true,
                PoolDelete = true,
            };
            row.TodoDropped += OnTodoDropped;
            Rows.Children.Add(row);
        }

        EmptyHint.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnTodoDropped(string sourceId, string targetId)
    {
        _store.MoveManagerTodo(sourceId, targetId);
        Rebuild();
    }

    private void SelectFormat(string format)
    {
        foreach (ComboBoxItem item in FormatBox.Items)
        {
            if (string.Equals(item.Tag as string, format, StringComparison.OrdinalIgnoreCase))
            {
                FormatBox.SelectedItem = item;
                return;
            }
        }

        FormatBox.SelectedIndex = 0;
    }

    private void FormatBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_internal || FormatBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
            return;
        _store.Settings.DateFormat = tag;
    }

    private void SoonBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_internal || SoonBox.SelectedItem is not int days)
            return;
        _store.Settings.DueSoonDays = days;
    }

    private void ShowDone_Changed(object sender, RoutedEventArgs e) => Rebuild();

    private bool _pickingAddDate;

    private void AddDateButton_Click(object sender, RoutedEventArgs e)
    {
        _pickingAddDate = true;
        AddPicker.SelectedDate = _addDue;
        _pickingAddDate = false;
        AddDatePopup.IsOpen = true;
    }

    private void AddPicker_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_pickingAddDate || AddPicker.SelectedDate == null)
            return;
        _addDue = AddPicker.SelectedDate.Value.Date;
        AddDateButton.Content = TodoDate.Format(_addDue, _store.Settings.DateFormat);
        AddDatePopup.IsOpen = false;
    }

    private void ClearAddDate_Click(object sender, RoutedEventArgs e)
    {
        _addDue = null;
        AddPicker.SelectedDate = null;
        AddDateButton.Content = "+";
        AddDatePopup.IsOpen = false;
    }

    private void AddBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Add_Click(sender, e);
            e.Handled = true;
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (AddPicker.SelectedDate != null)
            _addDue = AddPicker.SelectedDate.Value.Date;
        var text = _placeholder ? "" : AddBox.Text.Trim();
        if (text.Length == 0 || text == "添加待办…")
            return;
        _store.AddTodo(text, _addDue);
        _addDue = null;
        AddPicker.SelectedDate = null;
        AddDateButton.Content = "+";
        AddBox.Text = "";
        ShowAddPlaceholder();
    }

    private void ShowAddPlaceholder()
    {
        _placeholder = true;
        AddBox.Text = "添加待办…";
        AddBox.Opacity = 0.55;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (e.NewFocus != AddBox)
            return;
        if (_placeholder)
        {
            _placeholder = false;
            AddBox.Text = "";
            AddBox.Opacity = 1;
        }
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => Close();
}
