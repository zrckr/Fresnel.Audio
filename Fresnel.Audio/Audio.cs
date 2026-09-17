using Foster.Framework;

namespace Fresnel.Audio;

/// <summary>
/// Owns the audio device and listener used for playback in a Foster application.
/// </summary>
public sealed class Audio : IDisposable
{
    internal AudioDevice Device { get; }

    /// <summary>
    /// The listener used by spatial audio players.
    /// </summary>
    public AudioListener Listener { get; } = new();

    /// <summary>
    /// Whether this audio system has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    private readonly HashSet<AudioStream> _streams = new();

    /// <summary>
    /// Creates an audio system for an application.
    /// </summary>
    public Audio(App app)
    {
        Device = new AudioDeviceSDL(app);
    }

    /// <summary>
    /// Disposes the audio device and all streams and players created by this instance.
    /// </summary>
    public void Dispose()
    {
        if (!IsDisposed)
        {
            foreach (var stream in _streams.ToArray())
            {
                stream.Dispose();
            }

            _streams.Clear();
            Device.Dispose();
            IsDisposed = true;
        }
    }

    internal void AddStream(AudioStream stream)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _streams.Add(stream);
    }

    internal void RemoveStream(AudioStream stream)
    {
        _streams.Remove(stream);
    }
}
