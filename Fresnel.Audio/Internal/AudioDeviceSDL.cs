using Foster.Framework;

// ReSharper disable once CheckNamespace
// ReSharper disable once InconsistentNaming

namespace Fresnel.Audio;

internal sealed unsafe class AudioDeviceSDL : AudioDevice
{
    private const uint DefaultPlaybackDevice = uint.MaxValue;

    private readonly HashSet<nint> _streams = new();

    private readonly HashSet<nint> _tracks = new();

    private bool _mixerInitialized;

    private void* _mixer;

    internal AudioDeviceSDL(App app)
    {
        if (!app.IsMainThread())
        {
            throw new InvalidOperationException("Audio devices must be created on Foster's main thread.");
        }

        try
        {
            if (!SDL3.SDL3_Mixer.Init())
            {
                throw Error(nameof(SDL3.SDL3_Mixer.Init));
            }

            _mixerInitialized = true;
            _mixer = SDL3.SDL3_Mixer.CreateMixerDevice(DefaultPlaybackDevice, null);
            if (_mixer == null)
            {
                throw Error(nameof(SDL3.SDL3_Mixer.CreateMixerDevice));
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public override void Dispose()
    {
        foreach (var track in _tracks)
        {
            SDL3.SDL3_Mixer.DestroyTrack((void*)track);
        }

        _tracks.Clear();
        foreach (var stream in _streams)
        {
            SDL3.SDL3_Mixer.DestroyAudio((void*)stream);
        }

        _streams.Clear();
        if (_mixer != null)
        {
            SDL3.SDL3_Mixer.DestroyMixer(_mixer);
            _mixer = null;
        }

        if (_mixerInitialized)
        {
            SDL3.SDL3_Mixer.Quit();
            _mixerInitialized = false;
        }
    }

    internal override ResourceHandle StreamCreate(ReadOnlySpan<byte> bytes, AudioLoadMode mode)
    {
        ObjectDisposedException.ThrowIf(_mixer == null, this);
        if (bytes.IsEmpty)
        {
            throw new ArgumentException("Audio data must not be empty.", nameof(bytes));
        }

        fixed (byte* data = bytes)
        {
            var io = SDL3.SDL.SDL_IOFromConstMem((nint)data, (nuint)bytes.Length);
            if (io == 0)
            {
                throw Error(nameof(SDL3.SDL.SDL_IOFromConstMem));
            }

            var stream = SDL3.SDL3_Mixer.LoadAudioIO(_mixer, (void*)io, mode == AudioLoadMode.Decoded, true);
            if (stream == null)
            {
                throw Error(nameof(SDL3.SDL3_Mixer.LoadAudioIO));
            }

            _streams.Add((nint)stream);
            return new ResourceHandle((nint)stream);
        }
    }

    internal override TimeSpan? StreamGetDuration(ResourceHandle handle)
    {
        var stream = GetStream(handle);
        var frames = SDL3.SDL3_Mixer.GetAudioDuration(stream);
        if (frames < 0)
        {
            return null;
        }

        var milliseconds = SDL3.SDL3_Mixer.AudioFramesToMilliseconds(stream, frames);
        return milliseconds < 0 ? null : TimeSpan.FromMilliseconds(milliseconds);
    }

    internal override void StreamDestroy(ResourceHandle handle)
    {
        if (_streams.Remove(handle.Id))
        {
            SDL3.SDL3_Mixer.DestroyAudio((void*)handle.Id);
        }
    }

    internal override ResourceHandle TrackCreate(ResourceHandle streamHandle)
    {
        var track = SDL3.SDL3_Mixer.CreateTrack(GetMixer());
        if (track == null)
        {
            throw Error(nameof(SDL3.SDL3_Mixer.CreateTrack));
        }

        if (!SDL3.SDL3_Mixer.SetTrackAudio(track, GetStream(streamHandle)))
        {
            SDL3.SDL3_Mixer.DestroyTrack(track);
            throw Error(nameof(SDL3.SDL3_Mixer.SetTrackAudio));
        }

        _tracks.Add((nint)track);
        return new ResourceHandle((nint)track);
    }

    internal override void TrackDestroy(ResourceHandle handle)
    {
        if (_tracks.Remove(handle.Id))
        {
            SDL3.SDL3_Mixer.DestroyTrack((void*)handle.Id);
        }
    }

    internal override bool TrackIsActive(ResourceHandle handle)
    {
        var track = GetTrack(handle);
        return SDL3.SDL3_Mixer.TrackPlaying(track) || SDL3.SDL3_Mixer.TrackPaused(track);
    }

    internal override bool TrackIsPlaying(ResourceHandle handle)
    {
        return SDL3.SDL3_Mixer.TrackPlaying(GetTrack(handle));
    }

    internal override TimeSpan TrackGetPosition(ResourceHandle handle)
    {
        var track = GetTrack(handle);
        var frames = SDL3.SDL3_Mixer.GetTrackPlaybackPosition(track);
        var milliseconds = frames < 0 ? -1 : SDL3.SDL3_Mixer.TrackFramesToMilliseconds(track, frames);
        return milliseconds < 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(milliseconds);
    }

    internal override void TrackPlay(ResourceHandle handle, TimeSpan position, bool looping)
    {
        var properties = SDL3.SDL.SDL_CreateProperties();
        if (properties == 0)
        {
            throw Error(nameof(SDL3.SDL.SDL_CreateProperties));
        }

        try
        {
            var milliseconds = checked((long)position.TotalMilliseconds);
            if (!SDL3.SDL.SDL_SetNumberProperty(properties, SDL3.SDL3_Mixer.PropPlayStartMillisecondNumber,
                    milliseconds) ||
                !SDL3.SDL.SDL_SetNumberProperty(properties, SDL3.SDL3_Mixer.PropPlayLoopsNumber, looping ? -1 : 0))
            {
                throw Error(nameof(SDL3.SDL.SDL_SetNumberProperty));
            }

            if (!SDL3.SDL3_Mixer.PlayTrack(GetTrack(handle), properties))
            {
                throw Error(nameof(SDL3.SDL3_Mixer.PlayTrack));
            }
        }
        finally
        {
            SDL3.SDL.SDL_DestroyProperties(properties);
        }
    }

    internal override void TrackSeek(ResourceHandle handle, TimeSpan position)
    {
        var track = GetTrack(handle);
        var frames = SDL3.SDL3_Mixer.TrackMillisecondsToFrames(track, checked((long)position.TotalMilliseconds));
        if (frames < 0 || !SDL3.SDL3_Mixer.SetTrackPlaybackPosition(track, frames))
        {
            throw Error(nameof(SDL3.SDL3_Mixer.SetTrackPlaybackPosition));
        }
    }

    internal override void TrackStop(ResourceHandle handle)
    {
        var track = GetTrack(handle);
        if (TrackIsActive(handle) && !SDL3.SDL3_Mixer.StopTrack(track, 0))
        {
            throw Error(nameof(SDL3.SDL3_Mixer.StopTrack));
        }
    }

    internal override void TrackSetPlaybackRate(ResourceHandle handle, float rate)
    {
        if (!SDL3.SDL3_Mixer.SetTrackFrequencyRatio(GetTrack(handle), rate))
        {
            throw Error(nameof(SDL3.SDL3_Mixer.SetTrackFrequencyRatio));
        }
    }

    internal override void TrackSetLooping(ResourceHandle handle, bool looping)
    {
        if (!SDL3.SDL3_Mixer.SetTrackLoops(GetTrack(handle), looping ? -1 : 0))
        {
            throw Error(nameof(SDL3.SDL3_Mixer.SetTrackLoops));
        }
    }

    internal override void TrackSetOutput(ResourceHandle handle, float gain, float left, float right)
    {
        var track = GetTrack(handle);
        if (!SDL3.SDL3_Mixer.SetTrackGain(track, gain))
        {
            throw Error(nameof(SDL3.SDL3_Mixer.SetTrackGain));
        }

        var stereo = new SDL3.SDL3_Mixer.StereoGains { Left = left, Right = right };
        if (!SDL3.SDL3_Mixer.SetTrackStereo(track, &stereo))
        {
            throw Error(nameof(SDL3.SDL3_Mixer.SetTrackStereo));
        }
    }

    internal override void TrackSetPaused(ResourceHandle handle, bool paused)
    {
        var track = GetTrack(handle);
        var success = paused
            ? !SDL3.SDL3_Mixer.TrackPlaying(track) || SDL3.SDL3_Mixer.PauseTrack(track)
            : !SDL3.SDL3_Mixer.TrackPaused(track) || SDL3.SDL3_Mixer.ResumeTrack(track);
        if (!success)
        {
            throw Error(paused ? nameof(SDL3.SDL3_Mixer.PauseTrack) : nameof(SDL3.SDL3_Mixer.ResumeTrack));
        }
    }

    private void* GetMixer()
    {
        ObjectDisposedException.ThrowIf(_mixer == null, this);
        return _mixer;
    }

    private void* GetStream(ResourceHandle handle)
    {
        ObjectDisposedException.ThrowIf(_mixer == null, this);
        return _streams.Contains(handle.Id)
            ? (void*)handle.Id
            : throw new ArgumentException("The stream does not belong to this audio device.", nameof(handle));
    }

    private void* GetTrack(ResourceHandle handle)
    {
        ObjectDisposedException.ThrowIf(_mixer == null, this);
        return _tracks.Contains(handle.Id)
            ? (void*)handle.Id
            : throw new ArgumentException("The track does not belong to this audio device.", nameof(handle));
    }

    private static InvalidOperationException Error(string operation)
    {
        return new InvalidOperationException($"{operation}: {SDL3.SDL.SDL_GetError()}");
    }
}
