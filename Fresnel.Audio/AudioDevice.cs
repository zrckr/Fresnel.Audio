using Foster.Framework;

namespace Fresnel.Audio;

public sealed class AudioDevice : IDisposable
{
    private readonly AudioMixer _mixer;

    private readonly AudioDeviceSDL _device;

    private readonly Dictionary<AudioStream, StreamState> _streamStates = new();

    private readonly Dictionary<AudioPlayer, PlayerState> _playerStates = new();

    private bool _disposed;

    public AudioDevice(App app, AudioMixer mixer)
    {
        _device = new AudioDeviceSDL(app);
        _mixer = mixer;
        _mixer.Changed += MixerChanged;
    }

    internal void CreateStream(AudioStream stream, ReadOnlySpan<byte> encodedData, AudioLoadMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _streamStates.Add(stream, new StreamState { Resource = _device.LoadAudio(encodedData, mode) });
    }

    internal TimeSpan? GetDuration(AudioStream stream)
    {
        return GetStream(stream).Resource.Duration;
    }

    internal void DisposeStream(AudioStream stream)
    {
        var node = GetStream(stream);
        if (node.Players.Count != 0)
        {
            throw new InvalidOperationException("The audio stream still has undisposed players.");
        }

        node.Resource.Dispose();
        _streamStates.Remove(stream);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var player in _playerStates.Keys.ToArray())
        {
            player.DisposeFromDevice();
        }

        foreach (var stream in _streamStates.Keys.ToArray())
        {
            stream.DisposeFromDevice();
        }

        _mixer.Changed -= MixerChanged;
        _device.Dispose();
        _disposed = true;
    }

    internal void CreatePlayer(AudioPlayer player, AudioBus bus)
    {
        if (!ReferenceEquals(bus.Mixer, _mixer))
        {
            throw new ArgumentException("The bus does not belong to this audio device.", nameof(bus));
        }

        var changedHandler = () => PlayerChanged(player);
        player.Changed += changedHandler;
        GetStream(player.AudioStream).Players.Add(player);
        _playerStates.Add(player, new PlayerState { Owner = player, Bus = bus, ChangedHandler = changedHandler });
    }

    internal bool IsPlayerPlaying(AudioPlayer player)
    {
        return GetPlayer(player).Playbacks.Any(it => it.Track.Playing);
    }

    internal void Play(AudioPlayer player, TimeSpan fromPosition)
    {
        var node = GetPlayer(player);
        if (player.AudioStream.Duration == fromPosition)
        {
            return;
        }

        var playback = GetPlayback(player, node);
        ApplyStreamOptions(player, playback);
        ApplyRouting(player, playback);
        playback.Track.Play(fromPosition, player.AudioStream.Looping);
        playback.Track.SetPaused(player.StreamPaused);
        playback.Sequence = ++node.NextSequence;
    }

    internal void Seek(AudioPlayer player, TimeSpan toPosition)
    {
        foreach (var playback in GetPlayer(player).Playbacks)
        {
            if (!playback.Track.Playing)
            {
                continue;
            }

            if (player.AudioStream.Duration == toPosition)
            {
                playback.Track.Stop();
            }
            else
            {
                playback.Track.Seek(toPosition);
            }
        }
    }

    internal void Stop(AudioPlayer player)
    {
        foreach (var playback in GetPlayer(player).Playbacks)
        {
            playback.Track.Stop();
        }
    }

    internal void DisposePlayer(AudioPlayer player)
    {
        var node = GetPlayer(player);
        foreach (var playback in node.Playbacks)
        {
            playback.Track.Dispose();
        }

        player.Changed -= node.ChangedHandler;
        GetStream(player.AudioStream).Players.Remove(player);
        _playerStates.Remove(player);
    }

    private void PlayerChanged(AudioPlayer player)
    {
        var node = GetPlayer(player);
        TrimPolyphony(player, node);
        ApplyStreamOptions(node);
        ApplyRouting(node);

        foreach (var playback in node.Playbacks)
        {
            playback.Track.SetPaused(player.StreamPaused);
        }
    }

    private void MixerChanged()
    {
        foreach (var player in _playerStates.Values)
        {
            ApplyRouting(player);
        }
    }

    private StreamState GetStream(AudioStream stream)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(stream.IsDisposed, stream);
        return _streamStates.TryGetValue(stream, out var node)
            ? node
            : throw new ArgumentException("The stream does not belong to this audio device.", nameof(stream));
    }

    private PlayerState GetPlayer(AudioPlayer player)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(player.IsDisposed, player);
        return _playerStates.TryGetValue(player, out var node)
            ? node
            : throw new ArgumentException("The player does not belong to this audio device.", nameof(player));
    }

    private Playback GetPlayback(AudioPlayer player, PlayerState state)
    {
        var playback = state.Playbacks.FirstOrDefault(it => !it.Track.Playing);
        if (playback != null)
        {
            return playback;
        }

        if (state.Playbacks.Count < player.MaxPolyphony)
        {
            playback = new Playback(_device.CreateTrack(GetStream(player.AudioStream).Resource));
            state.Playbacks.Add(playback);
            return playback;
        }

        playback = state.Playbacks.MinBy(it => it.Sequence)!;
        playback.Track.Stop();
        return playback;
    }

    private static void TrimPolyphony(AudioPlayer player, PlayerState state)
    {
        while (state.Playbacks.Count > player.MaxPolyphony)
        {
            var playback = state.Playbacks.MinBy(it => it.Sequence)!;
            playback.Track.Stop();
            playback.Track.Dispose();
            state.Playbacks.Remove(playback);
        }
    }

    private void ApplyRouting(PlayerState player)
    {
        foreach (var playback in player.Playbacks)
        {
            ApplyRouting(player.Owner, playback);
        }
    }

    private void ApplyRouting(AudioPlayer player, Playback playback)
    {
        var output = _mixer.GetOutput(GetPlayer(player).Bus);
        playback.Track.SetOutput(player.Volume.ToLinear() * output.Volume, output.Left, output.Right);
    }

    private static void ApplyStreamOptions(PlayerState player)
    {
        foreach (var playback in player.Playbacks)
        {
            ApplyStreamOptions(player.Owner, playback);
        }
    }

    private static void ApplyStreamOptions(AudioPlayer player, Playback playback)
    {
        playback.Track.SetPlaybackRate(player.AudioStream.PlaybackRate);
        playback.Track.SetLooping(player.AudioStream.Looping);
    }

    private sealed class StreamState
    {
        public required AudioDeviceSDL.AudioResource Resource;
        public readonly HashSet<AudioPlayer> Players = new();
    }

    private sealed class PlayerState
    {
        public required AudioPlayer Owner;
        public required AudioBus Bus;
        public required Action ChangedHandler;
        public readonly List<Playback> Playbacks = new();
        public long NextSequence;
    }

    private sealed class Playback(AudioDeviceSDL.TrackResource track)
    {
        public readonly AudioDeviceSDL.TrackResource Track = track;
        public long Sequence;
    }
}
