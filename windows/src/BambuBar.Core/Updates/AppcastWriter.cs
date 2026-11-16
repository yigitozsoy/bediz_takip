using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace BambuBar.Core.Updates;

/// <summary>
/// beta's release side of <see cref="UpdateSignature"/>: makes the Ed25519 update key and writes a Windows-only
/// appcast whose items carry each installer's signature. The private key lives only in the
/// <c>PRINTPULSE_UPDATE_KEY</c> GitHub secret (and wherever its owner keeps a copy), never in the repository.
/// </summary>
public static class AppcastWriter
{
    /// <summary>The oldest Windows the app runs on (10 1903), as the items' <c>sparkle:minimumSystemVersion</c>.</summary>
    public const string MinimumSystemVersion = "10.0.18362";

    /// <summary>A new key pair: the private 32-byte seed and the public key, both Base64.</summary>
    public static (string PrivateKey, string PublicKey) NewKey()
    {
        var seed = RandomNumberGenerator.GetBytes(Ed25519.KeySize);
        return (Convert.ToBase64String(seed), Convert.ToBase64String(Ed25519.PublicKey(seed)));
    }

    /// <summary>The public key that belongs to <paramref name="privateKey"/> (Base64 seed).</summary>
    public static string PublicKeyOf(string privateKey) =>
        Convert.ToBase64String(Ed25519.PublicKey(Key(privateKey)));

    /// <summary>Ed25519 over the whole file, Base64: what <see cref="UpdateSignature.VerifyAsync"/> checks.</summary>
    public static string Sign(string path, string privateKey)
    {
        using var file = File.OpenRead(path);
        return Convert.ToBase64String(Ed25519.Sign(Key(privateKey), file));
    }

    /// <summary>
    /// An appcast with one item per installer. The architecture comes from the file name
    /// (<c>beta-1.2.3-windows-x64.exe</c>, <c>…-windows-arm64.exe</c>); <paramref name="downloadBase"/> is the
    /// release's download folder (<c>https://github.com/owner/repo/releases/download/v1.2.3/</c>).
    /// </summary>
    public static XDocument Write(string version, Uri downloadBase, IEnumerable<string> installers, string privateKey,
        Uri? releaseNotes = null, DateTimeOffset? published = null)
    {
        XNamespace sparkle = Appcast.SparkleNamespace;
        var date = (published ?? DateTimeOffset.UtcNow).ToString("r", CultureInfo.InvariantCulture);
        var items = installers.Select(path =>
        {
            var name = Path.GetFileName(path);
            var os = name.Contains("-windows-arm64", StringComparison.OrdinalIgnoreCase) ? UpdatePlatform.WindowsArm64
                : name.Contains("-windows-x64", StringComparison.OrdinalIgnoreCase) ? UpdatePlatform.WindowsX64
                : throw new ArgumentException($"{name}: no -windows-x64 or -windows-arm64 in the name");
            var item = new XElement("item",
                new XElement("title", $"{Brand.Name} {version}"),
                new XElement("pubDate", date),
                new XElement(sparkle + "version", version),
                new XElement(sparkle + "shortVersionString", version),
                new XElement(sparkle + "minimumSystemVersion", MinimumSystemVersion));
            if (releaseNotes is not null) item.Add(new XElement(sparkle + "fullReleaseNotesLink", releaseNotes.AbsoluteUri));
            item.Add(new XElement("enclosure",
                new XAttribute("url", new Uri(downloadBase, Uri.EscapeDataString(name)).AbsoluteUri),
                new XAttribute(sparkle + "os", os),
                new XAttribute("length", new FileInfo(path).Length.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("type", "application/octet-stream"),
                new XAttribute(sparkle + "edSignature", Sign(path, privateKey))));
            return item;
        });
        return new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement("rss", new XAttribute("version", "2.0"), new XAttribute(XNamespace.Xmlns + "sparkle", sparkle),
                new XElement("channel", new XElement("title", Brand.Name), items)));
    }

    /// <summary>The seed in <paramref name="privateKey"/>: the Base64 alone, or the whole key file --update-keygen
    /// writes ("PRIVATE (...): seed" and "PUBLIC (...): key" lines), whose PRIVATE line it takes.</summary>
    private static byte[] Key(string privateKey)
    {
        var text = privateKey.Trim();
        foreach (var line in text.Split('\n'))
            if (line.TrimStart().StartsWith("PRIVATE", StringComparison.OrdinalIgnoreCase))
                text = line[(line.LastIndexOf(':') + 1)..].Trim();
        var seed = Convert.FromBase64String(text);
        if (seed.Length != Ed25519.KeySize) throw new ArgumentException("not an Ed25519 private key (32 bytes, Base64)");
        return seed;
    }
}
