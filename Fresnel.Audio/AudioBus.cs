namespace Fresnel.Audio;

public sealed class AudioBus
{
    public string Name { get; }

    public AudioBus? Parent { get; }

    public AudioBusConfig Config { get; }

    public AudioMixer Mixer { get; }

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
    } = 0f;

    /// <summary>
    /// Gets or sets the stereo balance for non-spatial players routed through this bus.
    /// </summary>
    /// <remarks>
    /// Spatial players derive their channel balance from their position and ignore bus pan.
    /// </remarks>
    /// <value>-1 for fully left, 0 for center, and 1 for fully right.</value>
    public float Pan
    {
        get => field;
        set
        {
            if (!float.IsFinite(value) || value is < -1f or > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (Math.Abs(field - value) > float.Epsilon)
            {
                field = value;
                Changed?.Invoke();
            }
        }
    }

    public bool Muted
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

    public bool Solo
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

    internal event Action? Changed;

    internal AudioBus(AudioMixer mixer, string name, AudioBusConfig config, AudioBus? parent)
    {
        Mixer = mixer;
        Name = name;
        Config = config;
        Parent = parent;
    }
}
