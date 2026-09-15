using Foster.Framework;

// ReSharper disable once CheckNamespace
// ReSharper disable once InconsistentNaming

namespace Fresnel.Audio;

internal unsafe sealed class AudioDeviceSDL: IDisposable
{
    private const uint DefaultPlaybackDevice = uint.MaxValue;

    private bool _mixerInitialized;

    private void* _mixer;

    internal AudioResource LoadAudio(ReadOnlySpan<byte> encodedData, AudioLoadMode mode)
    {
        ObjectDisposedException.ThrowIf(_mixer == null, this);
        if (encodedData.IsEmpty)
        {
            throw new ArgumentException("Audio data must not be empty.", nameof(encodedData));
        }

        fixed (byte* data = encodedData)
        {
            var io = SDL3.SDL.SDL_IOFromConstMem((nint)data, (nuint)encodedData.Length);
            if (io == 0)
            {
                throw new InvalidOperationException($"{nameof(SDL3.SDL.SDL_IOFromConstMem)}: {SDL3.SDL.SDL_GetError()}");
            }

            var audio = SDL3.SDL3_Mixer.LoadAudioIO(
                _mixer,
                (void*)io,
                mode == AudioLoadMode.Decoded,
                true);

            if (audio == null)
            {
                throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.LoadAudioIO)}: {SDL3.SDL.SDL_GetError()}");
            }

            return new AudioResource(audio);
        }
    }

    internal TrackResource CreateTrack(AudioResource audio)
    {
        ObjectDisposedException.ThrowIf(_mixer == null, this);

        var track = SDL3.SDL3_Mixer.CreateTrack(_mixer);
        if (track == null)
        {
            throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.CreateTrack)}: {SDL3.SDL.SDL_GetError()}");
        }

        if (!SDL3.SDL3_Mixer.SetTrackAudio(track, audio.Audio))
        {
            SDL3.SDL3_Mixer.DestroyTrack(track);
            throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.SetTrackAudio)}: {SDL3.SDL.SDL_GetError()}");
        }

        return new TrackResource(track, audio);
    }

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
                throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.Init)}: {SDL3.SDL.SDL_GetError()}");
            }

            _mixerInitialized = true;
            _mixer = SDL3.SDL3_Mixer.CreateMixerDevice(DefaultPlaybackDevice, null);
            if (_mixer == null)
            {
                throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.CreateMixerDevice)}: {SDL3.SDL.SDL_GetError()}");
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
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

    internal sealed class AudioResource: IDisposable
    {
        internal readonly TimeSpan? Duration;

        private void* _audio;

        internal void* Audio
        {
            get
            {
                ObjectDisposedException.ThrowIf(_audio == null, this);
                return _audio;
            }
        }

        internal AudioResource(void* audio)
        {
            _audio = audio;
            var frames = SDL3.SDL3_Mixer.GetAudioDuration(audio);
            if (frames < 0)
            {
                return;
            }

            var milliseconds = SDL3.SDL3_Mixer.AudioFramesToMilliseconds(audio, frames);
            if (milliseconds >= 0)
            {
                Duration = TimeSpan.FromMilliseconds(milliseconds);
            }
        }

        public void Dispose()
        {
            if (_audio != null)
            {
                SDL3.SDL3_Mixer.DestroyAudio(_audio);
                _audio = null;
            }
        }
    }

    internal sealed class TrackResource: IDisposable
    {
        private void* Track
        {
            get
            {
                ObjectDisposedException.ThrowIf(_track == null, this);
                return _track;
            }
        }

        private void* _track;

        private readonly AudioResource _audio;

        internal bool Playing => _track != null &&
                                 (SDL3.SDL3_Mixer.TrackPlaying(_track) ||
                                  SDL3.SDL3_Mixer.TrackPaused(_track));

        internal TrackResource(void* track, AudioResource audio)
        {
            _track = track;
            _audio = audio;
        }

        internal void Play(TimeSpan fromPosition, bool looping)
        {
            var properties = SDL3.SDL.SDL_CreateProperties();
            if (properties == 0)
            {
                throw new InvalidOperationException($"{nameof(SDL3.SDL.SDL_CreateProperties)}: {SDL3.SDL.SDL_GetError()}");
            }

            try
            {
                var milliseconds = checked((long)fromPosition.TotalMilliseconds);
                if (!SDL3.SDL.SDL_SetNumberProperty(
                        properties, SDL3.SDL3_Mixer.PropPlayStartMillisecondNumber, milliseconds) ||
                    !SDL3.SDL.SDL_SetNumberProperty(
                        properties, SDL3.SDL3_Mixer.PropPlayLoopsNumber, looping ? -1: 0))
                {
                    throw new InvalidOperationException($"{nameof(SDL3.SDL.SDL_SetNumberProperty)}: {SDL3.SDL.SDL_GetError()}");
                }

                if (!SDL3.SDL3_Mixer.PlayTrack(Track, properties))
                {
                    throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.PlayTrack)}: {SDL3.SDL.SDL_GetError()}");
                }
            }
            finally
            {
                SDL3.SDL.SDL_DestroyProperties(properties);
            }
        }

        internal void Stop()
        {
            if (Playing && !SDL3.SDL3_Mixer.StopTrack(Track, 0))
            {
                throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.StopTrack)}: {SDL3.SDL.SDL_GetError()}");
            }
        }

        internal void SetPaused(bool paused)
        {
            var success = paused
                ? !SDL3.SDL3_Mixer.TrackPlaying(Track) || SDL3.SDL3_Mixer.PauseTrack(Track)
                : !SDL3.SDL3_Mixer.TrackPaused(Track) || SDL3.SDL3_Mixer.ResumeTrack(Track);

            if (!success)
            {
                throw new InvalidOperationException($"{(paused ? nameof(SDL3.SDL3_Mixer.PauseTrack): nameof(SDL3.SDL3_Mixer.ResumeTrack))}: {SDL3.SDL.SDL_GetError()}");
            }
        }

        internal void SetPlaybackRate(float rate)
        {
            if (!SDL3.SDL3_Mixer.SetTrackFrequencyRatio(Track, rate))
            {
                throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.SetTrackFrequencyRatio)}: {SDL3.SDL.SDL_GetError()}");
            }
        }

        internal void SetLooping(bool looping)
        {
            if (!SDL3.SDL3_Mixer.SetTrackLoops(Track, looping ? -1: 0))
            {
                throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.SetTrackLoops)}: {SDL3.SDL.SDL_GetError()}");
            }
        }

        internal void SetOutput(float gain, float left, float right)
        {
            if (!SDL3.SDL3_Mixer.SetTrackGain(Track, gain))
            {
                throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.SetTrackGain)}: {SDL3.SDL.SDL_GetError()}");
            }

            var stereo = new SDL3.SDL3_Mixer.StereoGains { Left = left, Right = right };
            if (!SDL3.SDL3_Mixer.SetTrackStereo(Track, &stereo))
            {
                throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.SetTrackStereo)}: {SDL3.SDL.SDL_GetError()}");
            }
        }

        internal void Seek(TimeSpan position)
        {
            var milliseconds = checked((long)position.TotalMilliseconds);
            var frames = SDL3.SDL3_Mixer.AudioMillisecondsToFrames(_audio.Audio, milliseconds);
            if (frames < 0 || !SDL3.SDL3_Mixer.SetTrackPlaybackPosition(Track, frames))
            {
                throw new InvalidOperationException($"{nameof(SDL3.SDL3_Mixer.SetTrackPlaybackPosition)}: {SDL3.SDL.SDL_GetError()}");
            }
        }

        public void Dispose()
        {
            if (_track != null)
            {
                SDL3.SDL3_Mixer.DestroyTrack(_track);
                _track = null;
            }
        }
    }
}
