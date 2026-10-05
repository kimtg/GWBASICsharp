namespace GWBASIC.Core.Audio;

using System.IO;
using System.Text;
using GWBASIC.Core.Runtime;

/// <summary>
/// Authentic PC Speaker sound synthesizer for GW-BASIC.
/// Generates 8-bit mono square waves matching vintage IBM PC PIT 8253 audio
/// and wraps them in standard 44-byte RIFF WAVE containers.
/// </summary>
public static class SquareWaveSynthesizer
{
    public const int DefaultSampleRate = 22050;
    private const double Amplitude = 36.0; // +/- 36 around baseline 128
    private const byte Baseline = 128; // DC neutral 8-bit PCM center

    /// <summary>
    /// Synthesizes a single frequency tone into an 8-bit mono square wave WAV buffer.
    /// </summary>
    public static byte[] SynthesizeToneWav(int frequencyHz, int durationMs, int sampleRate = DefaultSampleRate)
    {
        if (durationMs <= 0) return Array.Empty<byte>();

        int totalSamples = (int)(sampleRate * (durationMs / 1000.0));
        if (totalSamples <= 0) return Array.Empty<byte>();

        byte[] pcm = new byte[totalSamples];

        if (frequencyHz >= 37 && frequencyHz <= 32767)
        {
            double period = sampleRate / (double)frequencyHz;
            int rampLength = Math.Min(30, totalSamples / 4);

            for (int i = 0; i < totalSamples; i++)
            {
                double square = (Math.Sin(2.0 * Math.PI * i / period) >= 0) ? 1.0 : -1.0;
                double amp = Amplitude;
                if (i < rampLength)
                    amp *= (double)i / rampLength;
                else if (i > totalSamples - rampLength)
                    amp *= (double)(totalSamples - i) / rampLength;

                pcm[i] = (byte)(Baseline + (int)(square * amp));
            }
        }
        else
        {
            // Silence / Rest
            Array.Fill(pcm, Baseline);
        }

        return BuildWav(pcm, sampleRate);
    }

    /// <summary>
    /// Synthesizes an ordered sequence of notes/rests into a continuous 8-bit mono square wave WAV buffer.
    /// </summary>
    public static byte[] SynthesizeNotesWav(IEnumerable<MusicNote> notes, int sampleRate = DefaultSampleRate)
    {
        using var pcmStream = new MemoryStream();

        foreach (var note in notes)
        {
            if (note.DurationMs <= 0 && note.RestMs <= 0) continue;

            int toneSamples = note.DurationMs > 0 ? (int)(sampleRate * (note.DurationMs / 1000.0)) : 0;
            int restSamples = note.RestMs > 0 ? (int)(sampleRate * (note.RestMs / 1000.0)) : 0;

            if (toneSamples > 0 && note.FrequencyHz >= 37 && note.FrequencyHz <= 32767)
            {
                double period = sampleRate / (double)note.FrequencyHz;
                int rampLength = Math.Min(30, toneSamples / 4);

                for (int i = 0; i < toneSamples; i++)
                {
                    double square = (Math.Sin(2.0 * Math.PI * i / period) >= 0) ? 1.0 : -1.0;
                    double amp = Amplitude;
                    if (i < rampLength)
                        amp *= (double)i / rampLength;
                    else if (i > toneSamples - rampLength)
                        amp *= (double)(toneSamples - i) / rampLength;

                    pcmStream.WriteByte((byte)(Baseline + (int)(square * amp)));
                }
            }
            else
            {
                for (int i = 0; i < toneSamples; i++)
                    pcmStream.WriteByte(Baseline);
            }

            for (int i = 0; i < restSamples; i++)
                pcmStream.WriteByte(Baseline);
        }

        byte[] pcmData = pcmStream.ToArray();
        if (pcmData.Length == 0) return Array.Empty<byte>();

        return BuildWav(pcmData, sampleRate);
    }

    /// <summary>
    /// Builds a 44-byte standard RIFF WAVE container for 8-bit mono PCM audio data.
    /// </summary>
    public static byte[] BuildWav(byte[] pcmData, int sampleRate = DefaultSampleRate)
    {
        if (pcmData == null || pcmData.Length == 0) return Array.Empty<byte>();

        using var wavStream = new MemoryStream(44 + pcmData.Length);
        using var bw = new BinaryWriter(wavStream);

        // RIFF Header
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + pcmData.Length);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        // Format Chunk
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);                 // Subchunk1Size (16 for PCM)
        bw.Write((short)1);           // AudioFormat (1 = PCM)
        bw.Write((short)1);           // NumChannels (1 = Mono)
        bw.Write(sampleRate);         // SampleRate
        bw.Write(sampleRate);         // ByteRate (SampleRate * NumChannels * BitsPerSample / 8)
        bw.Write((short)1);           // BlockAlign (NumChannels * BitsPerSample / 8)
        bw.Write((short)8);           // BitsPerSample (8 bit)

        // Data Chunk
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(pcmData.Length);
        bw.Write(pcmData);

        return wavStream.ToArray();
    }
}
