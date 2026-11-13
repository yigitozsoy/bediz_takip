using System.Collections.Concurrent;
using System.IO;
using System.Media;
using BambuBar.Core.App;

namespace BambuBar.Notifications;

/// <summary>PrintPulse's chimes (<see cref="Chime"/>), made once per kind and played without waiting.</summary>
public sealed class SoundEffects : ISoundPlayer
{
    private readonly ConcurrentDictionary<PrintEvent, byte[]> _waves = new();
    // Held until the next sound, so the player isn't collected while it plays.
    private SoundPlayer? _playing;

    public void Play(PrintEvent kind)
    {
        var wave = _waves.GetOrAdd(kind, k => Chime.Wave(k));
        var player = new SoundPlayer(new MemoryStream(wave, writable: false));
        player.Load();
        _playing?.Dispose();
        _playing = player;
        player.Play();
    }
}
