using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Fresnel.Audio;

/// <summary>
/// Configures a mixer bus with independent volume, pan, mute, and solo controls.
/// </summary>
public sealed class AudioBus
{
    /// <summary>
    /// Gets the unique name of this bus within its mixer, or <see langword="null"/> when it is unregistered.
    /// </summary>
    [JsonIgnore]
    public string? Name { get; internal set; }

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
    }

    /// <summary>
    /// Gets or sets the stereo balance for non-spatial players routed through this bus.
    /// </summary>
    /// <remarks>
    /// Spatial players derive their channel balance from their position and ignore bus pan.
    /// </remarks>
    /// <value>
    /// -1 for fully left, 0 for center, and 1 for fully right.
    /// </value>
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

    /// <summary>
    /// Gets or initializes the effects processed by this bus.
    /// </summary>
    public ImmutableArray<IEffect> Effects { get; init; } = [];

    /// <summary>
    /// Gets the bus this bus routes through, or <see langword="null"/> for the master bus or an unregistered bus.
    /// </summary>
    [JsonIgnore] public AudioBus? Parent => Mixer.GetDestination(this);

    internal AudioMixer Mixer { get; set; } = null!;

    internal IList<EffectProcessor> EffectProcessors { get; } = new List<EffectProcessor>();

    internal event Action? Changed;

    private bool _effectInitialized;

    internal void InitializeEffects(int sampleRate, int channels)
    {
        if (_effectInitialized)
        {
            return;
        }

        foreach (var effect in Effects)
        {
            effect.Validate();
            EffectProcessors.Add(effect switch
            {
                ChorusEffect chorus => new ChorusProcessor(chorus, sampleRate, channels),
                CompressorEffect compressor => new CompressorProcessor(compressor, sampleRate, channels),
                DistortionEffect distortion => new DistortionProcessor(distortion, sampleRate, channels),
                EchoEffect echo => new EchoProcessor(echo, sampleRate, channels),
                EqualizerEffect equalizer => new EqualizerProcessor(equalizer, sampleRate, channels),
                FlangerEffect flanger => new FlangerProcessor(flanger, sampleRate, channels),
                ReverbEffect reverb => new ReverbProcessor(reverb, sampleRate, channels),
                RingModulatorEffect ringModulator => new RingModulatorProcessor(ringModulator, sampleRate, channels),
                _ => throw new NotSupportedException(
                    $"The effect type '{effect.GetType().Name}' has no processor implementation.")
            });
        }

        _effectInitialized = true;
    }
}
