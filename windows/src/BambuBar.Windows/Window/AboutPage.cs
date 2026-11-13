using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using BambuBar.Core;
using BambuBar.Core.Model;
using BambuBar.Theme;
using BambuBar.Tray;
using Microsoft.Win32;

namespace BambuBar.AppWindow;

/// <summary>About the app: version details for bug reports, links, how it works, privacy and credits. beta has
/// no supporter license: everything is free, and updates come from beta's own GitHub releases.</summary>
internal sealed class AboutPage(WindowContext ctx) : Page(ctx)
{
    private readonly DispatcherTimer _copiedTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private ChromeButton _copy = null!;

    public override FrameworkElement Build()
    {
        _copiedTimer.Tick += (_, _) =>
        {
            _copiedTimer.Stop();
            SetCopyLabel(false);
        };

        var hero = Hero();
        var copy = Section(Loc.T("This copy"), out var copyBody);
        copyBody.Children.Add(W.DetailRow(Loc.T("Version"), AppVersion.Marketing));
        copyBody.Children.Add(W.DetailRow(Loc.T("System"), SystemInfo.Description));
        copyBody.Children.Add(W.DetailRow(Loc.T("License"), Loc.T("Free · MIT License")));
        var reports = Ui.Columns("*", "Auto");
        reports.Children.Add(Ui.At(Ui.Text(Loc.T("For bug reports"), WRamp.Note, null, ThemeKeys.TextSecondary), 0));
        _copy = W.Button(Loc.T("Copy Version Info"), CopyVersion, small: true);
        reports.Children.Add(Ui.At(_copy, 1));
        copyBody.Children.Add(reports);

        var links = Section(Loc.T("Links"), out var linksBody);
        linksBody.Spacing = 2;
        void Link(string title, string glyph, Uri url)
        {
            var link = W.Link(title, () => W.Open(url), glyph, WRamp.Detail);
            link.Margin = new Thickness(-6, 0, 0, 0);
            Ui.Tip(link, url.AbsoluteUri);
            linksBody.Children.Add(link);
        }
        Link(Loc.T("beta on GitHub"), "", Website.Home);
        Link(Loc.T("Release Notes"), "", Website.Changelog);
        Link(Loc.T("Report an Issue"), "", Website.NewIssue);
        Link("Original project: Bambu Bar", "", Website.Upstream);

        var top = Ui.Columns("*", "20", "*");
        top.Children.Add(Ui.At(copy, 0));
        top.Children.Add(Ui.At(links, 2));

        var how = Section(Loc.T("How it works"), out var howBody);
        howBody.Children.Add(Paragraph(Loc.T("Bambu Bar connects straight to your printer on the local network, the same way Bambu Studio does in LAN mode: an encrypted MQTT connection on port 8883, signed in with the printer's access code. The printer's certificate is checked against Bambu Lab's own certificate authority.")));
        howBody.Children.Add(Paragraph(Loc.T("The printer streams its status about once a second. Bambu Bar shows it in the tray, keeps a short history of your prints, and writes a snapshot for its desktop widgets on this PC.")));

        var privacy = Section(Loc.T("Privacy"), out var privacyBody);
        privacyBody.Children.Add(Paragraph(Loc.T("Printer data stays on your local network. No app analytics. Printer access codes are stored in Windows Credential Manager.")));
        privacyBody.Children.Add(Paragraph(Loc.T("Update checks read the latest beta release on GitHub and send no printer data. Updates install only when they carry beta's signature. Manage automatic updates in Settings → Updates.")));

        var credits = Section(Loc.T("Credits"), out var creditsBody);
        creditsBody.Children.Add(Paragraph("beta is built on Bambu Bar by NINA LABS, LLC, released under the MIT License. Thank you for making it open source."));
        creditsBody.Children.Add(Paragraph(Loc.T("Protocol details and the HMS error texts come from the open-source ha-bambulab project (MIT), with thanks to Bambuddy and bambu-cli for documenting the newer printers. What each model can do (chamber sensor, fans, heated chamber, nozzles, build volume) follows the printer profiles Bambu Lab publishes with Bambu Studio.")));
        creditsBody.Children.Add(Paragraph(Loc.T("The printer models were built for this project in Blender. They are visual approximations, not manufacturer CAD.")));
        creditsBody.Children.Add(Paragraph(Loc.T("Desktop widgets are drawn by Rainmeter (GPL-2.0), shipped unmodified with its license and source.")));
        creditsBody.Children.Add(Paragraph(Loc.T("Bambu Lab, Bambu Studio and P2S are trademarks of Bambu Lab. Bambu Bar is an independent project, not affiliated with or endorsed by Bambu Lab.")));

        return new Stack { Spacing = 22, Children = { hero, top, how, privacy, credits } };
    }

    public override void Refresh() { }

    // MARK: Header

    /// <summary>The app's mark, name, credit, version and what it is.</summary>
    private Grid Hero()
    {
        // The app's icon beside its name: decoration (the Mac hides it from VoiceOver).
        FrameworkElement icon = W.AppIcon(192) is { } picture
            ? new DecorativeImage
            {
                Source = picture, Width = 96, Height = 96,
                Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 5, Direction = 270, Opacity = 0.25 },
            }
            : new Border
            {
                // A build without AppIcon.png beside it (a bare dotnet run): the tray's mark on the accent.
                Width = 96, Height = 96, CornerRadius = new CornerRadius(22),
                Child = new DecorativeImage { Source = TrayGlyph.Render(new TrayGlyph.Look(JobState.Printing, 0.6, false, false), 64, lightTaskbar: false), Width = 56, Height = 56 },
                Background = new LinearGradientBrush(Palette.Accent, Palette.AccentDeep, 90),
            };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        var titles = new Stack
        {
            Spacing = 4, VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Ui.Text(Brand.Name, 30, FontWeights.Bold, display: true),
                Ui.Text("Based on Bambu Bar by NINA LABS, LLC", WRamp.Detail, null, ThemeKeys.TextSecondary),
                Ui.Text(Loc.F("Version %@", AppVersion.Marketing), WRamp.Body, null, ThemeKeys.TextSecondary),
                Ui.Text(Loc.T("Your Bambu Lab printer, one click away in the tray."), WRamp.Body, null, ThemeKeys.TextTertiary),
            },
        };
        AutomationProperties.SetHeadingLevel(titles.Children[0], AutomationHeadingLevel.Level1);
        var grid = Ui.Columns("Auto", "20", "*");
        grid.Margin = new Thickness(0, 0, 0, 4);
        grid.Children.Add(Ui.At(icon, 0));
        grid.Children.Add(Ui.At(titles, 2));
        return grid;
    }

    private static Stack Section(string title, out Stack body)
    {
        body = new Stack { Spacing = 10 };
        var section = new Stack { Spacing = 8, Role = System.Windows.Automation.Peers.AutomationControlType.Group, Children = { Ui.SectionLabel(title, null), W.Card(body, 16) } };
        AutomationProperties.SetName(section, title);
        return section;
    }

    private static TextBlock Paragraph(string text) => W.Note(text, ThemeKeys.TextSecondary, WRamp.Detail);

    private void CopyVersion()
    {
        try { Clipboard.SetText($"{Brand.Name} {AppVersion.Marketing} · {SystemInfo.Description}"); }
        catch (ExternalException) { return; }
        SetCopyLabel(true);
        _copiedTimer.Stop();
        _copiedTimer.Start();
    }

    private void SetCopyLabel(bool copied)
    {
        var text = copied ? Loc.T("Copied") : Loc.T("Copy Version Info");
        ((TextBlock)_copy.Content).Text = text;
        AutomationProperties.SetName(_copy, text);
    }

    public override void Dispose() => _copiedTimer.Stop();
}

/// <summary>"Windows 11 24H2 (26100.2033) · ARM64", for bug reports (the Mac's AppInfo.system and architecture).</summary>
internal static class SystemInfo
{
    public static string Description { get; } = Describe();

    private static string Describe()
    {
        var version = Environment.OSVersion.Version;
        var name = version.Build >= 22000 ? "Windows 11" : "Windows 10";
        string? display = null;
        int? revision = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            display = key?.GetValue("DisplayVersion") as string;
            revision = key?.GetValue("UBR") as int?;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException) { }
        var build = revision is { } r ? $"{version.Build}.{r}" : $"{version.Build}";
        var os = string.IsNullOrEmpty(display) ? $"{name} ({build})" : $"{name} {display} ({build})";
        var arch = RuntimeInformation.OSArchitecture switch { Architecture.Arm64 => "ARM64", Architecture.X64 => "x64", var a => a.ToString() };
        if (RuntimeInformation.ProcessArchitecture != RuntimeInformation.OSArchitecture)
            arch += $" ({RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()} app)";
        return $"{os} · {arch}";
    }
}
