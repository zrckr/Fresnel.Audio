using Foster.Framework;
using System.Runtime.InteropServices;

namespace Fresnel.Audio;

public sealed class AudioStream : IDisposable
{
    public TimeSpan? Duration => _device.StreamGetDuration(Handle);

    public IReadOnlyCollection<AudioPlayer> Players => _players;

    internal AudioDevice.ResourceHandle Handle { get; }

    private readonly AudioDevice _device;

    private readonly HashSet<AudioPlayer> _players = new();

    private bool _disposed;

    public AudioStream(Audio audio, StorageContainer storage, string path, AudioLoadMode mode = AudioLoadMode.Decoded)
        : this(audio, ReadStorage(storage, path).Span, mode)
    {
    }

    public AudioStream(Audio audio, Stream source, AudioLoadMode mode = AudioLoadMode.Decoded)
        : this(audio, ReadAllBytes(source).Span, mode)
    {
    }

    public AudioStream(Audio audio, ReadOnlySpan<byte> encodedData, AudioLoadMode mode = AudioLoadMode.Decoded)
    {
        ArgumentNullException.ThrowIfNull(audio);
        _device = audio.Device;
        Handle = CreateStream(encodedData, mode);
    }

    private AudioDevice.ResourceHandle CreateStream(ReadOnlySpan<byte> encodedData, AudioLoadMode mode)
    {
        if (Qoa.IsQoa(encodedData))
        {
            var decoded = Qoa.Decode(encodedData);
            return _device.StreamCreateRaw(MemoryMarshal.AsBytes(decoded.Pcm.AsSpan()), decoded.Channels, decoded.SampleRate);
        }

        return _device.StreamCreate(encodedData, mode);
    }

    public AudioPlayer CreatePlayer(AudioBus bus)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var player = new AudioPlayer(_device, this, bus);
        _players.Add(player);
        return player;
    }

    internal void RemovePlayer(AudioPlayer player)
    {
        ObjectDisposedException.ThrowIf(player.IsDisposed, player);
        _players.Remove(player);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var player in _players.ToArray())
            {
                player.Dispose();
            }

            _players.Clear();
            _device.StreamDestroy(Handle);
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
