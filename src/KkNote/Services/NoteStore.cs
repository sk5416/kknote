using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using KkNote.Models;

namespace KkNote.Services;

public sealed class NoteStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly SynchronizationContext? _context;
    private readonly HashSet<Note> _watched = [];
    private readonly HashSet<TodoItem> _watchedTodos = [];
    private readonly object _gate = new();
    private readonly object _saveLock = new();
    private Timer? _timer;
    private bool _suppress;
    private bool _disposed;

    private NoteStore(string path, NoteDocument document)
    {
        FilePath = path;
        Settings = document.Settings ?? new AppSettings();
        if (string.IsNullOrWhiteSpace(Settings.Hotkey))
            Settings.Hotkey = "Ctrl+Alt+N";
        _context = SynchronizationContext.Current;
        Notes = [];
        Todos = [];
        Notes.CollectionChanged += OnCollectionChanged;
        Todos.CollectionChanged += OnTodosChanged;
        Settings.PropertyChanged += OnSettingsProperty;
        _suppress = true;
        foreach (var todo in document.Todos ?? [])
        {
            NormalizeTodo(todo);
            Todos.Add(todo);
        }
        foreach (var note in Flatten(document.Notes ?? []))
        {
            Normalize(note);
            Notes.Add(note);
        }
        PruneTodoRefs();
        if (Settings.SpreadPages)
        {
            foreach (var note in Notes)
            {
                if (note.PinnedToDesktop)
                    note.Detached = true;
            }
            Settings.SpreadPages = false;
        }
        _suppress = false;
    }

    public string FilePath { get; }
    public AppSettings Settings { get; }
    public ObservableCollection<Note> Notes { get; }
    public ObservableCollection<TodoItem> Todos { get; }

    public static string DefaultPath
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "KkNote", "notes.json");
        }
    }

    public static NoteStore Load(string? path = null)
    {
        path ??= DefaultPath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (!File.Exists(path))
        {
            var created = new NoteStore(path, new NoteDocument());
            created.SaveNow();
            return created;
        }

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<NoteDocument>(json, JsonOptions) ?? new NoteDocument();
            document.Settings ??= new AppSettings();
            document.Notes ??= [];
            return new NoteStore(path, document);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            TryBackup(path);
            var recovered = new NoteStore(path, new NoteDocument());
            recovered.SaveNow();
            return recovered;
        }
    }

    public Note AddNew(string kind = NoteKind.Text)
    {
        var index = Notes.Count;
        var note = Note.CreateNew(120 + (index % 8) * 28, 80 + (index % 8) * 28, kind);
        Notes.Add(note);
        return note;
    }

    public void Delete(Note note) => Notes.Remove(note);

    public TodoItem AddTodo(string text, DateTime? due = null)
    {
        var item = TodoItem.Create(text.Trim(), due);
        Todos.Add(item);
        return item;
    }

    public void DeleteTodo(TodoItem todo)
    {
        Todos.Remove(todo);
        foreach (var note in Notes)
        {
            if (note.TodoIds.RemoveAll(id => id == todo.Id) > 0)
                note.TouchTodos();
        }
    }

    public void AttachTodo(Note note, string todoId)
    {
        if (string.IsNullOrWhiteSpace(todoId) || note.TodoIds.Contains(todoId))
            return;
        note.TodoIds.Add(todoId);
        note.TouchTodos();
    }

    public void DetachTodo(Note note, string todoId)
    {
        if (note.TodoIds.RemoveAll(id => id == todoId) > 0)
            note.TouchTodos();
    }

    public TodoItem? FindTodo(string id)
        => Todos.FirstOrDefault(item => item.Id == id);

    public IReadOnlyList<TodoItem> TodosOn(Note note)
    {
        var map = Todos.ToDictionary(item => item.Id, item => item);
        var list = new List<TodoItem>();
        foreach (var id in note.TodoIds)
        {
            if (map.TryGetValue(id, out var item))
                list.Add(item);
        }

        return list;
    }

    public IReadOnlyList<TodoItem> SortedPool(bool includeDone)
    {
        return Todos
            .Where(item => includeDone || !item.Done)
            .OrderBy(item => item.Done)
            .ThenBy(item => item.Due == null)
            .ThenBy(item => item.Due)
            .ThenBy(item => item.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<TodoItem> ManagerList(bool includeDone)
    {
        IEnumerable<TodoItem> items = Settings.TodoManualOrder ? Todos : SortedPool(true);
        if (!includeDone)
            items = items.Where(item => !item.Done);
        return items.ToList();
    }

    public void MovePageTodo(Note note, string sourceId, string targetId)
    {
        var ids = note.TodoIds.ToList();
        var from = ids.IndexOf(sourceId);
        var to = ids.IndexOf(targetId);
        if (from < 0 || to < 0 || from == to)
            return;
        ids.RemoveAt(from);
        ids.Insert(to, sourceId);
        note.TodoIds = ids;
    }

    public void MoveManagerTodo(string sourceId, string targetId)
    {
        MaterializeTodoOrder();
        var from = IndexOfTodo(sourceId);
        var to = IndexOfTodo(targetId);
        if (from < 0 || to < 0 || from == to)
            return;
        Todos.Move(from, to);
    }

    private void MaterializeTodoOrder()
    {
        if (Settings.TodoManualOrder)
            return;
        var ordered = SortedPool(true);
        for (var i = 0; i < ordered.Count; i++)
        {
            var current = IndexOfTodo(ordered[i].Id);
            if (current >= 0 && current != i)
                Todos.Move(current, i);
        }

        Settings.TodoManualOrder = true;
    }

    private int IndexOfTodo(string id)
    {
        for (var i = 0; i < Todos.Count; i++)
        {
            if (Todos[i].Id == id)
                return i;
        }

        return -1;
    }

    public event Action<Note>? ActivateRequested;
    public event Action? OpenPanelRequested;
    public event Action? OpenTodosRequested;

    public void RequestActivate(Note note) => ActivateRequested?.Invoke(note);

    public void RequestOpenPanel() => OpenPanelRequested?.Invoke();

    public void RequestOpenTodos() => OpenTodosRequested?.Invoke();

    public IReadOnlyList<Note> DesktopPages()
        => Notes.Where(item => item.PinnedToDesktop).ToList();

    public IReadOnlyList<Note> StackedPages()
        => Notes.Where(item => item.PinnedToDesktop && !item.Detached).ToList();

    public void ScheduleSave()
    {
        if (_suppress || _disposed)
            return;
        if (_context == null)
        {
            SaveNow();
            return;
        }

        lock (_gate)
        {
            if (_disposed)
                return;
            _timer ??= new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
            _timer.Change(400, Timeout.Infinite);
        }
    }

    public void SaveNow()
    {
        lock (_saveLock)
        {
            if (_disposed)
                return;
            Write();
        }
    }

    public void Dispose()
    {
        Settings.PropertyChanged -= OnSettingsProperty;
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }

        lock (_saveLock)
        {
            if (!_disposed)
                Write();
            _disposed = true;
        }
    }

    private void OnTimer(object? state)
    {
        if (_disposed)
            return;
        var context = _context;
        if (context != null)
            context.Post(_ => SaveNow(), null);
        else
            SaveNow();
    }

    private void Write()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var document = new NoteDocument
        {
            Settings = new AppSettings
            {
                Hotkey = Settings.Hotkey,
                SpreadPages = Settings.SpreadPages,
                StackCurrentId = Settings.StackCurrentId,
                StackX = Settings.StackX,
                StackY = Settings.StackY,
                StackWidth = Settings.StackWidth,
                StackHeight = Settings.StackHeight,
                StackAlwaysOnTop = Settings.StackAlwaysOnTop,
                StackCollapsed = Settings.StackCollapsed,
                DateFormat = Settings.DateFormat,
                DueSoonDays = Settings.DueSoonDays,
                Opacity = Settings.Opacity,
                TodoManualOrder = Settings.TodoManualOrder,
            },
            Notes = Notes.Select(CopyForSave).ToList(),
            Todos = Todos.Select(CopyTodo).ToList(),
        };
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, json);
        if (File.Exists(FilePath))
            File.Replace(temporary, FilePath, null);
        else
            File.Move(temporary, FilePath);
    }

    private void OnTodosChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (TodoItem todo in e.NewItems)
                WatchTodo(todo);
        }

        if (e.OldItems != null)
        {
            foreach (TodoItem todo in e.OldItems)
                UnwatchTodo(todo);
        }

        if (!_suppress)
            ScheduleSave();
    }

    private void WatchTodo(TodoItem todo)
    {
        if (!_watchedTodos.Add(todo))
            return;
        todo.PropertyChanged += OnTodoProperty;
    }

    private void UnwatchTodo(TodoItem todo)
    {
        if (!_watchedTodos.Remove(todo))
            return;
        todo.PropertyChanged -= OnTodoProperty;
    }

    private void OnTodoProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (!_suppress)
            ScheduleSave();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (Note note in e.NewItems)
                Watch(note);
        }

        if (e.OldItems != null)
        {
            foreach (Note note in e.OldItems)
                Unwatch(note);
        }

        if (!_suppress)
            ScheduleSave();
    }

    private void Watch(Note note)
    {
        if (!_watched.Add(note))
            return;
        note.PropertyChanged += OnNoteProperty;
    }

    private void Unwatch(Note note)
    {
        if (!_watched.Remove(note))
            return;
        note.PropertyChanged -= OnNoteProperty;
    }

    private void OnNoteProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (!_suppress)
            ScheduleSave();
    }

    private void OnSettingsProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (!_suppress)
            ScheduleSave();
    }

    private static List<Note> Flatten(IEnumerable<Note> source)
    {
        var result = new List<Note>();
        foreach (var note in source)
        {
            var pages = note.Pages;
            if (pages is { Count: > 0 })
            {
                for (var i = 0; i < pages.Count; i++)
                {
                    var item = i == 0 ? note : CloneShell(note, i);
                    if (string.IsNullOrEmpty(item.Text))
                        item.Text = pages[i].Text ?? "";
                    item.Pages = null;
                    result.Add(item);
                }
            }
            else
            {
                note.Pages = null;
                note.Text ??= "";
                result.Add(note);
            }
        }

        return result;
    }

    private static Note CloneShell(Note source, int index) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Title = string.IsNullOrWhiteSpace(source.Title) ? $"第{index + 1}页" : $"{source.DisplayTitle} · {index + 1}",
        PinnedToDesktop = source.PinnedToDesktop,
        AlwaysOnTop = source.AlwaysOnTop,
        Background = source.Background,
        FontFamily = source.FontFamily,
        FontSize = source.FontSize,
        FontColor = source.FontColor,
        Bold = source.Bold,
        X = source.X + index * 24,
        Y = source.Y + index * 24,
        Width = source.Width,
        Height = source.Height,
    };

    private static Note CopyForSave(Note note) => new()
    {
        Id = note.Id,
        Kind = note.Kind,
        Title = note.Title,
        Text = note.Text,
        TodoIds = [.. note.TodoIds],
        PinnedToDesktop = note.PinnedToDesktop,
        Detached = note.Detached,
        AlwaysOnTop = note.AlwaysOnTop,
        Background = note.Background,
        FontFamily = note.FontFamily,
        FontSize = note.FontSize,
        FontColor = note.FontColor,
        Bold = note.Bold,
        Opacity = note.Opacity,
        Collapsed = note.Collapsed,
        X = note.X,
        Y = note.Y,
        Width = note.Width,
        Height = note.Height,
        Pages = null,
    };

    private static TodoItem CopyTodo(TodoItem todo) => new()
    {
        Id = todo.Id,
        Text = todo.Text,
        Due = todo.Due,
        Done = todo.Done,
        CreatedAt = todo.CreatedAt,
    };

    private static void Normalize(Note note)
    {
        if (string.IsNullOrWhiteSpace(note.Id))
            note.Id = Guid.NewGuid().ToString("N");
        note.Kind = string.Equals(note.Kind, NoteKind.Todo, StringComparison.OrdinalIgnoreCase)
            ? NoteKind.Todo
            : NoteKind.Text;
        note.Text ??= "";
        note.TodoIds ??= [];
        note.Pages = null;
        note.FontSize = NoteLimits.SnapFontSize(note.FontSize);
        note.Opacity = NoteLimits.ClampOpacity(note.Opacity);
        if (note.Width < NoteLimits.MinWidth)
            note.Width = NoteLimits.DefaultWidth;
        if (note.Height < NoteLimits.MinHeight)
            note.Height = NoteLimits.DefaultHeight;
    }

    private static void NormalizeTodo(TodoItem todo)
    {
        if (string.IsNullOrWhiteSpace(todo.Id))
            todo.Id = Guid.NewGuid().ToString("N");
        todo.Text ??= "";
        if (todo.CreatedAt == default)
            todo.CreatedAt = DateTime.Now;
    }

    private void PruneTodoRefs()
    {
        var known = Todos.Select(item => item.Id).ToHashSet();
        foreach (var note in Notes)
        {
            var next = note.TodoIds.Where(known.Contains).Distinct().ToList();
            if (next.Count != note.TodoIds.Count)
                note.TodoIds = next;
        }
    }

    private static void TryBackup(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Copy(path, path + ".bak", overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
