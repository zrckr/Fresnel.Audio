using Foster.Framework;

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
        : this(audio, ReadStorage(storage, path), mode)
    {
    }

    public AudioStream(Audio audio, Stream source, AudioLoadMode mode = AudioLoadMode.Decoded)
        : this(audio, ReadAllBytes(source), mode)
    {
    }

    public AudioStream(Audio audio, ReadOnlySpan<byte> encodedData, AudioLoadMode mode = AudioLoadMode.Decoded)
        : this(audio, encodedData.ToArray(), mode)
    {
    }

    private AudioStream(Audio audio, byte[] encodedData, AudioLoadMode mode)
    {
        ArgumentNullException.ThrowIfNull(audio);
        _device = audio.Device;
        Handle = _device.StreamCreate(encodedData, mode);
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

    private static byte[] ReadStorage(StorageContainer storage, string path)
    {
        using var source = storage.OpenRead(path);
        return ReadAllBytes(source);
    }

    private static byte[] ReadAllBytes(Stream source)
    {
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return buffer.ToArray();
    }
}
