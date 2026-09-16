using System.Numerics;

namespace Fresnel.Audio;

public sealed class AudioPlayer : IDisposable
{
    public Db Volume
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                UpdateTrackStates();
            }
        }
    } = 0f;

    public int MaxVoices
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (field != value)
            {
                field = value;
                TrimTracks();
                UpdateTrackStates();
            }
        }
    } = 1;

    public PlaybackState State
    {
        get
        {
            if (!_tracks.Any(track => _device.TrackIsActive(track.Handle)))
            {
                return PlaybackState.Stopped;
            }

            return _tracks.Any(track => _device.TrackIsPlaying(track.Handle))
                ? PlaybackState.Playing
                : PlaybackState.Paused;
        }
    }

    public float PlaybackRate
    {
        get;
        set
        {
            if (!float.IsFinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Playback rate must be finite and positive.");
            }

            if (Math.Abs(field - value) > float.Epsilon)
            {
                field = value;
                UpdateTrackStates();
            }
        }
    } = 1f;

    public bool Looping
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                UpdateTrackStates();
            }
        }
    }

    public TimeSpan Position
    {
        get
        {
            var track = GetLatestTrack();
            return track != null ? _device.TrackGetPosition(track.Handle) : TimeSpan.Zero;
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            var track = GetLatestTrack();
            if (track != null)
            {
                _device.TrackSeek(track.Handle, value);
            }
        }
    }

    public AudioSpatial Spatial
    {
        get => _spatial;
        set
        {
            if (!ReferenceEquals(_spatial, value))
            {
                var outputChanged = !_spatial.Equals(value);
                _spatial.Changed -= UpdateTrackStates;
                _spatial = value;
                _spatial.Changed += UpdateTrackStates;
                if (outputChanged)
                {
                    UpdateTrackStates();
                }
            }
        }
    }

    public bool IsDisposed { get; private set; }

    public int ActiveVoices => _tracks.Count(track => _device.TrackIsActive(track.Handle));

    private readonly AudioStream _stream;

    private readonly AudioBus _bus;

    private readonly AudioDevice _device;

    private readonly AudioListener _listener;

    private AudioSpatial _spatial = new AudioSpatial.None();

    private readonly List<Track> _tracks = new();

    private long _nextSequence;

    internal AudioPlayer(AudioDevice device, AudioListener listener, AudioStream stream, AudioBus bus)
    {
        _device = device;
        _listener = listener;
        _stream = stream;
        _bus = bus;
        _bus.Mixer.Changed += UpdateTrackStates;
        _listener.Changed += UpdateTrackStates;
        _spatial.Changed += UpdateTrackStates;
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            foreach (var track in _tracks)
            {
                _device.TrackDestroy(track.Handle);
            }

            _tracks.Clear();
            _stream.RemovePlayer(this);
            _bus.Mixer.Changed -= UpdateTrackStates;
            _listener.Changed -= UpdateTrackStates;
            _spatial.Changed -= UpdateTrackStates;
            IsDisposed = true;
        }
    }

    public void Play(TimeSpan fromPosition = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(fromPosition, TimeSpan.Zero);
        if (_stream.Duration != fromPosition)
        {
            var track = GetOrCreateTrack();
            UpdateTrackState(track);
            _device.TrackPlay(track.Handle, fromPosition, Looping);
            track.Sequence = ++_nextSequence;
        }
    }

    public void Seek(TimeSpan toPosition = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(toPosition, TimeSpan.Zero);
        foreach (var track in _tracks)
        {
            if (!_device.TrackIsActive(track.Handle))
            {
                continue;
            }

            if (_stream.Duration == toPosition)
            {
                _device.TrackStop(track.Handle);
            }
            else
            {
                _device.TrackPlay(track.Handle, toPosition, Looping);
            }
        }
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        foreach (var track in _tracks)
        {
            if (_device.TrackIsActive(track.Handle))
            {
                _device.TrackSetPaused(track.Handle, true);
            }
        }
    }

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        foreach (var track in _tracks)
        {
            if (_device.TrackIsActive(track.Handle))
            {
                _device.TrackSetPaused(track.Handle, false);
            }
        }
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        foreach (var track in _tracks)
        {
            if (_device.TrackIsActive(track.Handle))
            {
                _device.TrackStop(track.Handle);
            }
        }
    }

    private Track GetOrCreateTrack()
    {
        var voice = _tracks.FirstOrDefault(v => !_device.TrackIsActive(v.Handle));
        if (voice != null)
        {
            return voice;
        }

        if (_tracks.Count < MaxVoices)
        {
            voice = new Track(_device.TrackCreate(_stream.Handle));
            _tracks.Add(voice);
            return voice;
        }

        voice = _tracks.MinBy(v => v.Sequence)!;
        _device.TrackStop(voice.Handle);
        return voice;
    }

    private Track? GetLatestTrack()
    {
        var maxSequence = 0L;
        Track? latestTrack = null;

        foreach (var track in _tracks)
        {
            if (_device.TrackIsActive(track.Handle) && maxSequence < track.Sequence)
            {
                maxSequence = track.Sequence;
                latestTrack = track;
            }
        }

        return latestTrack;
    }

    private void UpdateTrackState(Track track)
    {
        var (volume, left, right) = _bus.Mixer.GetMixedOutput(_bus);
        _device.TrackSetPlaybackRate(track.Handle, PlaybackRate);
        _device.TrackSetLooping(track.Handle, Looping);
        _device.TrackSetGain(track.Handle, Volume.ToLinear() * volume);

        if (GetSpatialPosition() is { } position)
        {
            _device.TrackSet3DPosition(track.Handle, position.X, position.Y, position.Z);
        }
        else
        {
            _device.TrackSetStereo(track.Handle, left, right);
        }
    }

    private Vector3? GetSpatialPosition()
    {
        var worldPosition = _spatial switch
        {
            AudioSpatial.None => default,
            AudioSpatial.Spatial2D spatial => new Vector3(spatial.Position.X, 0f, spatial.Position.Y),
            AudioSpatial.Spatial3D spatial => spatial.Position,
            _ => throw new InvalidOperationException("Unknown audio spatial mode.")
        };

        if (_spatial is AudioSpatial.None)
        {
            return null;
        }

        var rotation = Quaternion.Inverse(_listener.Rotation);
        var position = Vector3.Transform(worldPosition - _listener.Position, rotation) / _listener.ReferenceDistance;

        return position;
    }


    private void UpdateTrackStates()
    {
        if (!IsDisposed)
        {
            foreach (var track in _tracks)
            {
                UpdateTrackState(track);
            }
        }
    }

    private void TrimTracks()
    {
        while (_tracks.Count > MaxVoices)
        {
            var track = _tracks.MinBy(track => track.Sequence)!;
            _device.TrackStop(track.Handle);
            _device.TrackDestroy(track.Handle);
            _tracks.Remove(track);
        }
    }

    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused
    }

    private sealed class Track(AudioDevice.ResourceHandle handle)
    {
        public readonly AudioDevice.ResourceHandle Handle = handle;
        public long Sequence;
    }
}
