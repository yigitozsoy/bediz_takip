namespace BambuBar.Core.App;

/// <summary>
/// beta's sounds, made in code (no sound files to copy or license), in the manner of a Bambu Lab printer's buzzer:
/// short clean beeps of a softened square wave (odd harmonics, rounded edges) in quick little figures. A finished print
/// runs up a major arpeggio, a start is two rising beeps, a pause two equal ones, a cancel two falling, a failure
/// three low ones, an alert alternates. 16-bit mono WAV.
/// </summary>
public static class Chime
{
    public const int SampleRate = 44100;

    /// <summary>The notes of each sound: frequency (Hz), start (s) and length (s).</summary>
    public static IReadOnlyList<(double Hz, double At, double Length)> Notes(PrintEvent kind) => kind switch
    {
        PrintEvent.Finished or PrintEvent.Test => [(1046.5, 0, 0.09), (1318.5, 0.11, 0.09), (1568, 0.22, 0.09), (2093, 0.33, 0.26)],
        PrintEvent.Started => [(1318.5, 0, 0.09), (1760, 0.13, 0.3)],
        PrintEvent.Paused => [(1568, 0, 0.12), (1568, 0.2, 0.22)],
        PrintEvent.Canceled => [(1568, 0, 0.12), (1046.5, 0.18, 0.3)],
        PrintEvent.Failed => [(784, 0, 0.16), (784, 0.24, 0.16), (784, 0.48, 0.3)],
        _ => [(1760, 0, 0.1), (1318.5, 0.14, 0.1), (1760, 0.28, 0.1), (1318.5, 0.42, 0.2)],
    };

    /// <summary>The sound as a complete WAV file.</summary>
    public static byte[] Wave(PrintEvent kind, double volume = 0.35)
    {
        var notes = Notes(kind);
        var seconds = notes.Max(n => n.At + n.Length) + 0.05;
        var count = (int)(seconds * SampleRate);
        var mix = new double[count];
        foreach (var (hz, at, length) in notes)
        {
            var start = (int)(at * SampleRate);
            var samples = (int)(length * SampleRate);
            for (var i = 0; i < samples && start + i < count; i++)
            {
                var t = (double)i / SampleRate;
                // A buzzer's beep: 3 ms in, held with a slight sag, 15 ms out (no clicks).
                var envelope = Math.Min(1, t / 0.003) * Math.Min(1, (length - t) / 0.015) * (1 - 0.25 * t / length);
                // A square wave's first odd harmonics, softened: bright like a buzzer, not harsh.
                var phase = 2 * Math.PI * hz * t;
                mix[start + i] += envelope * (Math.Sin(phase) + 0.22 * Math.Sin(3 * phase) + 0.08 * Math.Sin(5 * phase));
            }
        }
        var peak = Math.Max(1e-9, mix.Max(Math.Abs));
        using var stream = new MemoryStream(44 + count * 2);
        using var w = new BinaryWriter(stream);
        w.Write("RIFF"u8);
        w.Write(36 + count * 2);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);          // PCM
        w.Write((short)1);          // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);    // bytes per second
        w.Write((short)2);          // block align
        w.Write((short)16);         // bits per sample
        w.Write("data"u8);
        w.Write(count * 2);
        var gain = Math.Clamp(volume, 0, 1) / peak;
        foreach (var sample in mix) w.Write((short)Math.Round(Math.Clamp(sample * gain, -1, 1) * short.MaxValue));
        w.Flush();
        return stream.ToArray();
    }
}
