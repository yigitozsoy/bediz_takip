using Microsoft.Win32;
using BambuBar.Core.App;

namespace BambuBar.Notifications;

/// <summary>
/// Print notifications (P3.7; the Mac's Notifier). Core decides when and what (started, paused, finished, failed,
/// canceled, a new serious HMS error) and names each one <c>&lt;kind&gt;.&lt;serial&gt;</c>.
/// <para>
/// beta: shown through the tray icon (<see cref="Tray.TrayIcon.Notify"/>), which Windows 10 and 11 file as an
/// ordinary notification under the app's AppUserModelID. Bambu Bar used WinRT toasts; their projection is a 53 MB
/// library, for what the tray icon already does. A click opens <c>&lt;scheme&gt;://printer/&lt;serial&gt;</c>: that
/// printer's page.
/// </para>
/// </summary>
public sealed class ToastNotifier(Func<Tray.TrayIcon?> tray, Action<string> open) : INotifier
{
    public void RequestAuthorization()
    {
        // Windows doesn't ask: notifications are on unless the person turned them off (the Notifications page says so).
    }

    public void Post(string title, string body, string id)
    {
        var serial = SerialOf(id);
        var link = serial is null ? Brand.Scheme + "://open" : $"{Brand.Scheme}://printer/{serial}";
        try
        {
            tray()?.Notify(title, body, () => open(link));
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // No tray (Explorer restarting): nothing to show.
        }
    }

    /// <summary>Whether Windows lets the app's notifications through; null when it can't be read.</summary>
    public static bool? Enabled()
    {
        try
        {
            using var all = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\PushNotifications");
            if (all?.GetValue("ToastEnabled") is int global && global == 0) return false;
            using var app = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\{AppIdentity.Id}");
            return app?.GetValue("Enabled") is not int enabled || enabled != 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return null;
        }
    }

    /// <summary>The serial at the end of a Core notification id (<c>end.01P00A123456789</c>).</summary>
    internal static string? SerialOf(string id)
    {
        var dot = id.LastIndexOf('.');
        var serial = dot >= 0 ? id[(dot + 1)..] : "";
        return serial.Length > 0 && serial.All(char.IsAsciiLetterOrDigit) ? serial.ToUpperInvariant() : null;
    }
}
