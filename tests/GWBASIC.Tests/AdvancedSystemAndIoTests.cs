using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class AdvancedSystemAndIoTests
{
    private readonly VirtualScreenDriver _screen;
    private readonly VirtualAudioDriver _audio;
    private readonly VirtualInputDriver _input;
    private readonly VirtualFileSystemDriver _fs;
    private readonly BasicEnvironment _env;
    private readonly Interpreter _interpreter;

    public AdvancedSystemAndIoTests()
    {
        _screen = new VirtualScreenDriver();
        _audio = new VirtualAudioDriver();
        _input = new VirtualInputDriver();
        _fs = new VirtualFileSystemDriver();
        _env = new BasicEnvironment(_screen, _audio, _input, _fs);
        _interpreter = new Interpreter(_env);
    }

    [Fact]
    public void TestResetClosesAllFiles()
    {
        _fs.WriteAllText("A.DAT", "Hello");
        _fs.WriteAllText("B.DAT", "World");

        _interpreter.ExecuteInputLine("OPEN \"A.DAT\" FOR INPUT AS #1");
        _interpreter.ExecuteInputLine("OPEN \"B.DAT\" FOR INPUT AS #2");
        Assert.False(_env.IsEof(1));
        Assert.False(_env.IsEof(2));

        _interpreter.ExecuteInputLine("RESET");
        // Verify files are closed: IsEof should throw BadFileNumber
        Assert.Throws<GWBASIC.Core.Common.BasicException>(() => _env.IsEof(1));
        Assert.Throws<GWBASIC.Core.Common.BasicException>(() => _env.IsEof(2));
    }

    [Fact]
    public void TestDateAndTimeStatementsAndFunctions()
    {
        _interpreter.ExecuteInputLine("DATE$ = \"12-25-2026\"");
        _interpreter.ExecuteInputLine("TIME$ = \"18:30:00\"");

        _interpreter.ExecuteInputLine("D$ = DATE$");
        _interpreter.ExecuteInputLine("T$ = TIME$");

        Assert.Equal("12-25-2026", _env.GetVariable("D$").AsString);
        Assert.Equal("18:30:00", _env.GetVariable("T$").AsString);
    }

    [Fact]
    public void TestEnvironStatementAndFunction()
    {
        _interpreter.ExecuteInputLine("ENVIRON \"GWBASIC_TEST_VAR=ACTIVE\"");
        _interpreter.ExecuteInputLine("V$ = ENVIRON$(\"GWBASIC_TEST_VAR\")");
        Assert.Equal("ACTIVE", _env.GetVariable("V$").AsString);

        // Access by index 1
        _interpreter.ExecuteInputLine("IDX1$ = ENVIRON$(1)");
        Assert.Equal("GWBASIC_TEST_VAR=ACTIVE", _env.GetVariable("IDX1$").AsString);
    }

    [Fact]
    public void TestInputStringFromFileAndConsole()
    {
        _fs.WriteAllText("TEST.TXT", "ABCDEFGH");
        _interpreter.ExecuteInputLine("OPEN \"TEST.TXT\" FOR INPUT AS #1");
        _interpreter.ExecuteInputLine("CHUNK$ = INPUT$(4, #1)");
        _interpreter.ExecuteInputLine("CLOSE #1");

        Assert.Equal("ABCD", _env.GetVariable("CHUNK$").AsString);
    }

    [Fact]
    public void TestInpAndOutPorts()
    {
        _interpreter.ExecuteInputLine("OUT 888, 123");
        _interpreter.ExecuteInputLine("PORTVAL = INP(888)");
        Assert.Equal(123, _env.GetVariable("PORTVAL").AsInteger);
    }

    [Fact]
    public void TestVarptrAndLpos()
    {
        _interpreter.ExecuteInputLine("X% = 42");
        _interpreter.ExecuteInputLine("ADDR% = VARPTR(X%)");
        Assert.True(_env.GetVariable("ADDR%").AsInteger > 0);

        _interpreter.ExecuteInputLine("LP% = LPOS(0)");
        Assert.Equal(0, _env.GetVariable("LP%").AsInteger);
    }

    [Fact]
    public void TestCommonAndChainVariablePreservation()
    {
        _fs.WriteAllText("PROG2.BAS", "10 RESULT$ = MSG$ + \" WORLD\"\n20 TOTAL = SCORE + 10\n");

        _interpreter.ExecuteInputLine("COMMON MSG$");
        _interpreter.ExecuteInputLine("MSG$ = \"HELLO\"");
        _interpreter.ExecuteInputLine("SCORE = 50"); // SCORE is not common
        _interpreter.ExecuteInputLine("CHAIN \"PROG2.BAS\"");
        _interpreter.Run();

        Assert.Equal("HELLO WORLD", _env.GetVariable("RESULT$").AsString);
        // SCORE was cleared because it was not declared in COMMON
        Assert.Equal(10, _env.GetVariable("TOTAL").AsInteger);
    }
}
