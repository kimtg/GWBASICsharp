using System.Text;
using GWBASIC.Core.Audio;
using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class SoundSynthesisTests
{
    [Fact]
    public void TestWavHeaderFormat()
    {
        int sampleRate = 22050;
        int durationMs = 100;
        int expectedPcmSamples = sampleRate * durationMs / 1000;

        byte[] wav = SquareWaveSynthesizer.SynthesizeToneWav(440, durationMs, sampleRate);

        Assert.Equal(44 + expectedPcmSamples, wav.Length);

        // RIFF chunk
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        int chunkSize = BitConverter.ToInt32(wav, 4);
        Assert.Equal(36 + expectedPcmSamples, chunkSize);
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));

        // fmt chunk
        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
        int subchunk1Size = BitConverter.ToInt32(wav, 16);
        Assert.Equal(16, subchunk1Size);
        short audioFormat = BitConverter.ToInt16(wav, 20);
        Assert.Equal(1, audioFormat); // PCM
        short channels = BitConverter.ToInt16(wav, 22);
        Assert.Equal(1, channels); // Mono
        int sr = BitConverter.ToInt32(wav, 24);
        Assert.Equal(sampleRate, sr);
        int byteRate = BitConverter.ToInt32(wav, 28);
        Assert.Equal(sampleRate, byteRate);
        short blockAlign = BitConverter.ToInt16(wav, 32);
        Assert.Equal(1, blockAlign);
        short bitsPerSample = BitConverter.ToInt16(wav, 34);
        Assert.Equal(8, bitsPerSample);

        // data chunk
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
        int dataSize = BitConverter.ToInt32(wav, 40);
        Assert.Equal(expectedPcmSamples, dataSize);
    }

    [Fact]
    public void TestSquareWaveValuesAndRamp()
    {
        byte[] wav = SquareWaveSynthesizer.SynthesizeToneWav(440, 100);
        Assert.True(wav.Length > 44);

        ReadOnlySpan<byte> pcm = wav.AsSpan(44);

        // First sample starts near 128 due to ramp-in
        Assert.True(Math.Abs(pcm[0] - 128) <= 2);

        // Samples fluctuate between 92 and 164 (128 +/- 36)
        bool hasHigh = false;
        bool hasLow = false;
        for (int i = 0; i < pcm.Length; i++)
        {
            Assert.True(pcm[i] >= 92 && pcm[i] <= 164, $"Sample {i} was {pcm[i]}, expected in [92, 164]");
            if (pcm[i] == 164) hasHigh = true;
            if (pcm[i] == 92) hasLow = true;
        }

        Assert.True(hasHigh, "Expected at least one peak high sample (164)");
        Assert.True(hasLow, "Expected at least one peak low sample (92)");
    }

    [Fact]
    public void TestSilenceForFrequenciesBelow37()
    {
        byte[] wav = SquareWaveSynthesizer.SynthesizeToneWav(20, 50);
        Assert.True(wav.Length > 44);

        ReadOnlySpan<byte> pcm = wav.AsSpan(44);
        for (int i = 0; i < pcm.Length; i++)
        {
            Assert.Equal(128, pcm[i]);
        }
    }

    [Fact]
    public void TestNotesSequenceSynthesis()
    {
        var notes = MusicPlayer.Parse("T120 L4 C D P4 E");
        Assert.True(notes.Count >= 3);

        byte[] wav = SquareWaveSynthesizer.SynthesizeNotesWav(notes);
        Assert.True(wav.Length > 44);

        // Expected total ms: each quarter note at T120 is 500ms
        // Total duration for 4 beats = ~2000 ms -> ~44100 PCM samples
        int pcmLength = wav.Length - 44;
        Assert.True(pcmLength >= 40000 && pcmLength <= 50000);
    }

    [Fact]
    public void TestStopLifecycleInVirtualDriver()
    {
        var screen = new VirtualScreenDriver();
        var audio = new VirtualAudioDriver();
        var input = new VirtualInputDriver();
        var fs = new VirtualFileSystemDriver();
        var env = new BasicEnvironment(screen, audio, input, fs);

        Assert.Equal(0, audio.StopCount);
        audio.Stop();
        Assert.Equal(1, audio.StopCount);

        env.NewProgram();
        Assert.Equal(2, audio.StopCount);
    }
}
