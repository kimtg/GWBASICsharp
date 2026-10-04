using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class ErrorHandlingTests
{
    private readonly VirtualScreenDriver _screen;
    private readonly VirtualAudioDriver _audio;
    private readonly VirtualInputDriver _input;
    private readonly VirtualFileSystemDriver _fs;
    private readonly BasicEnvironment _env;
    private readonly Interpreter _interpreter;

    public ErrorHandlingTests()
    {
        _screen = new VirtualScreenDriver();
        _audio = new VirtualAudioDriver();
        _input = new VirtualInputDriver();
        _fs = new VirtualFileSystemDriver();
        _env = new BasicEnvironment(_screen, _audio, _input, _fs);
        _interpreter = new Interpreter(_env);
    }

    [Fact]
    public void TestOnErrorGotoAndResumeNext()
    {
        _interpreter.ExecuteInputLine("10 ON ERROR GOTO 100");
        _interpreter.ExecuteInputLine("20 X = 1 / 0"); // Division by zero
        _interpreter.ExecuteInputLine("30 Y = 42");
        _interpreter.ExecuteInputLine("40 END");
        _interpreter.ExecuteInputLine("100 E = ERR: L = ERL: RESUME NEXT");
        _interpreter.Run();

        Assert.Equal(11, _env.GetVariable("E").AsInteger); // 11 is Division by zero in GW-BASIC
        Assert.Equal(20, _env.GetVariable("L").AsInteger); // Error occurred in line 20
        Assert.Equal(42, _env.GetVariable("Y").AsSingle);  // Resumed at line 30
    }
}
