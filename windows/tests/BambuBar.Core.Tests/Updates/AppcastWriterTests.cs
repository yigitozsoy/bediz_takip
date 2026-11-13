using System.Text;
using BambuBar.Core.Updates;

namespace BambuBar.Core.Tests.Updates;

/// <summary>beta's release tooling: the appcast it writes is one the app reads, picks and verifies.</summary>
public class AppcastWriterTests
{
    [Fact]
    public async Task WrittenAppcastParsesAndVerifies()
    {
        var ct = TestContext.Current.CancellationToken;
        var (privateKey, publicKey) = AppcastWriter.NewKey();
        Assert.Equal(publicKey, AppcastWriter.PublicKeyOf(privateKey));
        var folder = Directory.CreateTempSubdirectory("betamonitor-appcast-").FullName;
        try
        {
            var x64 = Path.Combine(folder, "beta-3.1.0-windows-x64.exe");
            var arm = Path.Combine(folder, "beta-3.1.0-windows-arm64.exe");
            File.WriteAllBytes(x64, Encoding.UTF8.GetBytes("x64 installer"));
            File.WriteAllBytes(arm, Encoding.UTF8.GetBytes("arm64 installer, a bit longer"));

            var document = AppcastWriter.Write("3.1.0", new Uri("https://github.com/o/r/releases/download/v3.1.0/"), [x64, arm], privateKey);
            using var xml = new MemoryStream();
            document.Save(xml);
            xml.Position = 0;
            var appcast = Appcast.Parse(xml);

            var item = appcast.Latest(UpdatePlatform.WindowsArm64, new Version(10, 0, 26100))!;
            Assert.Equal("3.1.0", item.DisplayVersion);
            Assert.Equal("https://github.com/o/r/releases/download/v3.1.0/beta-3.1.0-windows-arm64.exe", item.Url!.AbsoluteUri);
            Assert.Equal(new FileInfo(arm).Length, item.Length);
            var signature = new UpdateSignature(publicKey);
            Assert.True(await signature.VerifyFileAsync(arm, item.Length!.Value, item.EdSignature!, ct));
            // The other architecture's signature doesn't fit this file, and Windows 10 1809 gets nothing.
            var other = appcast.Latest(UpdatePlatform.WindowsX64, new Version(10, 0, 26100))!;
            Assert.False(await signature.VerifyFileAsync(arm, item.Length!.Value, other.EdSignature!, ct));
            Assert.Null(appcast.Latest(UpdatePlatform.WindowsX64, new Version(10, 0, 17763)));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void InstallerWithoutArchitectureIsRefused()
    {
        var (privateKey, _) = AppcastWriter.NewKey();
        var path = Path.GetTempFileName();
        try { Assert.Throws<ArgumentException>(() => AppcastWriter.Write("1.0.0", new Uri("https://example.com/"), [path], privateKey).ToString()); }
        finally { File.Delete(path); }
    }
}
