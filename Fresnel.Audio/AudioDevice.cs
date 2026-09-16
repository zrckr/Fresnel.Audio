namespace Fresnel.Audio;

internal abstract class AudioDevice : IDisposable
{
    public abstract void Dispose();

    internal abstract ResourceHandle StreamCreate(ReadOnlySpan<byte> bytes, AudioLoadMode mode);

    internal abstract ResourceHandle StreamCreateRaw(ReadOnlySpan<byte> bytes, int channels, int sampleRate);

    internal abstract TimeSpan? StreamGetDuration(ResourceHandle streamHandle);

    internal abstract void StreamDestroy(ResourceHandle streamHandle);

    internal abstract ResourceHandle TrackCreate(ResourceHandle streamHandle);

    internal abstract void TrackDestroy(ResourceHandle trackHandle);

    internal abstract bool TrackIsActive(ResourceHandle trackHandle);

    internal abstract bool TrackIsPlaying(ResourceHandle trackHandle);

    internal abstract TimeSpan TrackGetPosition(ResourceHandle trackHandle);

    internal abstract void TrackPlay(ResourceHandle trackHandle, TimeSpan fromPosition, bool looping);

    internal abstract void TrackSeek(ResourceHandle trackHandle, TimeSpan seekPosition);

    internal abstract void TrackStop(ResourceHandle trackHandle);

    internal abstract void TrackSetPlaybackRate(ResourceHandle trackHandle, float playbackRate);

    internal abstract void TrackSetLooping(ResourceHandle trackHandle, bool looping);

    internal abstract void TrackSetGain(ResourceHandle trackHandle, float gain);

    internal abstract void TrackSetStereo(ResourceHandle trackHandle, float left, float right);

    internal abstract void TrackSet3DPosition(ResourceHandle trackHandle, float x, float y, float z);

    internal abstract void TrackSetPaused(ResourceHandle trackHandle, bool paused);

    internal readonly record struct ResourceHandle(nint Id);
}
