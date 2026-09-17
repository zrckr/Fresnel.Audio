namespace Fresnel.Audio;

/// <summary>
/// Defines settings that can create a stateful audio effect processor.
/// </summary>
public interface IEffect
{
    /// <summary>
    /// Verifies that all settings are supported by the effect.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// One or more settings are outside their supported ranges.
    /// </exception>
    void Validate();
}

internal abstract class EffectProcessor
{
    public abstract void Process(Span<float> pcm);
}

internal static class EffectValidation
{
    internal static void Waveform(EffectWaveform value, string parameterName)
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
/// Specifies the periodic waveform used by a modulated effect.
/// </summary>
public enum EffectWaveform
{
    /// <summary>
    /// A linear ramp from -1 to 1 followed by an immediate reset to -1.
    /// </summary>
    Sawtooth,

    /// <summary>
    /// A smooth sinusoidal wave ranging from -1 to 1.
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

internal static class EffectExtensions
{
    public static float WrapPhase(float phase)
    {
        phase %= MathF.Tau;
        return phase < 0f ? phase + MathF.Tau : phase;
    }

    extension(EffectWaveform waveform)
    {
        public float Phased(float phase)
        {
            phase = WrapPhase(phase);
            return waveform switch
            {
                EffectWaveform.Sawtooth => (phase / MathF.PI) - 1f,
                EffectWaveform.Sine => MathF.Sin(phase),
                EffectWaveform.Square => phase < MathF.PI ? 1f : -1f,
                EffectWaveform.Triangle => 1f - (2f * MathF.Abs((phase / MathF.PI) - 1f)),
                _ => throw new ArgumentOutOfRangeException(nameof(waveform))
            };
        }
    }
}
