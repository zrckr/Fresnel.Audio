using Foster.Framework;

namespace Fresnel.Audio;

public sealed class Audio : IDisposable
{
    internal AudioDevice Device { get; }

    public AudioListener Listener { get; } = new();

    public bool IsDisposed { get; private set; }

    private readonly HashSet<AudioStream> _streams = new();

    public Audio(App app)
    {
        Device = new AudioDeviceSDL(app);
    }

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
