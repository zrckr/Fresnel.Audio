namespace Fresnel.Audio;

public record struct AudioBusConfig
{
    public Db Volume { get; init; }

    /// <summary>
    /// Gets the initial stereo balance for non-spatial players routed through this bus.
    /// </summary>
    /// <remarks>
    /// Spatial players derive their channel balance from their position and ignore bus pan.
    /// </remarks>
    /// <value>-1 for fully left, 0 for center, and 1 for fully right.</value>
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
