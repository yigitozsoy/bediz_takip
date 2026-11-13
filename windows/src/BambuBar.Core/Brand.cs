namespace BambuBar.Core;

/// <summary>
/// beta's identity in one place. beta is a fork of Bambu Bar (MIT, © NINA LABS, LLC); the code keeps
/// Bambu Bar's namespaces and catalog keys so upstream changes still merge, and shows this name instead.
/// </summary>
public static class Brand
{
    /// <summary>The name people see.</summary>
    public const string Name = "beta";
    /// <summary>The folder under %APPDATA% and %LOCALAPPDATA%; also the Credential Manager prefix.</summary>
    public const string Folder = "BetaMonitor";
    /// <summary>The exe beside the app's files.</summary>
    public const string Exe = "beta.exe";
    /// <summary>The link scheme toasts and widgets open (<c>betamonitor://printer/&lt;serial&gt;</c>).</summary>
    public const string Scheme = "betamonitor";
    /// <summary>Windows' AppUserModelID: toasts, notification settings, the taskbar button.</summary>
    public const string AppUserModelId = "BetaMonitor.App";
    /// <summary>beta's GitHub repository ("owner/name"): links, issues and the update feed.</summary>
    public const string Repository = "yigitozsoy/beta_monitor";
    /// <summary>The upstream project's name, for the credit on the About page.</summary>
    public const string Upstream = "Bambu Bar";

    /// <summary>Turkish case endings after the name: "beta" ends in a vowel, so the endings that start with one take
    /// a buffer letter ("Bambu Bar'ı" but "beta'yı", "Bambu Bar'ın" but "beta'nın"). Longest first.</summary>
    private static readonly (string Upstream, string Name)[] TurkishEndings =
    [
        ("'ın", "'nın"), ("'ı", "'yı"), ("'a", "'ya"),
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
