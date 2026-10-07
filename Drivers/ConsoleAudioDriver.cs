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

    private readonly object _lock = new();
    private CancellationTokenSource? _bgCts;
    private int _queuedNotes = 0;

    public int QueuedNotes
    {
        get
        {
            lock (_lock) return _queuedNotes;
        }
    }

    public void Play(string musicCommands) => Play(musicCommands, null);

    public void Play(string musicCommands, BasicEnvironment? env)
    {
        if (string.IsNullOrWhiteSpace(musicCommands)) return;

        var result = MusicPlayer.ParseWithMode(musicCommands, env);
        var notes = result.Notes;
        if (notes.Count == 0) return;

        if (result.IsBackground)
        {
            lock (_lock)
            {
                _bgCts?.Cancel();
                _bgCts = new CancellationTokenSource();
                _queuedNotes += notes.Count;
            }

            var token = _bgCts.Token;
            Task.Run(() =>
            {
                PlayNotesSequence(notes, token, isBackground: true);
            }, token);
        }
        else
        {
            PlayNotesSequence(notes, CancellationToken.None, isBackground: false);
        }
    }

    private void PlayNotesSequence(List<MusicNote> notes, CancellationToken token, bool isBackground)
    {
        try
        {
            byte[] wav = SquareWaveSynthesizer.SynthesizeNotesWav(notes);
            if (wav.Length > 0 && !token.IsCancellationRequested)
            {
                SoundEngine.PlaySync(wav);
                if (isBackground)
                {
                    lock (_lock) _queuedNotes = Math.Max(0, _queuedNotes - notes.Count);
                }
                return;
            }
        }
        catch
        {
            // Fall through to note-by-note fallback if synthesis fails
        }

        foreach (var note in notes)
        {
            if (token.IsCancellationRequested) break;

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

            if (note.RestMs > 0 && !token.IsCancellationRequested)
            {
                Thread.Sleep(note.RestMs);
            }

            if (isBackground)
            {
                lock (_lock) _queuedNotes = Math.Max(0, _queuedNotes - 1);
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _bgCts?.Cancel();
            _queuedNotes = 0;
        }
        SoundEngine.Stop();
    }
}
