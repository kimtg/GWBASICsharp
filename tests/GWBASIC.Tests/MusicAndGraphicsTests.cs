using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class MusicAndGraphicsTests
{
    private readonly VirtualScreenDriver _screen;
    private readonly VirtualAudioDriver _audio;
    private readonly VirtualInputDriver _input;
    private readonly VirtualFileSystemDriver _fs;
    private readonly BasicEnvironment _env;
    private readonly Interpreter _interpreter;

    public MusicAndGraphicsTests()
    {
        _screen = new VirtualScreenDriver();
        _audio = new VirtualAudioDriver();
        _input = new VirtualInputDriver();
        _fs = new VirtualFileSystemDriver();
        _env = new BasicEnvironment(_screen, _audio, _input, _fs);
        _interpreter = new Interpreter(_env);
    }

    [Fact]
    public void TestPlayMacroLanguageParsing()
    {
        var notes = MusicPlayer.Parse("T120 O3 L4 C D E F G A B O4 C");
        Assert.Equal(8, notes.Count);

        // First note is C3
        Assert.True(notes[0].FrequencyHz > 120 && notes[0].FrequencyHz < 140); // C3 is ~130.8 Hz
        // Last note is C4 (middle C)
        Assert.True(notes[7].FrequencyHz > 250 && notes[7].FrequencyHz < 270); // C4 is ~261.6 Hz
    }

    [Fact]
    public void TestGraphicsLineAndCircle()
    {
        _interpreter.ExecuteInputLine("SCREEN 1");
        _interpreter.ExecuteInputLine("LINE (10, 10)-(50, 50), 2, B"); // Box in color 2 (cyan/magenta/white)
        _interpreter.ExecuteInputLine("CIRCLE (100, 100), 20, 1");

        // Verify pixels set
        Assert.Equal(2, _screen.Point(10, 10));
        Assert.Equal(2, _screen.Point(50, 50));
        Assert.Equal(2, _screen.Point(10, 50));
        Assert.Equal(2, _screen.Point(50, 10));
    }

    [Fact]
    public void TestDrawCommand()
    {
        _interpreter.ExecuteInputLine("SCREEN 1");
        _interpreter.ExecuteInputLine("PSET (100, 100), 0");
        _interpreter.ExecuteInputLine("DRAW \"C3 U10 R10 D10 L10\""); // Draws a 10x10 square in color 3

        Assert.Equal(3, _screen.Point(100, 90)); // U10
        Assert.Equal(3, _screen.Point(110, 90)); // R10
    }
}
