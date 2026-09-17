namespace Fresnel.Audio;

/// <summary>
/// Routes audio through a hierarchy of buses.
/// </summary>
public class AudioMixer
{
    /// <summary>
    /// The root bus that routes directly to the audio device.
    /// </summary>
    public AudioBus Master { get; }

    /// <summary>
    /// All buses in this mixer, indexed by name.
    /// </summary>
    public IReadOnlyDictionary<string, AudioBus> Buses => _buses;

    private readonly OrderedDictionary<string, AudioBus> _buses = new(StringComparer.Ordinal);

    internal event Action? Changed;

    /// <summary>
    /// Creates a mixer with a master bus.
    /// </summary>
    public AudioMixer(AudioBusConfig? masterConfig = null)
    {
        Master = new AudioBus(this, nameof(Master), masterConfig ?? new AudioBusConfig(), parent: null);
        _buses.Add(Master.Name, Master);
        Master.Changed += BusChanged;
    }

    /// <summary>
    /// Adds a named bus, optionally routed through another bus in this mixer.
    /// </summary>
    /// <remarks>
    /// This method is intended for derived mixer types that expose their fixed bus layout.
    /// </remarks>
    protected AudioBus AddBus(string name, AudioBusConfig config, AudioBus? routeTo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        routeTo ??= Master;
        if (!ReferenceEquals(routeTo.Mixer, this))
        {
            throw new ArgumentException("The parent belongs to another mixer.", nameof(routeTo));
        }

        if (_buses.ContainsKey(name))
        {
            throw new ArgumentException($"A bus named '{name}' already exists.", nameof(name));
        }

        var bus = new AudioBus(this, name, config, routeTo);
        _buses.Add(name, bus);
        bus.Changed += BusChanged;

        return bus;
    }

    private void BusChanged()
    {
        Changed?.Invoke();
    }

    internal (float Volume, float Left, float Right) GetMixedOutput(AudioBus bus)
    {
        if (!ReferenceEquals(bus.Mixer, this))
        {
            throw new ArgumentException("The bus belongs to another mixer.", nameof(bus));
        }

        var anySolo = _buses.Values.Any(candidate => candidate.Solo);
        var inSoloSubtree = false;
        var muted = false;

        var volume = 1f;
        var left = 1f;
        var right = 1f;

        for (var current = bus; current is not null; current = current.Parent)
        {
            inSoloSubtree |= current.Solo;
            muted |= current.Muted || current.Config.Muted;

            volume *= current.Config.Volume.ToLinear();
            volume *= current.Volume.ToLinear();

            var pan = Math.Clamp(current.Config.Pan + current.Pan, -1f, 1f);
            left *= 1f - Math.Max(pan, 0f);
            right *= 1f + Math.Min(pan, 0f);
        }

        if (muted || (anySolo && !inSoloSubtree))
        {
            volume = 0f;
        }

        return (volume, left, right);
    }
}
