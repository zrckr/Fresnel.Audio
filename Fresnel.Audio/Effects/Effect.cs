using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Fresnel.Audio;

/// <summary>
/// Defines immutable settings for a supported audio effect.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ChorusEffect), nameof(ChorusEffect))]
[JsonDerivedType(typeof(CompressorEffect), nameof(CompressorEffect))]
[JsonDerivedType(typeof(DistortionEffect), nameof(DistortionEffect))]
[JsonDerivedType(typeof(EchoEffect), nameof(EchoEffect))]
[JsonDerivedType(typeof(EqualizerEffect), nameof(EqualizerEffect))]
[JsonDerivedType(typeof(FlangerEffect), nameof(FlangerEffect))]
[JsonDerivedType(typeof(ReverbEffect), nameof(ReverbEffect))]
[JsonDerivedType(typeof(RingModulatorEffect), nameof(RingModulatorEffect))]
public abstract record AudioEffect
{
    internal AudioEffect() { }

    internal abstract void Validate();

    internal abstract EffectProcessor CreateProcessor(int sampleRate, int channels);
}

internal abstract class EffectProcessor
{
    protected readonly int _channels;

    protected EffectProcessor(int sampleRate, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        _channels = channels;
    }

    public abstract void Process(Span<float> pcm);
}

internal sealed class EffectChain
{
    private readonly EffectProcessor[] _processors;

    internal EffectChain(ImmutableArray<AudioEffect> effects, int sampleRate, int channels)
    {
        _processors = new EffectProcessor[effects.Length];
        for (var index = 0; index < effects.Length; index++)
        {
            var effect = effects[index];
            effect.Validate();
            _processors[index] = effect.CreateProcessor(sampleRate, channels);
        }
    }

    internal void Process(Span<float> pcm)
    {
        foreach (var processor in _processors)
        {
            processor.Process(pcm);
        }
    }
}

internal readonly struct DryWetMix(float mix)
{
    private readonly float _dryGain = MathF.Sqrt(1f - mix);

    private readonly float _wetGain = MathF.Sqrt(mix);

    internal float Blend(float dry, float wet)
    {
        return (dry * _dryGain) + (wet * _wetGain);
    }
}

internal static class EffectValidation
{
    internal static void Waveform(OscillatorWaveform value, string parameterName)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    internal static void EqualizerShape(EqualizerShape value, string parameterName)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    internal static void Range(float value, float minimum, float maximum, string parameterName)
    {
        if (!float.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    internal static void Range(TimeSpan value, TimeSpan minimum, TimeSpan maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

/// <summary>
/// Specifies the periodic waveform used by an oscillator.
/// </summary>
public enum OscillatorWaveform
{
    /// <summary>
    /// A linear ramp from -1 to 1 followed by an immediate reset.
    /// </summary>
    Sawtooth,

    /// <summary>
    /// A smooth sinusoidal wave.
    /// </summary>
    Sine,

    /// <summary>
    /// A wave that alternates directly between 1 and -1.
    /// </summary>
    Square,

    /// <summary>
    /// A linear rise and fall between -1 and 1.
    /// </summary>
    Triangle
}
