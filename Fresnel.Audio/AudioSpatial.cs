using System.Numerics;

namespace Fresnel.Audio;

/// <summary>
/// Specifies how an <see cref="AudioPlayer"/> is spatialized.
/// </summary>
public abstract class AudioSpatial : IEquatable<AudioSpatial>
{
    private AudioSpatial() { }

    internal event Action? Changed;

    /// <summary>
    /// Determines whether this spatial configuration is equal to another configuration.
    /// </summary>
    public bool Equals(AudioSpatial? other) => other != null && EqualsInternal(other);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AudioSpatial other && Equals(other);

    /// <inheritdoc/>
    public abstract override int GetHashCode();

    /// <summary>
    /// Determines whether this configuration equals another configuration of the same type.
    /// </summary>
    protected abstract bool EqualsInternal(AudioSpatial other);

    /// <summary>
    /// Disables spatial audio and uses the bus's stereo balance.
    /// </summary>
    public sealed class None : AudioSpatial
    {
        /// <inheritdoc/>
        protected override bool EqualsInternal(AudioSpatial other) => other is None;

        /// <inheritdoc/>
        public override int GetHashCode() => typeof(None).GetHashCode();
    }

    /// <summary>
    /// Spatializes audio at a position in the XZ plane.
    /// </summary>
    public sealed class Spatial2D : AudioSpatial
    {
        private Vector2 _position;

        /// <summary>
        /// Gets or sets the source position, where X and Y map to world X and Z.
        /// </summary>
        public Vector2 Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>
        /// Creates a 2D spatial configuration at the given position.
        /// </summary>
        public Spatial2D(Vector2 position = default)
        {
            _position = position;
        }

        /// <inheritdoc/>
        protected override bool EqualsInternal(AudioSpatial other) =>
            other is Spatial2D spatial && _position == spatial._position;

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(typeof(Spatial2D), _position);

    }

    /// <summary>
    /// Spatializes audio at a three-dimensional world position.
    /// </summary>
    public sealed class Spatial3D : AudioSpatial
    {
        private Vector3 _position;

        /// <summary>
        /// Gets or sets the source's world-space position.
        /// </summary>
        public Vector3 Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>
        /// Creates a 3D spatial configuration at the given position.
        /// </summary>
        public Spatial3D(Vector3 position = default)
        {
            _position = position;
        }

        /// <inheritdoc/>
        protected override bool EqualsInternal(AudioSpatial other) =>
            other is Spatial3D spatial && _position == spatial._position;

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(typeof(Spatial3D), _position);

    }
}
