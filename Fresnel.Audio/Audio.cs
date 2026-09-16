using Foster.Framework;

namespace Fresnel.Audio;

public sealed class Audio : IDisposable
{
    internal AudioDevice Device { get; }

    public AudioListener Listener { get; } = new();

    public Audio(App app)
    {
        Device = new AudioDeviceSDL(app);
    }

    public void Dispose()
    {
        Device.Dispose();
    }
}
