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

    [Fact]
    public void TestAutoDefaultAndBreak()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("AUTO");

        Assert.True(_env.IsAutoMode);
        Assert.Equal(10, _env.AutoLineNumber);
        Assert.Equal(10, _env.AutoIncrement);
        Assert.EndsWith("10 ", _screen.GetOutputLog());

        // Enter statement without line number -> prepends 10
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT \"HELLO\"");
        Assert.Equal(20, _env.AutoLineNumber);
        Assert.EndsWith("20 ", _screen.GetOutputLog());
        Assert.NotNull(_env.Program.GetLine(10));
        Assert.Contains("PRINT \"HELLO\"", _env.Program.GetLine(10)!.Text);

        // Enter statement for line 20
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT \"WORLD\"");
        Assert.Equal(30, _env.AutoLineNumber);
        Assert.EndsWith("30 ", _screen.GetOutputLog());
        Assert.NotNull(_env.Program.GetLine(20));

        // Empty enter skips line 30
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("");
        Assert.Equal(40, _env.AutoLineNumber);
        Assert.EndsWith("40 ", _screen.GetOutputLog());
        Assert.Null(_env.Program.GetLine(30));

        // Break with \x03 exits AUTO mode
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("\x03");
        Assert.False(_env.IsAutoMode);
        Assert.Contains("Ok", _screen.GetOutputLog());
        Assert.Equal(2, _env.Program.Count);
    }

    [Fact]
    public void TestAutoCustomStartAndIncrement()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("AUTO 100, 50");

        Assert.True(_env.IsAutoMode);
        Assert.Equal(100, _env.AutoLineNumber);
        Assert.Equal(50, _env.AutoIncrement);
        Assert.EndsWith("100 ", _screen.GetOutputLog());

        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT \"A\"");
        Assert.Equal(150, _env.AutoLineNumber);
        Assert.EndsWith("150 ", _screen.GetOutputLog());

        // Entering an explicit line number sets that line and updates next auto line
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("200 PRINT \"B\"");
        Assert.NotNull(_env.Program.GetLine(200));
        Assert.Equal(250, _env.AutoLineNumber);
        Assert.EndsWith("250 ", _screen.GetOutputLog());

        // Exit AUTO mode
        _interpreter.ExecuteInputLine("\x03");
        Assert.False(_env.IsAutoMode);

        // Subsequent AUTO with comma inherits previous increment 50
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("AUTO 300,");
        Assert.Equal(300, _env.AutoLineNumber);
        Assert.Equal(50, _env.AutoIncrement);
        Assert.EndsWith("300 ", _screen.GetOutputLog());
        _interpreter.ExecuteInputLine("\x03");
    }

    [Fact]
    public void TestAutoDotCurrentLine()
    {
        _interpreter.ExecuteInputLine("50 PRINT \"ORIGINAL\"");
        Assert.Equal(50, _env.CurrentLineNumber);

        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("AUTO ., 5");

        Assert.True(_env.IsAutoMode);
        Assert.Equal(50, _env.AutoLineNumber);
        Assert.Equal(5, _env.AutoIncrement);
        // Line 50 exists, so prompt should have asterisk
        Assert.EndsWith("50* ", _screen.GetOutputLog());

        // Empty line preserves original line 50
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("");
        Assert.Equal(55, _env.AutoLineNumber);
        Assert.EndsWith("55 ", _screen.GetOutputLog());
        Assert.Contains("ORIGINAL", _env.Program.GetLine(50)!.Text);

        _interpreter.ExecuteInputLine("\x03");
    }

    [Fact]
    public void TestAutoCommaOmitStartLine()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("AUTO , 25");

        Assert.True(_env.IsAutoMode);
        Assert.Equal(0, _env.AutoLineNumber);
        Assert.Equal(25, _env.AutoIncrement);
        Assert.EndsWith("0 ", _screen.GetOutputLog());

        _interpreter.ExecuteInputLine("REM ZERO");
        Assert.NotNull(_env.Program.GetLine(0));
        Assert.Equal(25, _env.AutoLineNumber);
        Assert.EndsWith("25 ", _screen.GetOutputLog());

        _interpreter.ExecuteInputLine("\x03");
    }

    [Fact]
    public void TestAutoExistingLineAsteriskAndOverwrite()
    {
        _interpreter.ExecuteInputLine("10 PRINT \"OLD\"");
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("AUTO 10");

        Assert.EndsWith("10* ", _screen.GetOutputLog());

        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("PRINT \"NEW\"");
        Assert.Equal(20, _env.AutoLineNumber);
        Assert.Contains("PRINT \"NEW\"", _env.Program.GetLine(10)!.Text);

        _interpreter.ExecuteInputLine("\x03");
    }

    [Fact]
    public void TestAutoExceedMaxLineNumber()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("AUTO 65525, 10");
        Assert.True(_env.IsAutoMode);

        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("REM LAST");
        Assert.NotNull(_env.Program.GetLine(65525));
        // Next line would be 65535 > 65529, so auto mode exits with Ok
        Assert.False(_env.IsAutoMode);
        Assert.Contains("Ok", _screen.GetOutputLog());
    }

    [Fact]
    public void TestAutoIllegalDirectInProgram()
    {
        _interpreter.ExecuteInputLine("10 AUTO");
        _screen.ClearOutputLog();
        _interpreter.Run();
        Assert.Contains("Illegal direct in 10", _screen.GetOutputLog());
    }

    [Fact]
    public void TestAutoInvalidArguments()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("AUTO 70000");
        Assert.Contains("Illegal function call", _screen.GetOutputLog());
        Assert.False(_env.IsAutoMode);

        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("AUTO 10, 0");
        Assert.Contains("Illegal function call", _screen.GetOutputLog());
        Assert.False(_env.IsAutoMode);

        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("AUTO 10, 70000");
        Assert.Contains("Illegal function call", _screen.GetOutputLog());
        Assert.False(_env.IsAutoMode);
    }

    [Fact]
    public void TestResumeVariableEvaluation()
    {
        _interpreter.ExecuteInputLine("10 ON ERROR GOTO 100");
        _interpreter.ExecuteInputLine("20 X = 1 / 0");
        _interpreter.ExecuteInputLine("30 Y = 99");
        _interpreter.ExecuteInputLine("40 END");
        _interpreter.ExecuteInputLine("100 TARGET = 30: RESUME TARGET");
        _interpreter.Run();

        Assert.Equal(99, _env.GetVariable("Y").AsSingle);
    }

    [Fact]
    public void TestRenumPreservesStringLiteralsAndComments()
    {
        _interpreter.ExecuteInputLine("10 GOTO 30");
        _interpreter.ExecuteInputLine("20 PRINT \"GOTO 30 IN STRING\"");
        _interpreter.ExecuteInputLine("30 REM GOTO 10 IN COMMENT");
        _interpreter.ExecuteInputLine("RENUM 100, 10, 10");

        var line100 = _env.Program.GetLine(100);
        Assert.NotNull(line100);
        Assert.Equal("GOTO 120", line100.Text.Trim());

        var line110 = _env.Program.GetLine(110);
        Assert.NotNull(line110);
        Assert.Contains("\"GOTO 30 IN STRING\"", line110.Text);

        var line120 = _env.Program.GetLine(120);
        Assert.NotNull(line120);
        Assert.Contains("REM GOTO 10 IN COMMENT", line120.Text);
    }

    [Fact]
    public void TestSpacelessKeywords()
    {
        _interpreter.ExecuteInputLine("10SUM=0");
        _interpreter.ExecuteInputLine("20FORI=1TO5");
        _interpreter.ExecuteInputLine("30SUM=SUM+I");
        _interpreter.ExecuteInputLine("40NEXTI");
        _interpreter.Run();

        Assert.Equal(15, _env.GetVariable("SUM").AsSingle);
        Assert.Equal(6, _env.GetVariable("I").AsSingle);
    }

    [Fact]
    public void TestLineNumberOutOfRange()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("70000 PRINT \"TOO BIG\"");
        Assert.Contains("Undefined line number", _screen.GetOutputLog());
    }
}
