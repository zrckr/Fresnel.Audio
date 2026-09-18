namespace Fresnel.Audio;

/// <summary>
/// Routes audio through a hierarchy of buses.
/// </summary>
public abstract class AudioMixer
{
    /// <summary>
    /// The root bus that routes directly to the audio device.
    /// </summary>
    public AudioBus Master { get; }

    /// <summary>
    /// All buses in this mixer, indexed by name.
    /// </summary>
    public IReadOnlyDictionary<string, AudioBus> Buses => _buses;

    /// <summary>
    /// Creates a mixer using the supplied root bus.
    /// </summary>
    protected AudioMixer(AudioBus master)
    {
        Master = AddBus(nameof(Master), master, routeTo: null);
    }

    private readonly OrderedDictionary<string, AudioBus> _buses = new(StringComparer.Ordinal);

    private readonly Dictionary<AudioBus, AudioBus?> _routes = [];

    internal event Action? Changed;

    /// <summary>
    /// Adds a named bus, optionally routed through another bus in this mixer.
    /// </summary>
    /// <remarks>
    /// This method is intended for derived mixer types that expose their fixed bus layout. When
    /// <paramref name="routeTo"/> is omitted, the bus routes through <see cref="Master"/>.
    /// </remarks>
    protected AudioBus AddBus(string name, AudioBus bus, AudioBus? routeTo = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_buses.Count == 0)
        {
            if (routeTo != null)
            {
                throw new ArgumentException("The master bus cannot have a destination.", nameof(routeTo));
            }
        }
        else
        {
            routeTo ??= Master;
            if (!ReferenceEquals(routeTo.Mixer, this))
            {
                throw new ArgumentException("The destination belongs to another mixer.", nameof(routeTo));
            }
        }

        if (bus.Mixer != null)
        {
            throw new ArgumentException("The bus is already registered with a mixer.", nameof(bus));
        }

        if (!_buses.TryAdd(name, bus))
        {
            throw new ArgumentException($"A bus named '{name}' already exists.", nameof(name));
        }

        bus.Name = name;
        bus.Mixer = this;
        _routes.Add(bus, routeTo);
        bus.Changed += BusChanged;
        Changed?.Invoke();

        return bus;
    }

    private void BusChanged()
    {
        Changed?.Invoke();
    }

    internal AudioBus? GetDestination(AudioBus bus)
    {
        if (!ReferenceEquals(bus.Mixer, this) || !_routes.TryGetValue(bus, out var destination))
        {
            throw new ArgumentException("The bus does not belong to this mixer.", nameof(bus));
        }

        return destination;
    }

    internal float GetLocalOutputGain(AudioBus bus)
    {
        if (!ReferenceEquals(bus.Mixer, this))
        {
            throw new ArgumentException("The bus belongs to another mixer.", nameof(bus));
        }

        var anySolo = _buses.Values.Any(candidate => candidate.Solo);
        var inSoloSubtree = false;
        var hasGain = true;

        for (var current = bus; current is not null; current = current.Parent)
        {
            if (current.Muted)
            {
                hasGain = false;
                break;
            }

            inSoloSubtree |= current.Solo;
        }

        if (!hasGain || (anySolo && !inSoloSubtree))
        {
            return 0f;
        }

        return bus.Volume.ToLinear();
    }

    internal (float Left, float Right) GetStereoOutput(AudioBus bus)
    {
        if (!ReferenceEquals(bus.Mixer, this))
        {
            throw new ArgumentException("The bus belongs to another mixer.", nameof(bus));
        }

        var left = 1f;
        var right = 1f;
        for (var current = bus; current is not null; current = current.Parent)
        {
            var pan = current.Pan;
            left *= 1f - Math.Max(pan, 0f);
            right *= 1f + Math.Min(pan, 0f);
        }

        return (left, right);
    }
}
