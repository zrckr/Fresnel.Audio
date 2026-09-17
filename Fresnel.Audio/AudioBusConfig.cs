namespace Fresnel.Audio;

/// <summary>
/// Specifies the initial settings for an <see cref="AudioBus"/>.
/// </summary>
public record struct AudioBusConfig()
{
    /// <summary>
    /// Gets the initial volume adjustment in decibels.
    /// </summary>
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

    /// <summary>
    /// Gets whether the bus starts muted.
    /// </summary>
    public bool Muted { get; init; }

    /// <summary>
    /// Gets a list of effects.
    /// </summary>
    public IList<IEffect> Effects { get; init; } = [];
}
