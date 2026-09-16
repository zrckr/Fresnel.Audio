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

    public bool IsDisposed { get; private set; }

    public int ActiveVoices => _tracks.Count(track => _device.TrackIsActive(track.Handle));

    private readonly AudioStream _stream;

    private readonly AudioBus _bus;

    private readonly AudioDevice _device;

    private readonly List<Track> _tracks = new();

    private long _nextSequence;

    internal AudioPlayer(AudioDevice device, AudioStream stream, AudioBus bus)
    {
        _device = device;
        _stream = stream;
        _bus = bus;
        _bus.Mixer.Changed += UpdateTrackStates;
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
        _device.TrackSetOutput(track.Handle, Volume.ToLinear() * volume, left, right);
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
