using GWBASIC.Core.Audio;
using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;

namespace GWBASIC.ConsoleApp.Drivers;

public class ConsoleAudioDriver : IAudioDriver
{
    public void Beep()
    {
        try
        {
            byte[] wav = SquareWaveSynthesizer.SynthesizeToneWav(800, 200);
            SoundEngine.PlaySync(wav, fallbackFrequencyHz: 800, fallbackDurationMs: 200);
        }
        catch
        {
            try { Console.Beep(800, 200); } catch { }
        }
    }

    public void Sound(int frequencyHz, int durationClockTicks)
    {
        if (durationClockTicks <= 0)
        {
            // GW-BASIC duration 0 immediately turns off any active sound
            Stop();
            return;
        }

        // GW-BASIC duration is in clock ticks (18.2 ticks per second)
        // 1 tick ~= 54.925 ms
        int durationMs = (int)Math.Round(durationClockTicks * 54.925);
        if (durationMs <= 0) durationMs = 1;

        if (frequencyHz is < 37 or > 32767)
        {
            // Frequencies < 37 are treated as silence/rest in GW-BASIC
            Thread.Sleep(durationMs);
            return;
        }

        try
        {
            byte[] wav = SquareWaveSynthesizer.SynthesizeToneWav(frequencyHz, durationMs);
            SoundEngine.PlaySync(wav, fallbackFrequencyHz: frequencyHz, fallbackDurationMs: Math.Min(durationMs, 5000));
        }
        catch
        {
            try { Console.Beep(frequencyHz, Math.Min(durationMs, 5000)); } catch { Thread.Sleep(durationMs); }
        }
    }

    public void Play(string musicCommands)
    {
        if (string.IsNullOrWhiteSpace(musicCommands)) return;

        var notes = MusicPlayer.Parse(musicCommands);
        if (notes.Count == 0) return;

        try
        {
            byte[] wav = SquareWaveSynthesizer.SynthesizeNotesWav(notes);
            if (wav.Length > 0)
            {
                SoundEngine.PlaySync(wav);
                return;
            }
        }
        catch
        {
            // Fall through to note-by-note fallback if synthesis fails
        }

        // Fallback for non-Windows or if WAV failed: note-by-note
        foreach (var note in notes)
        {
            if (note.FrequencyHz >= 37 && note.DurationMs > 0)
            {
                try
                {
                    Console.Beep(Math.Clamp(note.FrequencyHz, 37, 32767), note.DurationMs);
                }
                catch
                {
                    Thread.Sleep(note.DurationMs);
                }
            }
            else if (note.DurationMs > 0)
            {
                Thread.Sleep(note.DurationMs);
            }

            if (note.RestMs > 0)
            {
                Thread.Sleep(note.RestMs);
            }
        }
    }

    public void Stop()
    {
        SoundEngine.Stop();
    }
}
