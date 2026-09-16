using System.Buffers.Binary;

// ReSharper disable once CheckNamespace

namespace Fresnel.Audio;

internal static class Qoa
{
    private const int MaxChannels = 8;

    private const int MaxFrameSamples = 5120;

    private static readonly short[] ScaleFactors = new short[]
    {
        1, 7, 21, 45, 84, 138, 211, 304, 421, 562, 731, 928, 1157, 1419, 1715, 2048
    };

    internal static bool IsQoa(ReadOnlySpan<byte> encoded)
    {
        return encoded.Length >= 4 &&
               encoded[0] == (byte)'q' &&
               encoded[1] == (byte)'o' &&
               encoded[2] == (byte)'a' &&
               encoded[3] == (byte)'f';
    }

    internal static DecodedAudio Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length < 16 || !IsQoa(encoded))
        {
            throw new ArgumentException("The data is not a valid QOA file.", (Exception?)null);
        }

        var totalSamples = BinaryPrimitives.ReadInt32BigEndian(encoded[4..]);
        if (totalSamples <= 0)
        {
            throw new ArgumentException("The QOA file has an invalid total sample count.", (Exception?)null);
        }

        var firstFrameHeader = BinaryPrimitives.ReadUInt32BigEndian(encoded[8..]);
        var channels = (int)(firstFrameHeader >> 24);
        var sampleRate = (int)(firstFrameHeader & 0x00ff_ffff);
        if (channels is < 1 or > MaxChannels || sampleRate <= 0)
        {
            throw new ArgumentException("The QOA file has an invalid channel count or sample rate.", (Exception?)null);
        }

        short[] samples;
        try
        {
            samples = new short[checked(totalSamples * channels)];
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException("The QOA file is too large to decode.", exception);
        }

        var frameOffset = 8;
        var outputOffset = 0;
        var decodedSamples = 0;

        while (decodedSamples < totalSamples)
        {
            if (encoded.Length - frameOffset < 8)
            {
                throw new ArgumentException("The QOA file ends before a complete frame header.", (Exception?)null);
            }

            var frameHeader = BinaryPrimitives.ReadUInt32BigEndian(encoded[frameOffset..]);
            var frameSamples = BinaryPrimitives.ReadUInt16BigEndian(encoded[(frameOffset + 4)..]);
            var frameBytes = BinaryPrimitives.ReadUInt16BigEndian(encoded[(frameOffset + 6)..]);
            var slices = (frameSamples + 19) / 20;
            var expectedFrameBytes = 8 + (channels * (16 + (slices * 8)));

            if (frameHeader != firstFrameHeader || frameSamples is 0 or > MaxFrameSamples ||
                frameSamples > totalSamples - decodedSamples || frameBytes != expectedFrameBytes ||
                frameBytes > encoded.Length - frameOffset)
            {
                throw new ArgumentException("The QOA file contains an invalid frame.", (Exception?)null);
            }

            var frame = encoded.Slice(frameOffset, frameBytes);
            var lmses = new Lms[channels];
            var cursor = 8;
            for (var channel = 0; channel < channels; channel++)
            {
                lmses[channel] = new Lms(frame.Slice(cursor, 16));
                cursor += 16;
            }

            for (var sampleIndex = 0; sampleIndex < frameSamples; sampleIndex += 20)
            {
                for (var channel = 0; channel < channels; channel++)
                {
                    var slice = BinaryPrimitives.ReadUInt64BigEndian(frame[cursor..]);
                    cursor += 8;
                    var scaleFactor = ScaleFactors[slice >> 60];

                    for (var sample = 0; sample < 20; sample++)
                    {
                        var quantized = (int)((slice >> (57 - (sample * 3))) & 0b111);
                        if (sampleIndex + sample >= frameSamples)
                        {
                            continue;
                        }

                        var dequantized = Dequantize(quantized, scaleFactor);
                        var reconstructed = Math.Clamp(lmses[channel].Predict() + dequantized, short.MinValue,
                            short.MaxValue);
                        lmses[channel].Update(reconstructed, dequantized);
                        samples[outputOffset + ((sampleIndex + sample) * channels) + channel] = (short)reconstructed;
                    }
                }
            }

            outputOffset += frameSamples * channels;
            decodedSamples += frameSamples;
            frameOffset += frameBytes;
        }

        if (frameOffset != encoded.Length)
        {
            throw new ArgumentException("The QOA file has trailing data.", (Exception?)null);
        }

        return new DecodedAudio(System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples.AsSpan()).ToArray(),
            channels, sampleRate);
    }

    private static int Dequantize(int quantized, int scaleFactor)
    {
        var dequantized = (quantized >> 1) switch
        {
            0 => ((scaleFactor * 3) + 2) >> 2,
            1 => ((scaleFactor * 5) + 1) >> 1,
            2 => ((scaleFactor * 9) + 1) >> 1,
            _ => scaleFactor * 7
        };
        return (quantized & 1) != 0 ? -dequantized : dequantized;
    }

    internal readonly record struct DecodedAudio(byte[] Pcm, int Channels, int SampleRate);

    private sealed class Lms
    {
        private readonly int[] _history = new int[4];

        private readonly int[] _weights = new int[4];

        internal Lms(ReadOnlySpan<byte> data)
        {
            for (var i = 0; i < 4; i++)
            {
                _history[i] = BinaryPrimitives.ReadInt16BigEndian(data[(i * 2)..]);
                _weights[i] = BinaryPrimitives.ReadInt16BigEndian(data[(8 + (i * 2))..]);
            }
        }

        internal int Predict()
        {
            return ((_history[0] * _weights[0]) + (_history[1] * _weights[1]) +
                    (_history[2] * _weights[2]) + (_history[3] * _weights[3])) >> 13;
        }

        internal void Update(int sample, int residual)
        {
            var delta = residual >> 4;
            for (var i = 0; i < 4; i++)
            {
                _weights[i] += _history[i] < 0 ? -delta : delta;
            }

            _history[0] = _history[1];
            _history[1] = _history[2];
            _history[2] = _history[3];
            _history[3] = sample;
        }
    }
}
