using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class ProgramEditingTests
{
    private readonly VirtualScreenDriver _screen;
    private readonly VirtualAudioDriver _audio;
    private readonly VirtualInputDriver _input;
    private readonly VirtualFileSystemDriver _fs;
    private readonly BasicEnvironment _env;
    private readonly Interpreter _interpreter;

    public ProgramEditingTests()
    {
        _screen = new VirtualScreenDriver();
        _audio = new VirtualAudioDriver();
        _input = new VirtualInputDriver();
        _fs = new VirtualFileSystemDriver();
        _env = new BasicEnvironment(_screen, _audio, _input, _fs);
        _interpreter = new Interpreter(_env);
    }

    [Fact]
    public void TestLineAddEditDelete()
    {
        _interpreter.ExecuteInputLine("10 PRINT \"ONE\"");
        _interpreter.ExecuteInputLine("20 PRINT \"TWO\"");
        _interpreter.ExecuteInputLine("30 PRINT \"THREE\"");
        Assert.Equal(3, _env.Program.Count);

        // Delete line 20
        _interpreter.ExecuteInputLine("20");
        Assert.Equal(2, _env.Program.Count);
        Assert.Null(_env.Program.GetLine(20));

        // Delete command
        _interpreter.ExecuteInputLine("DELETE 10-30");
        Assert.Equal(0, _env.Program.Count);
    }

    [Fact]
    public void TestRenumPatchesLineReferences()
    {
        _interpreter.ExecuteInputLine("10 GOTO 30");
        _interpreter.ExecuteInputLine("20 PRINT \"SKIPPED\"");
        _interpreter.ExecuteInputLine("30 GOSUB 50");
        _interpreter.ExecuteInputLine("40 END");
        _interpreter.ExecuteInputLine("50 PRINT \"HELLO\": RETURN");

        // Renumber starting at 100 with step 10
        _interpreter.ExecuteInputLine("RENUM 100, 10, 10");

        // Old 10 -> 100, old 20 -> 110, old 30 -> 120, old 40 -> 130, old 50 -> 140
        var line100 = _env.Program.GetLine(100);
        Assert.NotNull(line100);
        Assert.Contains("GOTO 120", line100.Text);

        var line120 = _env.Program.GetLine(120);
        Assert.NotNull(line120);
        Assert.Contains("GOSUB 140", line120.Text);
    }

    [Fact]
    public void TestDataReadRestore()
    {
        _interpreter.ExecuteInputLine("10 DATA 100, 200, \"THREE\", FOUR");
        _interpreter.ExecuteInputLine("20 READ A, B, C$, D$");
        _interpreter.ExecuteInputLine("30 RESTORE");
        _interpreter.ExecuteInputLine("40 READ X, Y");
        _interpreter.Run();

        Assert.Equal(100, _env.GetVariable("A").AsSingle);
        Assert.Equal(200, _env.GetVariable("B").AsSingle);
        Assert.Equal("THREE", _env.GetVariable("C$").AsString);
        Assert.Equal("FOUR", _env.GetVariable("D$").AsString);

        Assert.Equal(100, _env.GetVariable("X").AsSingle);
        Assert.Equal(200, _env.GetVariable("Y").AsSingle);
    }

    [Fact]
    public void TestKeyOnOffImmediate()
    {
        // Initial state: row 25 has function keys
        string row25Initial = _screen.ReadLine(25);
        Assert.Contains("1LIST", row25Initial);
        Assert.Contains("0SCREEN", row25Initial);
        Assert.True(_screen.KeyRowVisible);

        // Long macro: should truncate on row 25 and not overflow
        _interpreter.ExecuteInputLine("KEY 10, \"VERY LONG MACRO THAT WOULD OVERFLOW PAST 80 COLUMNS\"");
        string row25Long = _screen.ReadLine(25);
        Assert.Contains("0VERY LO", row25Long);
        Assert.True(row25Long.Length <= 80);

        // KEY OFF without CLS: immediately hides row 25 completely
        _interpreter.ExecuteInputLine("KEY OFF");
        Assert.False(_screen.KeyRowVisible);
        string row25Off = _screen.ReadLine(25);
        Assert.Equal("", row25Off.Trim());

        // KEY ON without CLS: immediately restores row 25
        _interpreter.ExecuteInputLine("KEY ON");
        Assert.True(_screen.KeyRowVisible);
        string row25On = _screen.ReadLine(25);
        Assert.Contains("1LIST", row25On);
    }
}
