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
        Notes.CollectionChanged += OnCollectionChanged;
        _suppress = true;
        foreach (var note in document.Notes ?? [])
        {
            Normalize(note);
            Notes.Add(note);
        }
        _suppress = false;
    }

    public string FilePath { get; }
    public AppSettings Settings { get; }
    public ObservableCollection<Note> Notes { get; }

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

    public Note AddNew()
    {
        var index = Notes.Count;
        var note = Note.CreateNew(120 + (index % 8) * 28, 80 + (index % 8) * 28);
        Notes.Add(note);
        return note;
    }

    public void Delete(Note note) => Notes.Remove(note);

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
            Settings = new AppSettings { Hotkey = Settings.Hotkey },
            Notes = Notes.ToList(),
        };
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, json);
        if (File.Exists(FilePath))
            File.Replace(temporary, FilePath, null);
        else
            File.Move(temporary, FilePath);
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
        note.Pages.CollectionChanged += OnPageCollection;
        foreach (var page in note.Pages)
            page.PropertyChanged += OnPageProperty;
    }

    private void Unwatch(Note note)
    {
        if (!_watched.Remove(note))
            return;
        note.PropertyChanged -= OnNoteProperty;
        note.Pages.CollectionChanged -= OnPageCollection;
        foreach (var page in note.Pages)
            page.PropertyChanged -= OnPageProperty;
    }

    private void OnPageCollection(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (NotePage page in e.NewItems)
                page.PropertyChanged += OnPageProperty;
        }

        if (e.OldItems != null)
        {
            foreach (NotePage page in e.OldItems)
                page.PropertyChanged -= OnPageProperty;
        }

        if (!_suppress)
            ScheduleSave();
    }

    private void OnNoteProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (!_suppress)
            ScheduleSave();
    }

    private void OnPageProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (!_suppress)
            ScheduleSave();
    }

    private static void Normalize(Note note)
    {
        if (string.IsNullOrWhiteSpace(note.Id))
            note.Id = Guid.NewGuid().ToString("N");
        note.EnsurePage();
        foreach (var page in note.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.Id))
                page.Id = Guid.NewGuid().ToString("N");
            page.Text ??= "";
        }

        note.FontSize = NoteLimits.SnapFontSize(note.FontSize);
        if (note.Width < NoteLimits.MinWidth)
            note.Width = NoteLimits.DefaultWidth;
        if (note.Height < NoteLimits.MinHeight)
            note.Height = NoteLimits.DefaultHeight;
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
