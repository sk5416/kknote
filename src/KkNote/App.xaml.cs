using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using KkNote.Models;
using KkNote.Services;
using KkNote.Views;

namespace KkNote;

public partial class App : Application
{
    private readonly Dictionary<string, StickyWindow> _stickies = [];
    private SingleInstance? _single;
    private NoteStore? _store;
    private TrayService? _tray;
    private HotkeyService? _hotkey;
    private ControlPanelWindow? _panel;
    private bool _desktopHidden;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Log(ex);
        };
        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            MessageBox.Show("程序遇到问题，已写入 error.log。\n" + args.Exception.Message, "超级桌面便签");
            args.Handled = true;
            ExitApp();
        };

        _single = new SingleInstance();
        if (!_single.IsPrimary)
        {
            _single.SignalExisting();
            _single.Dispose();
            Shutdown();
            return;
        }

        try
        {
            _store = NoteStore.Load(ParseDataPath(e.Args));
        }
        catch (Exception ex)
        {
            Log(ex);
            MessageBox.Show("便签数据无法打开。\n" + ex.Message, "超级桌面便签");
            Shutdown();
            return;
        }

        _panel = new ControlPanelWindow(_store);
        _tray = new TrayService();
        _hotkey = new HotkeyService();

        _store.Notes.CollectionChanged += OnNotesChanged;
        foreach (var note in _store.Notes.ToList())
            Attach(note);

        _tray.OpenRequested += (_, _) => ShowPanel();
        _tray.ToggleDesktop += (_, _) => ToggleAllDesktop();
        _tray.ExitRequested += (_, _) => ExitApp();
        _hotkey.Pressed += (_, _) => TogglePanel();
        _single.Activated += (_, _) => Dispatcher.BeginInvoke(ShowPanel);
        _panel.SourceInitialized += (_, _) => RegisterHotkey();

        _single.StartListen();
        ShowPanel();
    }

    private void RegisterHotkey()
    {
        if (_panel == null || _hotkey == null || _tray == null)
            return;
        var source = (HwndSource?)PresentationSource.FromVisual(_panel);
        if (source == null)
            return;
        if (!_hotkey.TryRegister(source))
            _tray.Notify("快捷键 Ctrl+Alt+N 已被占用，请使用托盘图标打开面板。");
    }

    private void OnNotesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (Note note in e.NewItems)
                Attach(note);
        }

        if (e.OldItems != null)
        {
            foreach (Note note in e.OldItems)
                Detach(note);
        }
    }

    private void Attach(Note note)
    {
        note.PropertyChanged += OnNoteProperty;
        if (note.PinnedToDesktop)
            OpenSticky(note);
    }

    private void Detach(Note note)
    {
        note.PropertyChanged -= OnNoteProperty;
        CloseSticky(note);
    }

    private void OnNoteProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Note note || e.PropertyName != nameof(Note.PinnedToDesktop))
            return;
        if (note.PinnedToDesktop)
            OpenSticky(note);
        else
            CloseSticky(note);
    }

    private void OpenSticky(Note note)
    {
        if (_store == null)
            return;
        if (_stickies.TryGetValue(note.Id, out var existing))
        {
            if (!_desktopHidden)
            {
                existing.Show();
                existing.Activate();
            }
            return;
        }

        var window = new StickyWindow(_store, note);
        window.Closed += (_, _) => _stickies.Remove(note.Id);
        _stickies[note.Id] = window;
        if (!_desktopHidden)
            window.Show();
    }

    private void CloseSticky(Note note)
    {
        if (_stickies.TryGetValue(note.Id, out var window))
            window.RequestClose();
    }

    private void ToggleAllDesktop()
    {
        if (_store == null || _tray == null)
            return;
        _desktopHidden = !_desktopHidden;
        _tray.SetDesktopHidden(_desktopHidden);
        if (_desktopHidden)
        {
            foreach (var window in _stickies.Values)
                window.Hide();
            return;
        }

        foreach (var note in _store.Notes)
        {
            if (note.PinnedToDesktop)
                OpenSticky(note);
        }
    }

    private void ShowPanel()
    {
        if (_panel == null)
            return;
        if (_panel.WindowState == WindowState.Minimized)
            _panel.WindowState = WindowState.Normal;
        _panel.Topmost = true;
        _panel.Show();
        _panel.Activate();
    }

    private void TogglePanel()
    {
        if (_panel == null)
            return;
        if (_panel.IsVisible && _panel.WindowState != WindowState.Minimized && _panel.IsActive)
            _panel.Close();
        else
            ShowPanel();
    }

    private void ExitApp()
    {
        if (_exiting)
            return;
        _exiting = true;
        try
        {
            _hotkey?.Dispose();
            foreach (var window in _stickies.Values.ToList())
            {
                window.SuppressUnpin = true;
                window.RequestClose();
            }

            if (_panel != null)
            {
                _panel.AllowClose = true;
                _panel.Close();
            }

            _tray?.Dispose();
            _store?.Dispose();
            _single?.Dispose();
        }
        catch (Exception ex)
        {
            Log(ex);
        }

        Shutdown();
    }

    private static string? ParseDataPath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--data", StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static void Log(Exception ex)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KkNote");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "error.log");
            File.AppendAllText(path, $"---- {DateTime.Now:yyyy-MM-dd HH:mm:ss} ----{Environment.NewLine}{ex}{Environment.NewLine}");
        }
        catch (Exception)
        {
        }
    }
}
