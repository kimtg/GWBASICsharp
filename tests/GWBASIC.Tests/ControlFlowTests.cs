using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class ControlFlowTests
{
    private readonly VirtualScreenDriver _screen;
    private readonly VirtualAudioDriver _audio;
    private readonly VirtualInputDriver _input;
    private readonly VirtualFileSystemDriver _fs;
    private readonly BasicEnvironment _env;
    private readonly Interpreter _interpreter;

    public ControlFlowTests()
    {
        _screen = new VirtualScreenDriver();
        _audio = new VirtualAudioDriver();
        _input = new VirtualInputDriver();
        _fs = new VirtualFileSystemDriver();
        _env = new BasicEnvironment(_screen, _audio, _input, _fs);
        _interpreter = new Interpreter(_env);
    }

    [Fact]
    public void TestForNextLoopWithStep()
    {
        _interpreter.ExecuteInputLine("10 SUM = 0");
        _interpreter.ExecuteInputLine("20 FOR I = 1 TO 10 STEP 2");
        _interpreter.ExecuteInputLine("30 SUM = SUM + I");
        _interpreter.ExecuteInputLine("40 NEXT I");
        _interpreter.Run();

        // 1 + 3 + 5 + 7 + 9 = 25
        Assert.Equal(25, _env.GetVariable("SUM").AsSingle);
        // In GW-BASIC, I exits with value 11
        Assert.Equal(11, _env.GetVariable("I").AsSingle);
    }

    [Fact]
    public void TestForNextLoopNegativeStep()
    {
        _interpreter.ExecuteInputLine("10 S = 0");
        _interpreter.ExecuteInputLine("20 FOR I = 10 TO 1 STEP -3");
        _interpreter.ExecuteInputLine("30 S = S + I");
        _interpreter.ExecuteInputLine("40 NEXT I");
        _interpreter.Run();

        // 10 + 7 + 4 + 1 = 22
        Assert.Equal(22, _env.GetVariable("S").AsSingle);
    }

    [Fact]
    public void TestNestedForNext()
    {
        _interpreter.ExecuteInputLine("10 COUNT = 0");
        _interpreter.ExecuteInputLine("20 FOR I = 1 TO 3");
        _interpreter.ExecuteInputLine("30 FOR J = 1 TO 4");
        _interpreter.ExecuteInputLine("40 COUNT = COUNT + 1");
        _interpreter.ExecuteInputLine("50 NEXT J, I"); // Multi-variable NEXT!
        _interpreter.Run();

        Assert.Equal(12, _env.GetVariable("COUNT").AsSingle);
    }

    [Fact]
    public void TestWhileWendLoop()
    {
        _interpreter.ExecuteInputLine("10 X = 1");
        _interpreter.ExecuteInputLine("20 WHILE X < 100");
        _interpreter.ExecuteInputLine("30 X = X * 2");
        _interpreter.ExecuteInputLine("40 WEND");
        _interpreter.Run();

        Assert.Equal(128, _env.GetVariable("X").AsSingle);
    }

    [Fact]
    public void TestGosubAndReturn()
    {
        _interpreter.ExecuteInputLine("10 X = 5");
        _interpreter.ExecuteInputLine("20 GOSUB 100");
        _interpreter.ExecuteInputLine("30 GOTO 200");
        _interpreter.ExecuteInputLine("100 X = X * 3: RETURN");
        _interpreter.ExecuteInputLine("200 X = X + 1");
        _interpreter.Run();

        // 5 * 3 = 15, then + 1 = 16
        Assert.Equal(16, _env.GetVariable("X").AsSingle);
    }

    [Fact]
    public void TestOnGotoAndOnGosub()
    {
        _interpreter.ExecuteInputLine("10 K = 2");
        _interpreter.ExecuteInputLine("20 ON K GOTO 100, 200, 300");
        _interpreter.ExecuteInputLine("100 R = 1: GOTO 400");
        _interpreter.ExecuteInputLine("200 R = 2: GOTO 400");
        _interpreter.ExecuteInputLine("300 R = 3: GOTO 400");
        _interpreter.ExecuteInputLine("400 REM DONE");
        _interpreter.Run();

        Assert.Equal(2, _env.GetVariable("R").AsSingle);
    }

    [Fact]
    public void TestIfThenElseImpliedGoto()
    {
        _interpreter.ExecuteInputLine("10 X = 10");
        _interpreter.ExecuteInputLine("20 IF X = 10 THEN 100 ELSE 200");
        _interpreter.ExecuteInputLine("100 Y = 1: GOTO 300");
        _interpreter.ExecuteInputLine("200 Y = 2: GOTO 300");
        _interpreter.ExecuteInputLine("300 REM DONE");
        _interpreter.Run();

        Assert.Equal(1, _env.GetVariable("Y").AsSingle);
    }
}
