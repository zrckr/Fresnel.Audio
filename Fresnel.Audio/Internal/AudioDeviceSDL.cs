using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Foster.Framework;

// ReSharper disable once CheckNamespace
// ReSharper disable once InconsistentNaming

namespace Fresnel.Audio;

internal sealed unsafe partial class AudioDeviceSDL : AudioDevice
{
    private const uint DefaultPlaybackDevice = uint.MaxValue;

    private const int MaximumCallbackSamples = 65_536;

    private readonly HashSet<nint> _streams = new();

    private readonly HashSet<nint> _tracks = new();

    private readonly Dictionary<AudioBus, BusGroup> _busGroups = new();

    private BusGroup[] _rootBusGroups = Array.Empty<BusGroup>();

    private bool _mixerInitialized;

    private GCHandle _postMixHandle;

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

            _postMixHandle = GCHandle.Alloc(this);
            if (!SDL3.SDL3_Mixer.SetPostMixCallback(_mixer, &PostMix,
                    (void*)GCHandle.ToIntPtr(_postMixHandle)))
            {
                throw Error(nameof(SDL3.SDL3_Mixer.SetPostMixCallback));
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
        if (_mixer != null)
        {
            SDL3.SDL3_Mixer.SetPostMixCallback(_mixer, null, null);
        }

        if (_postMixHandle.IsAllocated)
        {
            _postMixHandle.Free();
        }

        foreach (var track in _tracks)
        {
            SDL3.SDL3_Mixer.DestroyTrack((void*)track);
        }

        _tracks.Clear();
        foreach (var group in _busGroups.Values)
        {
            group.Dispose();
        }

        _busGroups.Clear();
        Volatile.Write(ref _rootBusGroups, []);
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

    internal override ResourceHandle StreamCreateRaw(ReadOnlySpan<byte> bytes, int channels, int sampleRate)
    {
        ObjectDisposedException.ThrowIf(_mixer == null, this);
        if (bytes.IsEmpty)
        {
            throw new ArgumentException("Audio data must not be empty.", nameof(bytes));
        }

        var spec = new SDL3.SDL.SDL_AudioSpec
        {
            format = BitConverter.IsLittleEndian
                ? SDL3.SDL.SDL_AudioFormat.SDL_AUDIO_S16LE
                : SDL3.SDL.SDL_AudioFormat.SDL_AUDIO_S16BE,
            channels = channels,
            freq = sampleRate
        };

        fixed (byte* data = bytes)
        {
            var stream = SDL3.SDL3_Mixer.LoadRawAudio(_mixer, data, (nuint)bytes.Length, &spec);
            if (stream == null)
            {
                throw Error(nameof(SDL3.SDL3_Mixer.LoadRawAudio));
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

    internal override ResourceHandle TrackCreate(ResourceHandle streamHandle, AudioBus bus)
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

        try
        {
            var group = GetOrCreateBusGroup(bus);
            if (SDL3.SDL3_Mixer.SetTrackGroup(track, group.Group) == 0)
            {
                throw Error(nameof(SDL3.SDL3_Mixer.SetTrackGroup));
            }
        }
        catch
        {
            SDL3.SDL3_Mixer.DestroyTrack(track);
            throw;
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

    internal override void TrackSetGain(ResourceHandle handle, float gain)
    {
        var track = GetTrack(handle);
        if (!SDL3.SDL3_Mixer.SetTrackGain(track, gain))
        {
            throw Error(nameof(SDL3.SDL3_Mixer.SetTrackGain));
        }
    }

    internal override void TrackSetStereo(ResourceHandle handle, float left, float right)
    {
        var track = GetTrack(handle);
        var stereo = new SDL3.SDL3_Mixer.StereoGains { Left = left, Right = right };
        if (!SDL3.SDL3_Mixer.SetTrackStereo(track, &stereo))
        {
            throw Error(nameof(SDL3.SDL3_Mixer.SetTrackStereo));
        }
    }

    internal override void TrackSet3DPosition(ResourceHandle handle, float x, float y, float z)
    {
        var position = new SDL3.SDL3_Mixer.Point3D { X = x, Y = y, Z = z };
        if (SDL3.SDL3_Mixer.SetTrack3DPosition(GetTrack(handle), &position) == 0)
        {
            throw Error(nameof(SDL3.SDL3_Mixer.SetTrack3DPosition));
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

    private BusGroup GetOrCreateBusGroup(AudioBus bus)
    {
        if (_busGroups.TryGetValue(bus, out var existing))
        {
            return existing;
        }

        var parentGroup = bus.Parent is null ? null : GetOrCreateBusGroup(bus.Parent);

        var spec = new SDL3.SDL.SDL_AudioSpec();
        if (!SDL3.SDL3_Mixer.GetMixerFormat(GetMixer(), &spec) || spec.channels <= 0 || spec.freq <= 0)
        {
            throw Error(nameof(SDL3.SDL3_Mixer.GetMixerFormat));
        }

        bus.InitializeEffects(spec.freq, spec.channels);
        var nativeGroup = SDL3.SDL3_Mixer.CreateGroup(GetMixer());
        if (nativeGroup == null)
        {
            throw Error(nameof(SDL3.SDL3_Mixer.CreateGroup));
        }

        var group = new BusGroup(bus, nativeGroup);
        if (!SDL3.SDL3_Mixer.SetGroupPostMixCallback(nativeGroup, &GroupPostMix,
                (void*)GCHandle.ToIntPtr(group.Handle)))
        {
            group.Dispose();
            throw Error(nameof(SDL3.SDL3_Mixer.SetGroupPostMixCallback));
        }

        _busGroups.Add(bus, group);
        if (parentGroup is null)
        {
            AddRootBusGroup(group);
        }
        else
        {
            parentGroup.AddChild(group);
        }

        return group;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void GroupPostMix(void* userData, void* group, SDL3.SDL.SDL_AudioSpec* spec, float* pcm, int samples)
    {
        try
        {
            var busGroup = (BusGroup)GCHandle.FromIntPtr((nint)userData).Target!;
            busGroup.Capture(new ReadOnlySpan<float>(pcm, samples));
        }
        catch
        {
            // Exceptions must not cross the native audio callback boundary.
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void PostMix(void* userData, void* mixer, SDL3.SDL.SDL_AudioSpec* spec, float* pcm, int samples)
    {
        try
        {
            var device = (AudioDeviceSDL)GCHandle.FromIntPtr((nint)userData).Target!;
            if (samples > MaximumCallbackSamples)
            {
                return;
            }

            var output = new Span<float>(pcm, samples);
            output.Clear();
            foreach (var rootGroup in Volatile.Read(ref device._rootBusGroups))
            {
                ProcessBus(rootGroup, samples);
                Add(output, rootGroup.Buffer.AsSpan(0, samples));
            }
        }
        catch
        {
            // Exceptions must not cross the native audio callback boundary.
        }
    }

    private static void ProcessBus(BusGroup busGroup, int samples)
    {
        var buffer = busGroup.Buffer.AsSpan(0, samples);
        foreach (var childGroup in busGroup.Children)
        {
            ProcessBus(childGroup, samples);
            Add(buffer, childGroup.Buffer.AsSpan(0, samples));
        }

        foreach (var processor in busGroup.Bus.EffectProcessors)
        {
            processor.Process(buffer);
        }

        var gain = Volatile.Read(ref busGroup.Gain);
        if (Math.Abs(gain - 1f) > float.Epsilon)
        {
            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] *= gain;
            }
        }
    }

    private void AddRootBusGroup(BusGroup group)
    {
        var current = Volatile.Read(ref _rootBusGroups);
        var updated = new BusGroup[current.Length + 1];
        current.CopyTo(updated, 0);
        updated[^1] = group;
        Volatile.Write(ref _rootBusGroups, updated);
    }

    private static void Add(Span<float> destination, ReadOnlySpan<float> source)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] += source[index];
        }
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

    private sealed class BusGroup : IDisposable
    {
        internal BusGroup[] Children => Volatile.Read(ref _children);

        private BusGroup[] _children;

        internal readonly AudioBus Bus;

        internal readonly float[] Buffer = new float[MaximumCallbackSamples];

        internal readonly void* Group;

        internal GCHandle Handle;

        internal float Gain;

        internal BusGroup(AudioBus bus, void* group)
        {
            _children = [];
            Bus = bus;
            Group = group;
            Handle = GCHandle.Alloc(this);
            UpdateGain();
            Bus.Mixer.Changed += UpdateGain;
        }

        internal void AddChild(BusGroup child)
        {
            var current = Volatile.Read(ref _children);
            var updated = new BusGroup[current.Length + 1];
            current.CopyTo(updated, 0);
            updated[^1] = child;
            Volatile.Write(ref _children, updated);
        }

        internal void Capture(ReadOnlySpan<float> pcm)
        {
            if (pcm.Length <= Buffer.Length)
            {
                pcm.CopyTo(Buffer);
            }
        }

        public void Dispose()
        {
            Bus.Mixer.Changed -= UpdateGain;
            SDL3.SDL3_Mixer.SetGroupPostMixCallback(Group, null, null);
            if (Handle.IsAllocated)
            {
                Handle.Free();
            }

            SDL3.SDL3_Mixer.DestroyGroup(Group);
        }

        private void UpdateGain()
        {
            Volatile.Write(ref Gain, Bus.Mixer.GetLocalOutputGain(Bus));
        }
    }
}
