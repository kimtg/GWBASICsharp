using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class FunctionTests
{
    private readonly VirtualScreenDriver _screen;
    private readonly VirtualAudioDriver _audio;
    private readonly VirtualInputDriver _input;
    private readonly VirtualFileSystemDriver _fs;
    private readonly BasicEnvironment _env;
    private readonly Interpreter _interpreter;

    public FunctionTests()
    {
        _screen = new VirtualScreenDriver();
        _audio = new VirtualAudioDriver();
        _input = new VirtualInputDriver();
        _fs = new VirtualFileSystemDriver();
        _env = new BasicEnvironment(_screen, _audio, _input, _fs);
        _interpreter = new Interpreter(_env);
    }

    [Fact]
    public void TestStringFunctions()
    {
        _interpreter.ExecuteInputLine("A$ = \"Hello World!\"");
        _interpreter.ExecuteInputLine("L$ = LEFT$(A$, 5)");
        _interpreter.ExecuteInputLine("R$ = RIGHT$(A$, 6)");
        _interpreter.ExecuteInputLine("M$ = MID$(A$, 7, 5)");
        _interpreter.ExecuteInputLine("N = LEN(A$)");
        _interpreter.ExecuteInputLine("I = INSTR(A$, \"World\")");

        Assert.Equal("Hello", _env.GetVariable("L$").AsString);
        Assert.Equal("World!", _env.GetVariable("R$").AsString);
        Assert.Equal("World", _env.GetVariable("M$").AsString);
        Assert.Equal(12, _env.GetVariable("N").AsInteger);
        Assert.Equal(7, _env.GetVariable("I").AsInteger);
    }

    [Fact]
    public void TestMidSetStatement()
    {
        _interpreter.ExecuteInputLine("A$ = \"KANSAS CITY\"");
        _interpreter.ExecuteInputLine("MID$(A$, 8, 4) = \"DOROTHY\""); // Replaces 4 chars starting at 8
        Assert.Equal("KANSAS DORO", _env.GetVariable("A$").AsString);
    }

    [Fact]
    public void TestChrAscStrVal()
    {
        _interpreter.ExecuteInputLine("C$ = CHR$(65)");
        Assert.Equal("A", _env.GetVariable("C$").AsString);

        _interpreter.ExecuteInputLine("A = ASC(\"B\")");
        Assert.Equal(66, _env.GetVariable("A").AsInteger);

        _interpreter.ExecuteInputLine("S$ = STR$(123)");
        Assert.Equal(" 123", _env.GetVariable("S$").AsString); // In GW-BASIC, STR$(positive) has leading space!

        _interpreter.ExecuteInputLine("V = VAL(\"456.78abc\")");
        Assert.Equal(456.78, _env.GetVariable("V").AsDouble, 2);
    }

    [Fact]
    public void TestMathFunctions()
    {
        _interpreter.ExecuteInputLine("A = INT(-3.7)");
        Assert.Equal(-4, _env.GetVariable("A").AsSingle);

        _interpreter.ExecuteInputLine("B = FIX(-3.7)");
        Assert.Equal(-3, _env.GetVariable("B").AsSingle);

        _interpreter.ExecuteInputLine("C = SQR(16)");
        Assert.Equal(4, _env.GetVariable("C").AsSingle);

        _interpreter.ExecuteInputLine("D = ABS(-42)");
        Assert.Equal(42, _env.GetVariable("D").AsSingle);
    }

    [Fact]
    public void TestDefFn()
    {
        _interpreter.ExecuteInputLine("DEF FNA(X) = X * X + 1");
        _interpreter.ExecuteInputLine("R = FNA(5)");
        Assert.Equal(26, _env.GetVariable("R").AsSingle);

        _interpreter.ExecuteInputLine("DEF FNHYP(A, B) = SQR(A * A + B * B)");
        _interpreter.ExecuteInputLine("H = FNHYP(3, 4)");
        Assert.Equal(5, _env.GetVariable("H").AsSingle);
    }

    [Fact]
    public void TestArraysAndOptionBase()
    {
        _interpreter.ExecuteInputLine("OPTION BASE 1");
        _interpreter.ExecuteInputLine("DIM ARR(5, 5)");
        _interpreter.ExecuteInputLine("ARR(2, 3) = 99");

        Assert.Equal(99, _env.GetArrayElement("ARR", new[] { 2, 3 }).AsSingle);
    }

    [Fact]
    public void TestSwap()
    {
        _interpreter.ExecuteInputLine("A = 10 : B = 20");
        _interpreter.ExecuteInputLine("SWAP A, B");

        Assert.Equal(20, _env.GetVariable("A").AsSingle);
        Assert.Equal(10, _env.GetVariable("B").AsSingle);
    }
}
