using System.Numerics;
using Foster.Framework;
using Fresnel.Audio;
using Fresnel.Sample;
using ImGuiNET;

using var app = new PlaybackDemo();
app.Run();

internal sealed class PlaybackDemo : App
{
    private readonly ExampleMixer _mixer = new();

    private Audio _audio = null!;

    private AudioStream _songStream = null!;

    private AudioPlayer _songPlayer = null!;

    private AudioStream _coinStream = null!;

    private AudioPlayer _coinPlayer = null!;

    private AudioStream _bounceStream = null!;

    private AudioPlayer _bouncePlayer = null!;

    private Renderer _renderer = null!;

    private Db _masterGain = -20f;

    private Db _playerGain;

    private Db _musicGain = -20f;

    private float _scrubPosition;

    private bool _scrubbing;

    public PlaybackDemo()
        : base(new AppConfig("Fresnel.Sample", "Fresnel.Audio playback demo", 1280, 720))
    {
    }

    protected override void Startup()
    {
        _audio = new Audio(this);
        _songStream = LoadStream("shortcuts.ogg");
        _songPlayer = _songStream.CreatePlayer(_mixer.Music);

        _coinStream = LoadStream("action_drop_coin_01.wav");
        _coinPlayer = _coinStream.CreatePlayer(_mixer.Sfx);
        _coinPlayer.MaxVoices = 4;

        _bounceStream = LoadStream("bounce_cartoony_03.qoa");
        _bouncePlayer = _bounceStream.CreatePlayer(_mixer.Sfx);
        _bouncePlayer.MaxVoices = 4;

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
        _bouncePlayer.Dispose();
        _bounceStream.Dispose();
        _coinPlayer.Dispose();
        _coinStream.Dispose();
        _songPlayer.Dispose();
        _songStream.Dispose();
        _audio.Dispose();
    }

    private void DrawAudioControls()
    {
        ImGui.SetNextWindowPos(new Vector2(12, 12), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(426, 430), ImGuiCond.FirstUseEver);
        ImGui.Begin("Fresnel.Audio playback demo");

        ImGui.Text("Shortcuts - Zane Little Music");
        ImGui.Text($"Duration: {_songStream.Duration?.ToString(@"m\:ss\.ff") ?? "unknown"}");
        ImGui.Separator();

        DrawPlayerControls();
        DrawOneShotControls();
        DrawBusControls();
        ImGui.End();
    }

    private void DrawOneShotControls()
    {
        ImGui.Separator();
        ImGui.Text("One-shot effects");

        if (ImGui.Button("Play WAV: Coin drop"))
        {
            _coinPlayer.Play();
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"Voices: {_coinPlayer.ActiveVoices.ToString()}");

        if (ImGui.Button("Play QOA: Cartoony bounce"))
        {
            _bouncePlayer.Play();
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"Voices: {_bouncePlayer.ActiveVoices.ToString()}");
    }

    private void DrawPlayerControls()
    {
        var index = 0;
        foreach (var player in _songStream.Players)
        {
            ImGui.PushID(index);
            if (ImGui.TreeNode($"Player {index + 1}: Shortcuts"))
            {
                if (ImGui.Button(player.State == AudioPlayer.PlaybackState.Playing ? "Pause" : "Play"))
                {
                    if (_songPlayer.State == AudioPlayer.PlaybackState.Stopped)
                    {
                        _songPlayer.Play();
                    }
                    else if (_songPlayer.State == AudioPlayer.PlaybackState.Paused)
                    {
                        _songPlayer.Resume();
                    }
                    else
                    {
                        _songPlayer.Pause();
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button("Stop"))
                {
                    player.Stop();
                }

                ImGui.SameLine();
                ImGui.TextDisabled($"Voices: {player.ActiveVoices.ToString()}");

                var looping = player.Looping;
                if (ImGui.Checkbox("Loop", ref looping))
                {
                    player.Looping = looping;
                }

                ImGui.SameLine();
                var paused = player.State == AudioPlayer.PlaybackState.Paused;
                if (ImGui.Checkbox("Paused", ref paused))
                {
                    if (paused)
                    {
                        player.Pause();
                    }
                    else
                    {
                        player.Resume();
                    }
                }

                var rate = player.PlaybackRate;
                if (ImGui.SliderFloat("Playback rate", ref rate, .25f, 4f, "%.2fx"))
                {
                    player.PlaybackRate = rate;
                }

                if (_songStream.Duration is { } duration)
                {
                    if (!_scrubbing)
                    {
                        _scrubPosition = (float)Math.Clamp(player.Position.TotalSeconds, 0, duration.TotalSeconds);
                    }

                    ImGui.SliderFloat("Position", ref _scrubPosition, 0, (float)duration.TotalSeconds, "%.2fs");
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        player.Position = TimeSpan.FromSeconds(_scrubPosition);
                        _scrubbing = false;
                    }
                    else if (ImGui.IsItemDeactivated())
                    {
                        _scrubbing = false;
                    }
                    else if (ImGui.IsItemActive())
                    {
                        _scrubbing = true;
                    }
                }

                float volume = _playerGain;
                if (ImGui.SliderFloat("Volume", ref volume, -48f, 12f, "%.0f dB"))
                {
                    _playerGain = Math.Clamp(volume, -48f, 12f);
                    _songPlayer.Volume = new Db(_playerGain);
                }

                ImGui.TreePop();
            }

            ImGui.PopID();
            index += 1;
        }
    }

    private void DrawBusControls()
    {
        var index = 0;
        foreach (var bus in _mixer.Buses.Values)
        {
            var route = bus.Parent?.Name ?? "Speakers";
            ImGui.PushID(index);
            if (ImGui.TreeNode($"Bus: {bus.Name}"))
            {
                float volume = ReferenceEquals(bus, _mixer.Master) ? _masterGain : _musicGain;
                if (ImGui.SliderFloat("Volume", ref volume, -48f, 12f, "%.0f dB"))
                {
                    if (ReferenceEquals(bus, _mixer.Master))
                    {
                        _masterGain = Math.Clamp(volume, -48f, 12f);
                        _mixer.Master.Volume = new Db(_masterGain);
                    }
                    else
                    {
                        _musicGain = Math.Clamp(volume, -48f, 12f);
                        _mixer.Music.Volume = new Db(_musicGain);
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
            index += 1;
        }
    }

    private AudioStream LoadStream(string filename)
    {
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", filename));
        return new AudioStream(_audio, source);
    }

    private sealed class ExampleMixer : AudioMixer
    {
        public AudioBus Music { get; }

        public AudioBus Sfx { get; }

        public ExampleMixer()
        {
            Music = AddBus(nameof(Music), new AudioBusConfig
            {
                Volume = -12f
            });

            Sfx = AddBus(nameof(Sfx), new AudioBusConfig
            {
                Volume = -6f
            });
        }
    }
}
