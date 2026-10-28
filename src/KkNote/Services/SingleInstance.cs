namespace KkNote.Services;

public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\KkNote.SingleInstance";
    private const string EventName = @"Local\KkNote.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private CancellationTokenSource? _cts;
    private Thread? _thread;
    private bool _disposed;

    public SingleInstance()
    {
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        _mutex = new Mutex(true, MutexName, out var created);
        IsPrimary = created;
    }

    public bool IsPrimary { get; }
    public event EventHandler? Activated;

    public void SignalExisting() => _activate.Set();

    public void StartListen()
    {
        if (!IsPrimary)
            return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _thread = new Thread(() =>
        {
            while (!token.IsCancellationRequested)
            {
                bool signaled;
                try
                {
                    signaled = _activate.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (token.IsCancellationRequested)
                    break;
                if (signaled)
                    Activated?.Invoke(this, EventArgs.Empty);
            }
        })
        {
            IsBackground = true,
            Name = "KkNote.Activate",
        };
        _thread.Start();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cts?.Cancel();
        try
        {
            _activate.Set();
        }
        catch (ObjectDisposedException)
        {
        }

        _activate.Dispose();
        if (IsPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _mutex.Dispose();
        _cts?.Dispose();
    }
}
