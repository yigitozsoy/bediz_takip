namespace BambuBar.Core.App;

/// <summary>
/// PrintPulse's sounds, made in code (no sound files to license): short soft bell tones, a sine with a quiet octave
/// and an exponential fade. A finished print rises, a failed one falls, a pause asks twice. 16-bit mono WAV.
/// </summary>
public static class Chime
{
    public const int SampleRate = 44100;

    /// <summary>The notes of each sound: frequency (Hz), start (s) and length (s).</summary>
    public static IReadOnlyList<(double Hz, double At, double Length)> Notes(PrintEvent kind) => kind switch
    {
        PrintEvent.Finished or PrintEvent.Test => [(523.25, 0, 0.5), (659.25, 0.11, 0.5), (783.99, 0.22, 0.5), (1046.5, 0.33, 0.9)],
        PrintEvent.Started => [(523.25, 0, 0.35), (783.99, 0.1, 0.6)],
        PrintEvent.Paused => [(880, 0, 0.3), (880, 0.22, 0.45)],
        PrintEvent.Canceled => [(659.25, 0, 0.4), (523.25, 0.14, 0.7)],
        PrintEvent.Failed => [(783.99, 0, 0.4), (659.25, 0.16, 0.4), (523.25, 0.32, 0.4), (392, 0.48, 0.9)],
        _ => [(987.77, 0, 0.3), (698.46, 0.16, 0.3), (987.77, 0.32, 0.3), (698.46, 0.48, 0.6)],
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
                // 6 ms in, then a bell's fade over the note's length.
                var envelope = Math.Min(1, t / 0.006) * Math.Exp(-5.5 * t / length);
                mix[start + i] += envelope * (Math.Sin(2 * Math.PI * hz * t) + 0.25 * Math.Sin(4 * Math.PI * hz * t));
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
