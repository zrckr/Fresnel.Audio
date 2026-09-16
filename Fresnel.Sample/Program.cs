using System.Numerics;
using Foster.Framework;
using Fresnel.Audio;
using Fresnel.Sample;
using ImGuiNET;

using var app = new PlaybackDemo();
app.Run();

internal sealed class PlaybackDemo : App
{
    private readonly PlaybackLayout _layout = new();

    private AudioDevice _audio = null!;

    private AudioStream _stream = null!;

    private AudioPlayer _player = null!;

    private Renderer _renderer = null!;

    private float _masterGain = -20f;

    private float _playerGain;

    private float _musicGain = -20f;

    public PlaybackDemo()
        : base(new AppConfig("Fresnel.Sample", "Fresnel.Audio playback demo", 1280, 720))
    {
    }

    protected override void Startup()
    {
        _audio = new AudioDevice(this);
        _audio.LoadLayout(_layout);

        using (var track = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "shortcuts.ogg")))
        {
            _stream = new AudioStream(_audio, track, AudioLoadMode.Decoded);
        }

        _player = new AudioPlayer(_audio, _stream, _layout.Music);
        _player.Play();
        _renderer = new Renderer(this, Path.Combine(AppContext.BaseDirectory, "Assets", "monogram.ttf"));
    }

    protected override void Update()
    {
        _renderer.BeginLayout();
        DrawAudioControls();
        _renderer.EndLayout();
    }

    protected override void Render()
    {
        Window.Clear(new Color(0x101622));
        _renderer.Render();
    }

    protected override void Shutdown()
    {
        _renderer.Dispose();
        _player.Dispose();
        _stream.Dispose();
        _audio.Dispose();
    }

    private void DrawAudioControls()
    {
        ImGui.SetNextWindowPos(new Vector2(12, 12), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(426, 286), ImGuiCond.FirstUseEver);
        ImGui.Begin("Fresnel.Audio playback demo");

        ImGui.Text("Shortcuts - Zane Little Music");
        ImGui.Text($"Duration: {_stream.Duration?.ToString(@"m\:ss\.ff") ?? "unknown"}");
        ImGui.Separator();

        DrawPlayerControls();
        DrawBusControls();
        ImGui.End();
    }

    private void DrawPlayerControls()
    {
        var players = new[] { _player };
        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            var state = !player.Playing ? "stopped" : player.StreamPaused ? "paused" : "playing";
            ImGui.PushID(index);
            if (ImGui.TreeNode($"Player {index + 1}: Shortcuts"))
            {
                if (ImGui.Button(player.Playing ? "Play / Pause" : "Play"))
                {
                    TogglePlayback();
                }

                ImGui.SameLine();
                if (ImGui.Button("Stop"))
                {
                    player.Stop();
                }

                ImGui.SameLine();
                if (ImGui.Button("Restart"))
                {
                    Restart();
                }

                ImGui.SameLine();
                if (ImGui.Button("Midpoint"))
                {
                    PlayFromMidpoint();
                }

                var looping = _stream.Looping;
                if (ImGui.Checkbox("Loop", ref looping))
                {
                    _stream.Looping = looping;
                }

                ImGui.SameLine();
                var paused = player.StreamPaused;
                if (ImGui.Checkbox("Paused", ref paused))
                {
                    player.StreamPaused = paused;
                }

                var rate = _stream.PlaybackRate;
                if (ImGui.SliderFloat("Playback rate", ref rate, .25f, 4f, "%.2fx"))
                {
                    _stream.PlaybackRate = rate;
                }

                var gain = _playerGain;
                if (ImGui.SliderFloat("Gain", ref gain, -48f, 12f, "%.0f dB"))
                {
                    SetPlayerGain(gain);
                }

                ImGui.Text($"Route: {_layout.Music.Name}");
                ImGui.Text($"State: {state}");
                ImGui.TreePop();
            }

            ImGui.PopID();
        }
    }

    private void DrawBusControls()
    {
        var buses = new[] { _layout.Master, _layout.Music };
        for (var index = 0; index < buses.Length; index++)
        {
            var bus = buses[index];
            var route = string.IsNullOrEmpty(bus.RouteTo) ? "output" : bus.RouteTo;
            ImGui.PushID(index);
            if (ImGui.TreeNode($"Bus: {bus.Name}"))
            {
                var gain = ReferenceEquals(bus, _layout.Master) ? _masterGain : _musicGain;
                if (ImGui.SliderFloat("Gain", ref gain, -48f, 12f, "%.0f dB"))
                {
                    if (ReferenceEquals(bus, _layout.Master))
                    {
                        SetMasterGain(gain);
                    }
                    else
                    {
                        SetMusicGain(gain);
                    }
                }

                var pan = bus.Pan;
                if (ImGui.SliderFloat("Pan", ref pan, -1f, 1f, "%.2f"))
                {
                    bus.Pan = pan;
                }

                var muted = bus.Muted;
                if (ImGui.Checkbox("Muted", ref muted))
                {
                    bus.Muted = muted;
                }

                ImGui.SameLine();
                var solo = bus.Solo;
                if (ImGui.Checkbox("Solo", ref solo))
                {
                    bus.Solo = solo;
                }

                ImGui.Text($"Route: {route}");
                ImGui.TreePop();
            }

            ImGui.PopID();
        }
    }

    private void TogglePlayback()
    {
        if (!_player.Playing)
        {
            _player.Play();
        }
        else
        {
            _player.StreamPaused = !_player.StreamPaused;
        }
    }

    private void Restart()
    {
        _player.Stop();
        _player.StreamPaused = false;
        _player.Play();
    }

    private void PlayFromMidpoint()
    {
        _player.Play((_stream.Duration ?? TimeSpan.Zero) / 2);
    }

    private void SetPlayerGain(float gain)
    {
        _playerGain = Math.Clamp(gain, -48f, 12f);
        _player.Volume = new Db(_playerGain);
    }

    private void SetMasterGain(float gain)
    {
        _masterGain = Math.Clamp(gain, -48f, 12f);
        _layout.Master.Volume = new Db(_masterGain);
    }

    private void SetMusicGain(float gain)
    {
        _musicGain = Math.Clamp(gain, -48f, 12f);
        _layout.Music.Volume = new Db(_musicGain);
    }

    private sealed class PlaybackLayout : AudioBusLayout
    {
        public AudioBus Music { get; }

        public PlaybackLayout()
        {
            Master.Volume = Db.FromLinear(0.1f);
            Music = new AudioBus("Music", () => Master)
            {
                Volume = Db.FromLinear(0.1f)
            };
        }
    }
}
