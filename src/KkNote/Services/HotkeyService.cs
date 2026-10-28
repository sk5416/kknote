using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace KkNote.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x4B01;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkN = 0x4E;

    private HwndSource? _source;
    private bool _registered;

    public event EventHandler? Pressed;

    public bool TryRegister(HwndSource source)
    {
        _source = source;
        source.AddHook(WndProc);
        _registered = RegisterHotKey(source.Handle, HotkeyId, ModControl | ModAlt | ModNoRepeat, VkN);
        return _registered;
    }

    public void Dispose()
    {
        if (_source != null)
        {
            if (_registered)
                UnregisterHotKey(_source.Handle, HotkeyId);
            _source.RemoveHook(WndProc);
            _registered = false;
            _source = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
