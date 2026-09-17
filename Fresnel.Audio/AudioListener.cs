using System.Numerics;
using Foster.Framework;

namespace Fresnel.Audio;

/// <summary>
/// Defines the position and orientation from which spatial audio is heard.
/// </summary>
public sealed class AudioListener
{
    /// <summary>
    /// Gets the listener's world-space position.
    /// </summary>
    public Vector3 Position { get; private set; }

    /// <summary>
    /// Gets the listener's world-space orientation.
    /// <br/> Identity faces -Z with +Y as up.
    /// </summary>
    public Quaternion Rotation { get; private set; } = Quaternion.Identity;

    /// <summary>
    /// Gets or sets the world-space distance that maps to SDL's reference distance of 1.
    /// </summary>
    public float ReferenceDistance
    {
        get;
        set
        {
            if (Math.Abs(field - value) > float.Epsilon)
            {
                field = value;
                Changed?.Invoke();
            }
        }
    } = 1f;

    internal event Action? Changed;

    /// <summary>
    /// Sets the listener transform in the XZ plane.
    /// </summary>
    /// <remarks>
    /// The position's X and Y components map to world X and Z. Positive rotation turns clockwise when viewed from above.
    /// </remarks>
    public void SetTransform(Vector2 position, float rotation = 0f)
    {
        SetTransform(new Vector3(position.X, 0f, position.Y),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, -rotation));
    }

    /// <summary>
    /// Sets the listener transform from a Foster 2D transform.
    /// </summary>
    public void SetTransform(Transform transform)
    {
        SetTransform(new Vector3(transform.Position.X, 0f, transform.Position.Y),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, -transform.Rotation));
    }

    /// <summary>
    /// Sets the listener's world-space position and orientation.
    /// </summary>
    public void SetTransform(Vector3 position, Quaternion rotation)
    {
        var normalized = Quaternion.Normalize(rotation);
        if (Position != position || Rotation != normalized)
        {
            Position = position;
            Rotation = normalized;
            Changed?.Invoke();
        }
    }
}
