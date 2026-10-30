using System.Windows.Threading;

namespace KkNote.Services;

public sealed class DayClock : IDisposable
{
    private DispatcherTimer? _timer;
    private bool _disposed;

    public static event Action? DayRolled;

    public event Action? Rolled;

    public void Start()
    {
        Arm();
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Stop();
        _timer = null;
    }

    private void Arm()
    {
        if (_disposed)
            return;
        _timer?.Stop();
        var now = DateTime.Now;
        var wait = now.Date.AddDays(1) - now + TimeSpan.FromSeconds(2);
        if (wait < TimeSpan.FromSeconds(2))
            wait = TimeSpan.FromSeconds(2);
        _timer = new DispatcherTimer { Interval = wait };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Rolled?.Invoke();
        DayRolled?.Invoke();
        Arm();
    }
}
