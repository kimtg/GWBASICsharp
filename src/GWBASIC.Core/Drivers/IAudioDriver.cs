namespace GWBASIC.Core.Drivers;

/// <summary>
/// Driver for PC speaker sound synthesis, beeps, and PLAY music macro language.
/// </summary>
public interface IAudioDriver
{
    void Beep();
    void Sound(int frequencyHz, int durationClockTicks);
    void Play(string musicCommands);
}
