using Foster.Framework;

namespace Fresnel.Audio;

public sealed class AudioStream : IDisposable
{
    public TimeSpan? Duration
    {
        get
        {
            return _device.GetDuration(this);
        }
    }

    public float PlaybackRate
    {
        get;
        set
        {
            if (!float.IsFinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Playback rate must be finite and positive.");
            }

            if (Math.Abs(field - value) > float.Epsilon)
            {
                field = value;
                Changed?.Invoke();
            }
        }
    } = 1f;

    public bool Looping
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Changed?.Invoke();
            }
        }
    }

    public bool IsDisposed { get; private set; }

    internal event Action? Changed;

    private readonly AudioDevice _device;

    public AudioStream(AudioDevice device, StorageContainer storage, string path,
        AudioLoadMode mode = AudioLoadMode.Decoded)
        : this(device, ReadStorage(storage, path), mode)
    {
    }

    public AudioStream(AudioDevice device, Stream source, AudioLoadMode mode = AudioLoadMode.Decoded)
        : this(device, ReadAllBytes(source), mode)
    {
    }

    public AudioStream(AudioDevice device, ReadOnlySpan<byte> encodedData, AudioLoadMode mode = AudioLoadMode.Decoded)
        : this(device, encodedData.ToArray(), mode)
    {
    }

    private AudioStream(AudioDevice device, byte[] encodedData, AudioLoadMode mode)
    {
        _device = device;
        _device.CreateStream(this, encodedData, mode);
    }

    public void Dispose()
    {
        DisposeFromDevice();
    }

    internal void DisposeFromDevice()
    {
        if (!IsDisposed)
        {
            _device.DisposeStream(this);
            IsDisposed = true;
        }
    }

    private static byte[] ReadStorage(StorageContainer storage, string path)
    {
        using var source = storage.OpenRead(path);
        return ReadAllBytes(source);
    }

    private static byte[] ReadAllBytes(Stream source)
    {
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return buffer.ToArray();
    }
}
