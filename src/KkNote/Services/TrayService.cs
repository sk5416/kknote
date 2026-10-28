using WinForms = System.Windows.Forms;

namespace KkNote.Services;

public sealed class TrayService : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _toggleDesktop;
    private readonly System.Drawing.Icon _ownedIcon;
    private bool _desktopHidden;

    public TrayService()
    {
        _ownedIcon = LoadIcon();
        _toggleDesktop = new WinForms.ToolStripMenuItem("隐藏桌面便签", null, (_, _) => ToggleDesktop?.Invoke(this, EventArgs.Empty));
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("打开面板", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(_toggleDesktop);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        var version = typeof(TrayService).Assembly.GetName().Version;
        var versionText = version == null ? "v0.1.0" : $"v{version.ToString(3)}";
        menu.Items.Add(new WinForms.ToolStripMenuItem(versionText) { Enabled = false });
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _icon = new WinForms.NotifyIcon
        {
            Icon = _ownedIcon,
            Text = "超级桌面便签 — 双击打开",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.MouseDoubleClick += (_, args) =>
        {
            if (args.Button == WinForms.MouseButtons.Left)
                OpenRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? ToggleDesktop;
    public event EventHandler? ExitRequested;

    public void SetDesktopHidden(bool hidden)
    {
        _desktopHidden = hidden;
        _toggleDesktop.Text = hidden ? "显示桌面便签" : "隐藏桌面便签";
    }

    public void Notify(string message)
    {
        _icon.BalloonTipTitle = "超级桌面便签";
        _icon.BalloonTipText = message;
        _icon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _ownedIcon.Dispose();
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                using var extracted = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (extracted != null)
                    return (System.Drawing.Icon)extracted.Clone();
            }
        }
        catch (Exception)
        {
        }

        return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
    }
}
