using System.Text.Json.Serialization;

namespace Fresnel.Audio;

public sealed class AudioBus
{
    public string Name { get; }

    public string RouteTo { get => _routeTo?.Invoke()?.Name ?? field; } = "";

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

    public float Pan
    {
        get;
        set
        {
            value = Math.Clamp(value, -1f, 1f);
            if (Math.Abs(field - value) > float.Epsilon)
            {
                field = value;
                Changed?.Invoke();
            }
        }
    }

    internal event Action? Changed;

    [JsonIgnore] private readonly Func<AudioBus?>? _routeTo;

    public AudioBus(string name, Func<AudioBus?>? routeTo = null)
    {
        Name = name;
        _routeTo = routeTo;
    }

    [JsonConstructor]
    public AudioBus(string name, string routeTo)
    {
        Name = name;
        RouteTo = routeTo;
    }
}

public class AudioBusLayout
{
    public AudioBus Master { get; private set; } = new("Master");
}
