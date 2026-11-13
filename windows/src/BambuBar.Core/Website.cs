namespace BambuBar.Core;

/// <summary>Beta Monitor's links: its GitHub repository (<see cref="Brand.Repository"/>), plus the upstream project it
/// is built on. No tracking parameters.</summary>
public static class Website
{
    public static readonly Uri Home = new($"https://github.com/{Brand.Repository}");
    public static readonly Uri Changelog = new($"https://github.com/{Brand.Repository}/releases");
    public static readonly Uri NewIssue = new($"https://github.com/{Brand.Repository}/issues/new");
    public static readonly Uri Upstream = new("https://github.com/rolottr/bambu-bar");
}
