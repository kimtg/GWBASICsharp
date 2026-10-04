namespace GWBASIC.Core.Drivers;

/// <summary>
/// Driver for keyboard input and interactive line editing.
/// </summary>
public interface IInputDriver
{
    bool KeyAvailable { get; }
    string? ReadInkey();
    string ReadLine();
}
