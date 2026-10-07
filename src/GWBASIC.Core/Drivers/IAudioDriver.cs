namespace GWBASIC.Core.Drivers;

/// <summary>
/// Driver for PC speaker sound synthesis, beeps, and PLAY music macro language.
/// </summary>
public interface IAudioDriver
{
    void Beep();
    void Sound(int frequencyHz, int durationClockTicks);
    void Play(string musicCommands);
    void Play(string musicCommands, GWBASIC.Core.Runtime.BasicEnvironment? env) => Play(musicCommands);
    void Stop();
    int QueuedNotes => 0;
}
