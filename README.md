# Fresnel.Audio

[![NuGet](https://img.shields.io/nuget/v/Fresnel.Audio?label=NuGet)](https://www.nuget.org/packages/Fresnel.Audio)
[![Foster Framework](https://img.shields.io/badge/Foster%20Framework-0.4.2-4c8eda)](https://www.nuget.org/packages/FosterFramework/0.4.2)

Fresnel.Audio is a small audio companion for [Foster Framework](https://github.com/FosterFramework/Foster).
It provides a straightforward API for music, sound effects, mixer buses, DSP effects, and spatial audio without
requiring a separate commercial audio engine.

> [!WARNING]
> The core feature set is usable, but the public API may still change between early releases.

## Features

- Decoded or deferred-decoding playback through [SDL3_mixer](https://github.com/libsdl-org/SDL_mixer)
- OGG, WAV, and other formats supported by SDL3_mixer
- Built-in [Quite OK Audio](https://qoaformat.org/) decoding
- Multiple concurrent voices with oldest-voice reuse
- Play, pause, resume, stop, seek, loop, gain, and playback-rate controls
- Hierarchical mixer buses with volume, pan, mute, and solo controls
- Mutable effect collections with live processor replacement
- Chorus, compressor, distortion, echo, equalizer, flanger, reverb, and ring-modulator effects
- 2D and 3D listener-relative spatial audio
- JSON-serializable bus and effect configuration
- Native libraries for Windows x64, Linux x64/arm64, and macOS x64/arm64

## Requirements

- .NET 10
- Foster Framework 0.4.2
- A supported desktop runtime and architecture listed above

## Building

Clone the repository and build the solution:

```console
dotnet restore
dotnet build Fresnel.Audio.slnx
```

Run the interactive sample:

```console
dotnet run --project Fresnel.Sample
```

## Usage

See the [how-to guide](HOWTO.md) for setup, playback, mixer configuration, live updates, effects, and spatial audio.

## Credits

- Playback and format support: [SDL3_mixer](https://github.com/libsdl-org/SDL_mixer)
- Effect implementations adapted from [OpenAL Soft](https://github.com/kcat/openal-soft); see the [third-party notices](Fresnel.Audio/Effects/THIRD-PARTY-NOTICES.md)
- QOA format and reference material: [Quite OK Audio](https://qoaformat.org/)
- Sample asset attribution: [sample asset credits](Fresnel.Sample/Assets/README.md)

Fresnel.Audio is distributed under the [MIT License](LICENSE).
