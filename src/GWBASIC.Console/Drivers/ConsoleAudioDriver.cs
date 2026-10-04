using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;

namespace GWBASIC.ConsoleApp.Drivers;

public class ConsoleAudioDriver : IAudioDriver
{
    public void Beep()
    {
        try
        {
            Console.Beep(800, 200);
        }
        catch { }
    }

    public void Sound(int frequencyHz, int durationClockTicks)
    {
        // GW-BASIC duration is in clock ticks (18.2 ticks per second)
        // 1 tick ~= 54.9 ms
        int durationMs = (int)Math.Round(durationClockTicks * 54.925);
        if (durationMs <= 0) durationMs = 1;

        if (frequencyHz is < 37 or > 32767)
        {
            Thread.Sleep(durationMs);
            return;
        }

        try
        {
            Console.Beep(frequencyHz, Math.Min(durationMs, 5000));
        }
        catch
        {
            Thread.Sleep(durationMs);
        }
    }

    public void Play(string musicCommands)
    {
        var notes = MusicPlayer.Parse(musicCommands);
        foreach (var note in notes)
        {
            if (note.FrequencyHz > 37 && note.DurationMs > 0)
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
}
