using Foster.Framework;
using System.Runtime.InteropServices;

namespace Fresnel.Audio;

/// <summary>
/// Stores audio data that can create one or more independently controlled players.
/// </summary>
public sealed class AudioStream : IDisposable
{
    /// <summary>
    /// The duration of the audio, when it is available from the source format.
    /// </summary>
    public TimeSpan? Duration => _audio.Device.StreamGetDuration(Handle);

    /// <summary>
    /// The players created from this stream that have not been disposed.
    /// </summary>
    public IReadOnlyCollection<AudioPlayer> Players => _players;

    internal AudioDevice.ResourceHandle Handle { get; }

    private readonly Audio _audio;

    private readonly AudioListener _listener;

    private readonly HashSet<AudioPlayer> _players = new();

    private bool _disposed;

    /// <summary>
    /// Loads audio from a path in a Foster storage container.
    /// </summary>
    public AudioStream(Audio audio, StorageContainer storage, string path, AudioLoadMode mode = AudioLoadMode.Decoded)
        : this(audio, ReadStorage(storage, path).Span, mode)
    {
    }

    /// <summary>
    /// Loads audio from a stream.
    /// </summary>
    public AudioStream(Audio audio, Stream source, AudioLoadMode mode = AudioLoadMode.Decoded)
        : this(audio, ReadAllBytes(source).Span, mode)
    {
    }

    /// <summary>
    /// Loads audio from encoded bytes.
    /// </summary>
    public AudioStream(Audio audio, ReadOnlySpan<byte> encodedData, AudioLoadMode mode = AudioLoadMode.Decoded)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ObjectDisposedException.ThrowIf(audio.IsDisposed, audio);
        _audio = audio;
        _listener = audio.Listener;
        Handle = CreateStream(encodedData, mode);
        audio.AddStream(this);
    }

    private AudioDevice.ResourceHandle CreateStream(ReadOnlySpan<byte> encodedData, AudioLoadMode mode)
    {
        if (Qoa.IsQoa(encodedData))
        {
            var decoded = Qoa.Decode(encodedData);
            return _audio.Device.StreamCreateRaw(MemoryMarshal.AsBytes(decoded.Pcm.AsSpan()), decoded.Channels, decoded.SampleRate);
        }

        return _audio.Device.StreamCreate(encodedData, mode);
    }

    /// <summary>
    /// Creates a player routed through the given mixer bus.
    /// </summary>
    public AudioPlayer CreatePlayer(AudioBus bus)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var player = new AudioPlayer(_audio.Device, _listener, this, bus);
        _players.Add(player);
        return player;
    }

    internal void RemovePlayer(AudioPlayer player)
    {
        ObjectDisposedException.ThrowIf(player.IsDisposed, player);
        _players.Remove(player);
    }

    /// <summary>
    /// Disposes this stream and all players created from it.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var player in _players.ToArray())
            {
                player.Dispose();
            }

            _players.Clear();
            _audio.Device.StreamDestroy(Handle);
            _audio.RemoveStream(this);
            _disposed = true;
        }
    }

    private static ReadOnlyMemory<byte> ReadStorage(StorageContainer storage, string path)
    {
        using var source = storage.OpenRead(path);
        return ReadAllBytes(source);
    }

    private static ReadOnlyMemory<byte> ReadAllBytes(Stream source)
    {
        if (source.CanSeek)
        {
            var remaining = Math.Max(0, source.Length - source.Position);
            var bytes = new byte[checked((int)remaining)];
            source.ReadExactly(bytes);
            return bytes;
        }

        using var buffer = new MemoryStream();
        source.CopyTo(buffer);

        // Disposing MemoryStream leaves its managed buffer valid for synchronous loading.
        return buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length));
    }
}
