using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KkNote.Converters;
using KkNote.Models;
using KkNote.Services;

namespace KkNote.Views;

public partial class TodoListView : UserControl
{
    public static readonly DependencyProperty StoreProperty = DependencyProperty.Register(
        nameof(Store), typeof(NoteStore), typeof(TodoListView),
        new PropertyMetadata(null, (d, e) => ((TodoListView)d).OnStoreChanged((NoteStore?)e.OldValue, (NoteStore?)e.NewValue)));

    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note), typeof(Note), typeof(TodoListView),
        new PropertyMetadata(null, (d, e) => ((TodoListView)d).OnNoteChanged((Note?)e.OldValue, (Note?)e.NewValue)));

    public static readonly DependencyProperty DarkProperty = DependencyProperty.Register(
        nameof(Dark), typeof(bool), typeof(TodoListView),
        new PropertyMetadata(false, (d, _) => ((TodoListView)d).ApplyChrome()));

    private readonly List<TodoItem> _hooked = [];
    private bool _placeholder;

    public TodoListView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            DayClock.DayRolled += OnDayRolled;
            ShowPlaceholder();
            Rebuild();
            ApplyChrome();
        };
        Unloaded += (_, _) => DayClock.DayRolled -= OnDayRolled;
    }

    private void OnDayRolled() => Dispatcher.BeginInvoke(Rebuild);

    public NoteStore? Store
    {
        get => (NoteStore?)GetValue(StoreProperty);
        set => SetValue(StoreProperty, value);
    }

    public Note? Note
    {
        get => (Note?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public bool Dark
    {
        get => (bool)GetValue(DarkProperty);
        set => SetValue(DarkProperty, value);
    }

    public void Refresh() => Rebuild();

    private void OnStoreChanged(NoteStore? oldStore, NoteStore? store)
    {
        if (oldStore != null)
        {
            oldStore.Todos.CollectionChanged -= OnTodosChanged;
            oldStore.Settings.PropertyChanged -= OnSettingsChanged;
            foreach (var todo in _hooked.ToList())
                Unhook(todo);
        }

        if (store != null)
        {
            store.Todos.CollectionChanged += OnTodosChanged;
            store.Settings.PropertyChanged += OnSettingsChanged;
            foreach (var todo in store.Todos)
                Hook(todo);
        }

        Rebuild();
        ApplyChrome();
    }

    private void OnNoteChanged(Note? oldNote, Note? note)
    {
        if (oldNote != null)
            oldNote.PropertyChanged -= OnNoteProperty;
        if (note != null)
            note.PropertyChanged += OnNoteProperty;
        Rebuild();
        ApplyChrome();
    }

    private void OnNoteProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Note.TodoIds) or nameof(Note.Background) or nameof(Note.FontColor)
            or nameof(Note.FontFamily) or nameof(Note.FontSize) or nameof(Note.Bold) or nameof(Note.Kind))
        {
            Rebuild();
            ApplyChrome();
        }
    }

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
        if (e.PropertyName == nameof(TodoItem.Done) && sender is TodoItem)
            return;
    }

    private void Rebuild()
    {
        if (Rows == null || Store == null || Note == null)
        {
            Rows?.Children.Clear();
            return;
        }

        var items = Store.TodosOn(Note);
        Rows.Children.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            var row = new TodoRow
            {
                Store = Store,
                Item = items[i],
                HostPage = Note,
                Index = i + 1,
                Dark = Dark,
            };
            row.TodoDropped += OnTodoDropped;
            Rows.Children.Add(row);
        }
    }

    private void OnTodoDropped(string sourceId, string targetId)
    {
        if (Store == null || Note == null)
            return;
        Store.MovePageTodo(Note, sourceId, targetId);
    }

    private void ApplyChrome()
    {
        if (AddBox == null || PickButton == null)
            return;
        var note = Note;
        var light = note == null || ColorUtil.IsLight(note.Background);
        AddBox.Style = (Style)FindResource("TodoAddBox");
        AddBox.Height = 30;
        AddBox.MinHeight = 30;
        AddBox.Padding = new Thickness(8, 1, 8, 1);
        if (Dark)
        {
            PickButton.Style = (Style)FindResource("GhostButton");
            PickButton.Foreground = (Brush)FindResource("TextBrush");
        }
        else
        {
            AddBox.Background = light || note == null
                ? ColorUtil.Brush("#FBF6EE")
                : ColorUtil.ShiftBrush(note.Background, 0.1);
            AddBox.Foreground = note == null ? ColorUtil.Brush("#2A3340") : ColorUtil.Brush(note.FontColor, Palette.DefaultFont);
            AddBox.BorderBrush = light ? ColorUtil.Brush("#D9CDBB") : ColorUtil.Brush("#34506A");
            AddBox.CaretBrush = AddBox.Foreground;
            PickButton.Style = (Style)FindResource("PaperButton");
        }

        ShowPlaceholder();
    }

    private void ShowPlaceholder()
    {
        if (AddBox == null || AddBox.IsKeyboardFocused)
            return;
        if (!_placeholder && AddBox.Text.Length > 0 && AddBox.Text != "添加待办…")
            return;
        _placeholder = true;
        AddBox.Text = "添加待办…";
        AddBox.Opacity = 0.55;
    }

    private void AddBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        CommitAdd();
        e.Handled = true;
    }

    private void AddBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AddBox.Text) || _placeholder)
            ShowPlaceholder();
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

    private void CommitAdd()
    {
        if (Store == null || Note == null)
            return;
        var text = _placeholder ? "" : AddBox.Text.Trim();
        if (text.Length == 0 || text == "添加待办…")
            return;
        var item = Store.AddTodo(text);
        Store.AttachTodo(Note, item.Id);
        _placeholder = false;
        AddBox.Text = "";
        ShowPlaceholder();
    }

    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        if (Store == null || Note == null)
            return;
        var pick = new TodoPickWindow(Store, Note) { Owner = Window.GetWindow(this) };
        pick.ShowDialog();
    }
}
