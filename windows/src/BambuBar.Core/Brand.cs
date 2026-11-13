namespace BambuBar.Core;

/// <summary>
/// Beta Monitor's identity in one place. Beta Monitor is a fork of Bambu Bar (MIT, © NINA LABS, LLC); the code keeps
/// Bambu Bar's namespaces and catalog keys so upstream changes still merge, and shows this name instead.
/// </summary>
public static class Brand
{
    /// <summary>The name people see.</summary>
    public const string Name = "Beta Monitor";
    /// <summary>The folder under %APPDATA% and %LOCALAPPDATA%; also the Credential Manager prefix.</summary>
    public const string Folder = "BetaMonitor";
    /// <summary>The exe beside the app's files.</summary>
    public const string Exe = "BetaMonitor.exe";
    /// <summary>The link scheme toasts and widgets open (<c>betamonitor://printer/&lt;serial&gt;</c>).</summary>
    public const string Scheme = "betamonitor";
    /// <summary>Windows' AppUserModelID: toasts, notification settings, the taskbar button.</summary>
    public const string AppUserModelId = "BetaMonitor.App";
    /// <summary>Beta Monitor's GitHub repository ("owner/name"): links, issues and the update feed.</summary>
    public const string Repository = "yigitozsoy/printpulse";
    /// <summary>The upstream project's name, for the credit on the About page.</summary>
    public const string Upstream = "Bambu Bar";

    /// <summary>Turkish case endings after the name follow its last vowel: "Bambu Bar'ı" but "Beta Monitor'ü"
    /// (read "monitör"). Longest first.</summary>
    private static readonly (string Upstream, string Name)[] TurkishEndings =
    [
        ("'dan", "'den"), ("'da", "'de"), ("'ın", "'ün"), ("'ı", "'ü"), ("'a", "'e"),
    ];

    /// <summary>A catalog text with the upstream name swapped for <see cref="Name"/>.</summary>
    public static string Apply(string text)
    {
        if (!text.Contains(Upstream, StringComparison.Ordinal)) return text;
        foreach (var (from, to) in TurkishEndings)
            text = text.Replace(Upstream + from, Name + to, StringComparison.Ordinal);
        return text.Replace(Upstream, Name, StringComparison.Ordinal);
    }
}
