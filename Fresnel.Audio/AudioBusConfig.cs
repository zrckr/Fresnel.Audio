namespace Fresnel.Audio;

public record struct AudioBusConfig
{
    public Db Volume { get; init; }

    public float Pan
    {
        get => field;
        init
        {
            if (!float.IsFinite(value) || value is < -1f or > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            field = value;
        }
    }

    public bool Muted { get; init; }
}
