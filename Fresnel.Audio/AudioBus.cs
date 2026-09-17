namespace Fresnel.Audio;

/// <summary>
/// A mixer route with independent volume, pan, mute, and solo controls.
/// </summary>
public sealed class AudioBus
{
    /// <summary>
    /// The unique name of this bus within its mixer.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The bus this bus routes through, or <see langword="null"/> for the master bus.
    /// </summary>
    public AudioBus? Parent { get; }

    /// <summary>
    /// The configuration supplied when this bus was created.
    /// </summary>
    public AudioBusConfig Config { get; }

    /// <summary>
    /// The mixer that owns this bus.
    /// </summary>
    public AudioMixer Mixer { get; }

    /// <summary>
    /// Gets or sets this bus's runtime volume adjustment in decibels.
    /// </summary>
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

    /// <summary>
    /// Gets or sets whether this bus and all buses routed through it are silent.
    /// </summary>
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

    /// <summary>
    /// Gets or sets whether this bus and its descendants are the only audible buses in the mixer.
    /// </summary>
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
