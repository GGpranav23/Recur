using System;
using Forms = System.Windows.Forms;

namespace HabitApp;

public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Recur", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _icon = new Forms.NotifyIcon
        {
            // Use the app's own icon (Recur.ico), or a plain one if it can't be read.
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
                   ?? System.Drawing.SystemIcons.Application,
            Text = "Recur",
            ContextMenuStrip = menu,
            Visible = true
        };

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                OpenRequested?.Invoke();
            }
        };
        _icon.BalloonTipClicked += (_, _) => OpenRequested?.Invoke();
    }

    public void Notify(string title, string message)
    {
        _icon.ShowBalloonTip(8000, title, message, Forms.ToolTipIcon.None);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}