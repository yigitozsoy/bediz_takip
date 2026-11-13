namespace BambuBar.Core;

/// <summary>
/// PrintPulse's identity in one place. PrintPulse is a fork of Bambu Bar (MIT, © NINA LABS, LLC); the code keeps
/// Bambu Bar's namespaces and catalog keys so upstream changes still merge, and shows this name instead.
/// </summary>
public static class Brand
{
    /// <summary>The name people see.</summary>
    public const string Name = "PrintPulse";
    /// <summary>The folder under %APPDATA% and %LOCALAPPDATA%; also the Credential Manager prefix.</summary>
    public const string Folder = "PrintPulse";
    /// <summary>The exe beside the app's files.</summary>
    public const string Exe = "PrintPulse.exe";
    /// <summary>The link scheme toasts and widgets open (<c>printpulse://printer/&lt;serial&gt;</c>).</summary>
    public const string Scheme = "printpulse";
    /// <summary>Windows' AppUserModelID: toasts, notification settings, the taskbar button.</summary>
    public const string AppUserModelId = "PrintPulse.App";
    /// <summary>PrintPulse's GitHub repository ("owner/name"): links, issues and the update feed.</summary>
    public const string Repository = "yigitozsoy/printpulse";
    /// <summary>The upstream project's name, for the credit on the About page.</summary>
    public const string Upstream = "Bambu Bar";

    /// <summary>A catalog text with the upstream name swapped for <see cref="Name"/>.</summary>
    public static string Apply(string text) =>
        text.Contains(Upstream, StringComparison.Ordinal) ? text.Replace(Upstream, Name, StringComparison.Ordinal) : text;
}
