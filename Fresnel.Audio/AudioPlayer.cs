namespace Fresnel.Audio;

public sealed class AudioPlayer : IDisposable
{
    public Db Volume
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
    } = Db.Zero;

    public int MaxPolyphony
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (field != value)
            {
                field = value;
                Changed?.Invoke();
            }
        }
    } = 1;

    public bool Playing
    {
        get => _device.IsPlayerPlaying(this);
        set
        {
            if (value)
            {
                Play();
            }
            else
            {
                Stop();
            }
        }
    }

    public bool StreamPaused
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

    internal AudioStream AudioStream { get; }

    internal event Action? Changed;

    private readonly AudioDevice _device;

    public AudioPlayer(AudioDevice device, AudioStream stream, AudioBus bus)
    {
        _device = device;
        AudioStream = stream;
        AudioStream.Changed += StreamChanged;

        try
        {
            device.CreatePlayer(this, bus);
        }
        catch
        {
            AudioStream.Changed -= StreamChanged;
            throw;
        }
    }

    public void Play(TimeSpan fromPosition = default)
    {
        _device.Play(this, fromPosition);
    }

    public void Seek(TimeSpan toPosition = default)
    {
        _device.Seek(this, toPosition);
    }

    public void Stop()
    {
        _device.Stop(this);
    }

    public void Dispose()
    {
        DisposeFromDevice();
    }

    internal void DisposeFromDevice()
    {
        if (!IsDisposed)
        {
            _device.DisposePlayer(this);
            AudioStream.Changed -= StreamChanged;
            IsDisposed = true;
        }
    }

    private void StreamChanged()
    {
        Changed?.Invoke();
    }
}
