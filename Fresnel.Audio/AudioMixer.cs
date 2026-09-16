namespace Fresnel.Audio;

public class AudioMixer
{
    public AudioBus Master { get; }

    public IReadOnlyDictionary<string, AudioBus> Buses => _buses;

    private readonly OrderedDictionary<string, AudioBus> _buses = new(StringComparer.Ordinal);

    internal event Action? Changed;

    public AudioMixer(AudioBusConfig? masterConfig = null)
    {
        Master = new AudioBus(this, nameof(Master), masterConfig ?? new AudioBusConfig(), parent: null);
        _buses.Add(Master.Name, Master);
        Master.Changed += BusChanged;
    }

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

    internal BusOutput GetOutput(AudioBus bus)
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

        return new BusOutput(volume, left, right);
    }

    internal readonly record struct BusOutput(float Volume, float Left, float Right);
}
