using BambuBar.Core.App;
using BambuBar.Core.Imaging;
using BambuBar.Core.Model;
using BambuBar.Core.Updates;

// bambubar --probe <host> <serial> <access-code> [seconds] [--insecure-tls] [--raw-filament] [--raw-all]
// bambubar --probe-camera <host> <serial> <access-code> [seconds] [--jpeg|--rtsp] [--model <name>] [--insecure-tls] [--out <file.png>]
if (CameraProbe.Parse(args) is { } camera) return await CameraProbe.RunAsync(camera, Console.Out);
if (Probe.Parse(args) is { } probe) return await Probe.RunAsync(probe, Console.Out);
// bambubar --art <out.png> <width> <height>: the installer's printer (installer/art/make-art.py), a P2S printing at 64 %
if (args is ["--art", var output, var w, var h])
{
    var scene = new PrinterArt.Scene
    {
        Shape = PrinterShapes.FromModel("P2S"), State = JobState.Printing, Progress = 0.64, Filament = "F4A925FF",
        Spools = ["E8D7B0FF", "161616FF", "3F8E43FF", "F4A925FF"], LightOn = true, Dimmed = false,
    };
    var art = PrinterArt.Render(scene, int.Parse(w, System.Globalization.CultureInfo.InvariantCulture), int.Parse(h, System.Globalization.CultureInfo.InvariantCulture));
    if (art is null) { Console.Error.WriteLine("The printer renders aren't there (shared/Printers)."); return 1; }
    File.WriteAllBytes(output, Png.Encode(art));
    return 0;
}
// bambubar --update-keygen: a new update signing key (PrintPulse). The private half goes into the PRINTPULSE_UPDATE_KEY
// secret, the public half into UpdateSignature.SparklePublicKey.
if (args is ["--update-keygen"])
{
    var (privateKey, publicKey) = AppcastWriter.NewKey();
    Console.WriteLine($"PRIVATE (secret, never commit): {privateKey}");
    Console.WriteLine($"PUBLIC  (UpdateSignature.cs):   {publicKey}");
    return 0;
}
// bambubar --appcast <out.xml> <version> <download-base-url> <installer.exe>...: signs the installers with the key in
// PRINTPULSE_UPDATE_KEY and writes the appcast the app checks.
if (args is ["--appcast", var appcast, var version, var downloadBase, .. var installers] && installers.Length > 0)
{
    if (Environment.GetEnvironmentVariable("PRINTPULSE_UPDATE_KEY") is not { Length: > 0 } key)
    {
        Console.Error.WriteLine("PRINTPULSE_UPDATE_KEY isn't set.");
        return 1;
    }
    if (AppcastWriter.PublicKeyOf(key) != UpdateSignature.SparklePublicKey)
    {
        Console.Error.WriteLine("PRINTPULSE_UPDATE_KEY doesn't match the public key the app trusts (UpdateSignature.SparklePublicKey).");
        return 1;
    }
    var baseUrl = new Uri(downloadBase.EndsWith('/') ? downloadBase : downloadBase + "/");
    AppcastWriter.Write(version, baseUrl, installers, key).Save(appcast);
    Console.WriteLine($"Wrote {appcast}: {installers.Length} item(s), version {version}");
    return 0;
}
Console.Error.WriteLine("""
    usage: bambubar --probe <host> <serial> <access-code> [seconds] [--insecure-tls] [--raw-filament] [--raw-all]
           bambubar --probe-camera <host> <serial> <access-code> [seconds] [--jpeg|--rtsp] [--model <name>] [--insecure-tls] [--out <file.png>]
           bambubar --art <out.png> <width> <height>
           bambubar --update-keygen
           bambubar --appcast <out.xml> <version> <download-base-url> <installer.exe>...   (key in PRINTPULSE_UPDATE_KEY)
    """);
return 2;
