using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace BambuBar.Core.Updates;

/// <summary>
/// Beta Monitor's release side of <see cref="UpdateSignature"/>: makes the Ed25519 update key and writes a Windows-only
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
        var seed = RandomNumberGenerator.GetBytes(Ed25519PrivateKeyParameters.KeySize);
        var key = new Ed25519PrivateKeyParameters(seed);
        return (Convert.ToBase64String(seed), Convert.ToBase64String(key.GeneratePublicKey().GetEncoded()));
    }

    /// <summary>The public key that belongs to <paramref name="privateKey"/> (Base64 seed).</summary>
    public static string PublicKeyOf(string privateKey) =>
        Convert.ToBase64String(Key(privateKey).GeneratePublicKey().GetEncoded());

    /// <summary>Ed25519 over the whole file, Base64: what <see cref="UpdateSignature.VerifyAsync"/> checks.</summary>
    public static string Sign(string path, string privateKey)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, Key(privateKey));
        var bytes = File.ReadAllBytes(path);
        signer.BlockUpdate(bytes, 0, bytes.Length);
        return Convert.ToBase64String(signer.GenerateSignature());
    }

    /// <summary>
    /// An appcast with one item per installer. The architecture comes from the file name
    /// (<c>BetaMonitor-1.2.3-windows-x64.exe</c>, <c>…-windows-arm64.exe</c>); <paramref name="downloadBase"/> is the
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

    private static Ed25519PrivateKeyParameters Key(string privateKey)
    {
        var seed = Convert.FromBase64String(privateKey.Trim());
        if (seed.Length != Ed25519PrivateKeyParameters.KeySize) throw new ArgumentException("not an Ed25519 private key (32 bytes, Base64)");
        return new Ed25519PrivateKeyParameters(seed);
    }
}
