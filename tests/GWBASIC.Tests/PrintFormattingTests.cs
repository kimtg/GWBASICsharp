using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class PrintFormattingTests
{
    private readonly VirtualScreenDriver _screen;
    private readonly VirtualAudioDriver _audio;
    private readonly VirtualInputDriver _input;
    private readonly VirtualFileSystemDriver _fs;
    private readonly BasicEnvironment _env;
    private readonly Interpreter _interpreter;

    public PrintFormattingTests()
    {
        _screen = new VirtualScreenDriver();
        _audio = new VirtualAudioDriver();
        _input = new VirtualInputDriver();
        _fs = new VirtualFileSystemDriver();
        _env = new BasicEnvironment(_screen, _audio, _input, _fs);
        _interpreter = new Interpreter(_env);
    }

    [Fact]
    public void TestStandardNumberPrintSpacing()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT 5; -5");
        string log = _screen.GetOutputLog();

        // Positive 5 has leading and trailing space: " 5 "
        // Negative -5 has no leading space, but trailing space: "-5 "
        Assert.Contains(" 5 -5 ", log);
    }

    [Fact]
    public void TestPrintUsingNumeric()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT USING \"##.##\"; 12.34");
        string log = _screen.GetOutputLog();
        Assert.Contains("12.34", log);

        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT USING \"$$###.##\"; 45.67");
        log = _screen.GetOutputLog();
        Assert.Contains("$45.67", log);

        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT USING \"**$###.##\"; 45.67");
        log = _screen.GetOutputLog();
        Assert.Contains("***$45.67", log);
    }

    [Fact]
    public void TestPrintUsingString()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT USING \"!\"; \"GW-BASIC\"");
        string log = _screen.GetOutputLog();
        Assert.Contains("G", log);

        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT USING \"\\  \\\"; \"GWBASIC\"");
        log = _screen.GetOutputLog();
        // 2 backslashes + 2 spaces = 4 chars width: "GWBA"
        Assert.Contains("GWBA", log);

        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT USING \"&\"; \"MICROSOFT\"");
        log = _screen.GetOutputLog();
        Assert.Contains("MICROSOFT", log);
    }

    [Fact]
    public void TestPrintCommaZones()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT \"A\", \"B\"");
        // Cursor starts at 1, 'A' printed, comma moves to column 15 where 'B' is printed!
        string line = _screen.ReadLine(1);
        Assert.StartsWith("A", line);
        Assert.Equal('B', line[14]); // 15th character (0-indexed 14)
    }

    [Fact]
    public void TestPrintUsingTrailingDelimiters()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("10 PRINT USING \"##\"; 10");
        _interpreter.ExecuteInputLine("20 PRINT USING \"##\"; 20;");
        _interpreter.ExecuteInputLine("30 PRINT 30");
        _interpreter.Run();

        string log = _screen.GetOutputLog();
        Assert.Contains("10\r\n20 30 \r\n", log);
    }
}
