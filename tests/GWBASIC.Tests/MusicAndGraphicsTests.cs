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

    [Fact]
    public void TestGraphicsGetAndPutSpriteBlitting()
    {
        _interpreter.ExecuteInputLine("SCREEN 1");
        _interpreter.ExecuteInputLine("LINE (10, 10)-(15, 15), 3, BF");
        Assert.Equal(3, _screen.Point(12, 12));

        // GET sprite into array A
        _interpreter.ExecuteInputLine("DIM A%(50)");
        _interpreter.ExecuteInputLine("GET (10, 10)-(15, 15), A%");

        // PUT sprite at (30, 30) with PSET
        _interpreter.ExecuteInputLine("PUT (30, 30), A%, PSET");
        Assert.Equal(3, _screen.Point(32, 32));

        // PUT sprite at (30, 30) with XOR - should erase it back to 0
        _interpreter.ExecuteInputLine("PUT (30, 30), A%, XOR");
        Assert.Equal(0, _screen.Point(32, 32));
    }

    [Fact]
    public void TestGraphicsWindowAndViewport()
    {
        _interpreter.ExecuteInputLine("SCREEN 1");
        // Set a window from (-10, -10) to (10, 10)
        _interpreter.ExecuteInputLine("WINDOW (-10, -10)-(10, 10)");
        // Plot (0, 0) - should map to center of screen (160, 100)
        _interpreter.ExecuteInputLine("PSET (0, 0), 2");

        Assert.Equal(2, _screen.Point(160, 100));

        // Reset window
        _interpreter.ExecuteInputLine("WINDOW");

        // Set viewport with clipping
        _interpreter.ExecuteInputLine("VIEW (50, 50)-(100, 100)");
        // PSET inside viewport
        _interpreter.ExecuteInputLine("PSET (10, 10), 1"); // Offset by (50, 50) => (60, 60)
        Assert.Equal(1, _screen.Point(60, 60));

        // PSET outside viewport - should be clipped
        _interpreter.ExecuteInputLine("PSET (200, 200), 1");
        Assert.Equal(0, _screen.Point(250, 250));
    }

    [Fact]
    public void TestDrawExtendedMacrosAndTurnAngle()
    {
        _interpreter.ExecuteInputLine("SCREEN 1");
        _interpreter.ExecuteInputLine("PSET (100, 100), 0");
        _interpreter.ExecuteInputLine("S$ = \"U10 R10\"");
        _interpreter.ExecuteInputLine("DRAW \"C2 XS$;\"");

        Assert.Equal(2, _screen.Point(100, 90));
        Assert.Equal(2, _screen.Point(110, 90));

        // Turn angle TA 90 (counterclockwise: right becomes up)
        _interpreter.ExecuteInputLine("PSET (50, 50), 0");
        _interpreter.ExecuteInputLine("DRAW \"C1 TA90 R10\"");
        Assert.Equal(1, _screen.Point(50, 40));
    }

    [Fact]
    public void TestMusicBackgroundAndPlayQueue()
    {
        _interpreter.ExecuteInputLine("PLAY \"MB C D E\"");
        Assert.True(_audio.LastPlayWasBackground);
        Assert.True(_audio.QueuedNotes > 0);

        _interpreter.ExecuteInputLine("Q = PLAY(0)");
        Assert.Equal(_audio.QueuedNotes, _env.GetVariable("Q").AsInteger);

        // Music macro expansion in PLAY
        _interpreter.ExecuteInputLine("T$ = \"O3 G A B\"");
        _interpreter.ExecuteInputLine("PLAY \"MF XT$;\"");
        Assert.False(_audio.LastPlayWasBackground);
    }
}
