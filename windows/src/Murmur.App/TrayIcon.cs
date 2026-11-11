using System.Drawing;
using System.Windows.Forms;
using Murmur.Core;
using Murmur.Services;

namespace Murmur.App;

/// <summary>
/// The notification-area icon and its menu (the Windows version of the Mac's menu bar item), plus
/// the balloons that say where a dictation went when no text field was focused.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _startWithWindows;
    private readonly ToolStripMenuItem _hint;

    public TrayIcon(Action openWindow, Action copyDiagnostics, Action quit)
    {
        _hint = new ToolStripMenuItem(HintText(isHotkeyActive: true)) { Enabled = false };
        _startWithWindows = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        // click, not CheckedChanged: re-reading the state when the menu opens must not write it back
        _startWithWindows.Click += (_, _) => StartWithWindows.SetEnabled(_startWithWindows.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_hint);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Open Murmur…", image: null, (_, _) => openWindow()));
        menu.Items.Add(_startWithWindows);
        menu.Items.Add(new ToolStripMenuItem("Copy diagnostics", image: null, (_, _) => copyDiagnostics()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Quit Murmur", image: null, (_, _) => quit()));
        // read when the menu opens, so a change made in task manager's startup apps shows up
        menu.Opening += (_, _) => _startWithWindows.Checked = StartWithWindows.IsEnabled;

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = AppConstants.AppName,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                openWindow();
            }
        };
    }

    /// <summary>Shows whether Caps Lock dictation works right now.</summary>
    public void SetHotkeyActive(bool isActive) => _hint.Text = HintText(isActive);

    public void ShowBalloon(string title, string body) => _icon.ShowBalloonTip(0, title, body, ToolTipIcon.Info);

    public void Dispose()
    {
        // hiding first removes the icon right away instead of when the mouse next passes over it
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _hint.Dispose();
        _startWithWindows.Dispose();
    }

    private static string HintText(bool isHotkeyActive) =>
        isHotkeyActive ? "Hold Caps Lock to dictate, Caps Lock + Space to lock" : "Caps Lock shortcut isn't running";

    private static Icon LoadIcon()
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream("Murmur.ico")
            ?? throw new InvalidOperationException("Murmur.ico is not embedded in the build");
        return new Icon(stream, SystemInformation.SmallIconSize);
    }
}
