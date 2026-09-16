using System.Numerics;
using Foster.Framework;

namespace Fresnel.Audio;

public sealed class AudioListener
{
    /// <summary>
    /// TODO
    /// </summary>
    public Vector3 Position { get; private set; }

    /// <summary>
    /// Identity faces -Z with +Y as up.
    /// </summary>
    public Quaternion Rotation { get; private set; } = Quaternion.Identity;

    /// <summary>
    /// World-space distance that maps to SDL's reference distance of 1.
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

    public void SetTransform(Vector2 position, float rotation = 0f)
    {
        SetTransform(new Vector3(position.X, 0f, position.Y),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, -rotation));
    }

    public void SetTransform(Transform transform)
    {
        SetTransform(new Vector3(transform.Position.X, 0f, transform.Position.Y),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, -transform.Rotation));
    }

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
