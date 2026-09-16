using System.Numerics;

namespace Fresnel.Audio;

public abstract class AudioSpatial : IEquatable<AudioSpatial>
{
    private AudioSpatial() { }

    internal event Action? Changed;

    public bool Equals(AudioSpatial? other) => other is not null && EqualsInternal(other);

    public override bool Equals(object? obj) => obj is AudioSpatial other && Equals(other);

    public abstract override int GetHashCode();

    protected abstract bool EqualsInternal(AudioSpatial other);

    public sealed class None : AudioSpatial
    {
        protected override bool EqualsInternal(AudioSpatial other) => other is None;

        public override int GetHashCode() => typeof(None).GetHashCode();
    }

    public sealed class Spatial2D : AudioSpatial
    {
        private Vector2 _position;

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

        public Spatial2D(Vector2 position = default)
        {
            _position = position;
        }

        protected override bool EqualsInternal(AudioSpatial other) =>
            other is Spatial2D spatial && _position == spatial._position;

        public override int GetHashCode() => HashCode.Combine(typeof(Spatial2D), _position);

    }

    public sealed class Spatial3D : AudioSpatial
    {
        private Vector3 _position;

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

        public Spatial3D(Vector3 position = default)
        {
            _position = position;
        }

        protected override bool EqualsInternal(AudioSpatial other) =>
            other is Spatial3D spatial && _position == spatial._position;

        public override int GetHashCode() => HashCode.Combine(typeof(Spatial3D), _position);

    }
}
