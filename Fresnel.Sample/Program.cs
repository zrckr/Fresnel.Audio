using System.Numerics;
using Foster.Framework;
using Fresnel.Audio;
using Fresnel.Sample;
using ImGuiNET;

using var app = new PlaybackDemo();
app.Run();

internal sealed class PlaybackDemo : App
{
    private readonly ExampleMixer _mixer;

    private Audio _audio = null!;

    private AudioStream _songStream = null!;

    private AudioPlayer _songPlayer = null!;

    private AudioStream _coinStream = null!;

    private AudioPlayer _coinPlayer = null!;

    private AudioStream _bounceStream = null!;

    private AudioPlayer _bouncePlayer = null!;

    private Renderer _renderer = null!;

    private Db _playerGain;

    private float _scrubPosition;

    private bool _scrubbing;

    private int _spatialMode;

    private readonly AudioSpatial.Spatial2D _spatial2D = new();

    private readonly AudioSpatial.Spatial3D _spatial3D = new();

    private Vector2 _listenerPosition2D;

    private Vector2 _sourcePosition2D = new(4f, 0f);

    private float _listenerRotation2D;

    private bool _spatialAutopilot;

    private float _spatialOrbitAngle;

    private Vector3 _listenerPosition3D;

    private Vector3 _sourcePosition3D = new(4f, 0f, 0f);

    private float _listenerRotation3D;

    public PlaybackDemo() : base(new AppConfig(
        "Fresnel.Sample",
        "Fresnel.Audio playback demo",
        1280,
        720
    ))
    {
        _mixer = new ExampleMixer(new ExampleMixerConfig());
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
        DrawSpatialScene();
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
        ImGui.SetNextWindowSize(new Vector2(426, 620), ImGuiCond.FirstUseEver);
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
                if (ImGui.SliderFloat("Playback rate", ref rate, 0.25f, 4f, "%.2fx"))
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

                DrawSpatialControls(player);

                ImGui.TreePop();
            }

            ImGui.PopID();
            index += 1;
        }
    }

    private void DrawSpatialControls(AudioPlayer player)
    {
        ImGui.Separator();
        ImGui.Text("Positional audio");

        if (ImGui.RadioButton("None", _spatialMode == 0))
        {
            _spatialMode = 0;
            player.Spatial = new AudioSpatial.None();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("2D", _spatialMode == 1))
        {
            _spatialMode = 1;
            player.Spatial = _spatial2D;
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("3D", _spatialMode == 2))
        {
            _spatialMode = 2;
            player.Spatial = _spatial3D;
        }

        var referenceDistance = _audio.Listener.ReferenceDistance;
        if (ImGui.SliderFloat("Reference distance", ref referenceDistance, 0.1f, 10f, "%.2f"))
        {
            _audio.Listener.ReferenceDistance = referenceDistance;
        }

        if (_spatialMode == 1)
        {
            DrawSlider2D("Listener", ref _listenerPosition2D);
            if (ImGui.SliderFloat("Listener rotation", ref _listenerRotation2D, -MathF.PI, MathF.PI, "%.2f rad"))
            {
                _audio.Listener.SetTransform(_listenerPosition2D, _listenerRotation2D);
            }

            DrawSlider2D("Source", ref _sourcePosition2D);
            _audio.Listener.SetTransform(_listenerPosition2D, _listenerRotation2D);
            _spatial2D.Position = _sourcePosition2D;
        }
        else if (_spatialMode == 2)
        {
            DrawSlider3D("Listener", ref _listenerPosition3D);
            if (ImGui.SliderFloat("Listener yaw", ref _listenerRotation3D, -MathF.PI, MathF.PI, "%.2f rad"))
            {
                _audio.Listener.SetTransform(_listenerPosition3D,
                    Quaternion.CreateFromAxisAngle(Vector3.UnitY, _listenerRotation3D));
            }

            DrawSlider3D("Source", ref _sourcePosition3D);
            _audio.Listener.SetTransform(_listenerPosition3D,
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, _listenerRotation3D));
            _spatial3D.Position = _sourcePosition3D;
        }
    }

    private static void DrawSlider2D(string label, ref Vector2 value)
    {
        var x = value.X;
        var y = value.Y;
        if (ImGui.SliderFloat($"{label} X", ref x, -10f, 10f) |
            ImGui.SliderFloat($"{label} Y", ref y, -10f, 10f))
        {
            value = new Vector2(x, y);
        }
    }

    private static void DrawSlider3D(string label, ref Vector3 value)
    {
        var x = value.X;
        var y = value.Y;
        var z = value.Z;
        if (ImGui.SliderFloat($"{label} X", ref x, -10f, 10f) |
            ImGui.SliderFloat($"{label} Y", ref y, -10f, 10f) |
            ImGui.SliderFloat($"{label} Z", ref z, -10f, 10f))
        {
            value = new Vector3(x, y, z);
        }
    }

    private void DrawSpatialScene()
    {
        ImGui.SetNextWindowPos(new Vector2(450, 12), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(818, 696), ImGuiCond.FirstUseEver);
        ImGui.Begin("Spatialization scene");
        var is3D = _spatialMode == 2;

        ImGui.Checkbox("Automatic orbit", ref _spatialAutopilot);
        ImGui.SameLine();
        if (ImGui.Button("Center listener"))
        {
            if (is3D)
            {
                _listenerPosition3D = Vector3.Zero;
                _listenerRotation3D = 0f;
            }
            else
            {
                _listenerPosition2D = Vector2.Zero;
                _listenerRotation2D = 0f;
            }

            _spatialAutopilot = false;
        }

        ImGui.TextDisabled(is3D
            ? "Top-down X/Z view. Marker size represents Y height; drag to move source X/Z."
            : "Left-click or drag to move the source. Zero rotation faces upward (-Y).");

        if (_spatialMode == 0)
        {
            ImGui.TextColored(new Vector4(1f, 0.75f, 0.3f, 1f),
                "Select 2D or 3D positional audio to hear this scene.");
        }

        if (_spatialAutopilot)
        {
            _spatialOrbitAngle = (_spatialOrbitAngle + (Time.Delta * MathF.Tau / 5f)) % MathF.Tau;
            var orbit = new Vector2(MathF.Cos(_spatialOrbitAngle), MathF.Sin(_spatialOrbitAngle)) * 4f;
            if (is3D)
            {
                _sourcePosition3D = new Vector3(
                    _listenerPosition3D.X + orbit.X,
                    _sourcePosition3D.Y,
                    _listenerPosition3D.Z + orbit.Y);
            }
            else
            {
                _sourcePosition2D = _listenerPosition2D + orbit;
            }
        }

        var canvasTopLeft = ImGui.GetCursorScreenPos();
        var canvasSize = ImGui.GetContentRegionAvail();
        canvasSize.X = Math.Max(canvasSize.X, 1f);
        canvasSize.Y = Math.Max(canvasSize.Y, 1f);
        ImGui.InvisibleButton("spatial canvas", canvasSize, ImGuiButtonFlags.MouseButtonLeft);

        const float worldExtent = 10f;
        var canvasCenter = canvasTopLeft + (canvasSize * 0.5f);
        var pixelsPerUnit = Math.Min(canvasSize.X, canvasSize.Y) / (worldExtent * 2f);

        if (ImGui.IsItemHovered() && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var mouse = ImGui.GetMousePos();
            var position = new Vector2(
                (mouse.X - canvasCenter.X) / pixelsPerUnit,
                (mouse.Y - canvasCenter.Y) / pixelsPerUnit);

            position = Vector2.Clamp(position, new Vector2(-worldExtent), new Vector2(worldExtent));
            if (is3D)
            {
                _sourcePosition3D = new Vector3(position.X, _sourcePosition3D.Y, position.Y);
            }
            else
            {
                _sourcePosition2D = position;
            }

            _spatialAutopilot = false;
        }

        if (_spatialMode == 1)
        {
            _audio.Listener.SetTransform(_listenerPosition2D, _listenerRotation2D);
            _spatial2D.Position = _sourcePosition2D;
        }
        else if (_spatialMode == 2)
        {
            _audio.Listener.SetTransform(_listenerPosition3D,
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, _listenerRotation3D));
            _spatial3D.Position = _sourcePosition3D;
        }

        var listenerPosition = is3D
            ? new Vector2(_listenerPosition3D.X, _listenerPosition3D.Z)
            : _listenerPosition2D;
        var sourcePosition = is3D
            ? new Vector2(_sourcePosition3D.X, _sourcePosition3D.Z)
            : _sourcePosition2D;
        var listenerRotation = is3D ? _listenerRotation3D : _listenerRotation2D;
        var listenerRadius = HeightToRadius(is3D ? _listenerPosition3D.Y : 0f);
        var sourceRadius = HeightToRadius(is3D ? _sourcePosition3D.Y : 0f);

        var draw = ImGui.GetWindowDrawList();
        var canvasBottomRight = canvasTopLeft + canvasSize;
        var background = new Color(0x0E131D).ABGR;
        var border = new Color(0x40526B).ABGR;
        var grid = new Color(0x29364A).ABGR;
        var listenerColor = new Color(0x33E666).ABGR;
        var sourceColor = new Color(0x3380FF).ABGR;
        var textColor = Color.White.ABGR;

        draw.AddRectFilled(canvasTopLeft, canvasBottomRight, background);
        draw.AddRect(canvasTopLeft, canvasBottomRight, border);
        for (var coordinate = -worldExtent; coordinate <= worldExtent; coordinate += 1f)
        {
            var vertical = WorldToCanvas(new Vector2(coordinate, 0f)).X;
            var horizontal = WorldToCanvas(new Vector2(0f, coordinate)).Y;
            draw.AddLine(canvasTopLeft with { X = vertical }, canvasBottomRight with { X = vertical }, grid);
            draw.AddLine(canvasTopLeft with { Y = horizontal }, canvasBottomRight with { Y = horizontal }, grid);
        }

        var listener = WorldToCanvas(listenerPosition);
        var source = WorldToCanvas(sourcePosition);
        var referenceRadius = _audio.Listener.ReferenceDistance * pixelsPerUnit;
        draw.AddCircle(listener, referenceRadius, listenerColor, 48, 1f);
        draw.AddLine(listener, source, border, 2f);

        var facing = new Vector2(MathF.Sin(listenerRotation), -MathF.Cos(listenerRotation));
        draw.AddLine(listener, listener + (facing * 32f), listenerColor, 4f);
        draw.AddCircleFilled(listener, listenerRadius, listenerColor);
        draw.AddCircleFilled(source, sourceRadius, sourceColor);
        draw.AddText(listener + new Vector2(listenerRadius + 4f, 8f), textColor,
            is3D ? $"LISTENER  Y={_listenerPosition3D.Y:0.0}" : "LISTENER");
        draw.AddText(source + new Vector2(sourceRadius + 4f, 8f), textColor,
            is3D ? $"SOURCE  Y={_sourcePosition3D.Y:0.0}" : "SOURCE");

        ImGui.End();

        return;

        Vector2 WorldToCanvas(Vector2 position)
        {
            return canvasCenter + (new Vector2(position.X, position.Y) * pixelsPerUnit);
        }

        static float HeightToRadius(float height)
        {
            return 10f + (Math.Clamp(height, -10f, 10f) * 0.5f);
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
                float volume = bus.Volume;
                if (ImGui.SliderFloat("Volume", ref volume, -48f, 12f, "%.0f dB"))
                {
                    bus.Volume = new Db(Math.Clamp(volume, -48f, 12f));
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
                ImGui.Text("Effect chain:");
                if (bus.Effects.Count == 0)
                {
                    ImGui.TextDisabled("  None");
                }
                else
                {
                    for (var i = 0; i < bus.Effects.Count; i++)
                    {
                        var effect = bus.Effects[i];
                        ImGui.TextDisabled($"{i}.");
                        ImGui.SameLine();
                        ImGui.TextWrapped(effect.ToString());
                    }
                }

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
        public readonly AudioBus Music;

        public readonly AudioBus Sfx;

        public ExampleMixer(ExampleMixerConfig config)
            : base(config.Master)
        {
            Music = AddBus(nameof(Music), config.Music);
            Sfx = AddBus(nameof(Sfx), config.Sfx);
        }
    }
}

internal sealed class ExampleMixerConfig
{
    public AudioBus Master { get; init; } = new();

    public AudioBus Music { get; init; } = new()
    {
        Volume = -12f,
        Effects =
        {
            new EqualizerEffect
            {
                Shape = EqualizerShape.HighShelf,
                FrequencyHz = 8_000f,
                Gain = new Db(1.5f),
                Q = 0.707f
            }
        }
    };

    public AudioBus Sfx { get; init; } = new()
    {
        Volume = -6f,
        Effects =
        {
            new ReverbEffect
            {
                Decay = TimeSpan.FromSeconds(1.8),
                RoomSize = 0.85f,
                Damping = 0.35f,
                Mix = 0.3f
            },
            new CompressorEffect()
        }
    };
}
