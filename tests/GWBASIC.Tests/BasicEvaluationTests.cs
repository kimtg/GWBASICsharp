using GWBASIC.Core.Common;
using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class BasicEvaluationTests
{
    private readonly VirtualScreenDriver _screen;
    private readonly VirtualAudioDriver _audio;
    private readonly VirtualInputDriver _input;
    private readonly VirtualFileSystemDriver _fs;
    private readonly BasicEnvironment _env;
    private readonly Interpreter _interpreter;

    public BasicEvaluationTests()
    {
        _screen = new VirtualScreenDriver();
        _audio = new VirtualAudioDriver();
        _input = new VirtualInputDriver();
        _fs = new VirtualFileSystemDriver();
        _env = new BasicEnvironment(_screen, _audio, _input, _fs);
        _interpreter = new Interpreter(_env);
    }

    [Fact]
    public void TestArithmeticOperators()
    {
        _interpreter.ExecuteInputLine("X = 2 + 3 * 4");
        Assert.Equal(14, _env.GetVariable("X").AsSingle);

        _interpreter.ExecuteInputLine("Y = 2 ^ 3 ^ 2"); // 2 ^ 9 in BASIC right-associative or 2^3^2 = 512
        Assert.Equal(512, _env.GetVariable("Y").AsSingle);

        _interpreter.ExecuteInputLine("Z = 17 \\ 5"); // Integer division
        Assert.Equal(3, _env.GetVariable("Z").AsInteger);

        _interpreter.ExecuteInputLine("M = 17 MOD 5");
        Assert.Equal(2, _env.GetVariable("M").AsInteger);
    }

    [Fact]
    public void TestRelationalOperators()
    {
        _interpreter.ExecuteInputLine("A = (5 > 3)");
        Assert.Equal(-1, _env.GetVariable("A").AsInteger); // -1 is True in GW-BASIC

        _interpreter.ExecuteInputLine("B = (5 = 4)");
        Assert.Equal(0, _env.GetVariable("B").AsInteger); // 0 is False in GW-BASIC
    }

    [Fact]
    public void TestBitwiseLogicalOperators()
    {
        _interpreter.ExecuteInputLine("A = 5 AND 3");
        Assert.Equal(1, _env.GetVariable("A").AsInteger);

        _interpreter.ExecuteInputLine("B = 5 OR 2");
        Assert.Equal(7, _env.GetVariable("B").AsInteger);

        _interpreter.ExecuteInputLine("C = 5 XOR 3");
        Assert.Equal(6, _env.GetVariable("C").AsInteger);

        _interpreter.ExecuteInputLine("D = NOT 0");
        Assert.Equal(-1, _env.GetVariable("D").AsInteger);

        _interpreter.ExecuteInputLine("E = 5 EQV 3");
        Assert.Equal(-7, _env.GetVariable("E").AsInteger);

        _interpreter.ExecuteInputLine("F = 5 IMP 3");
        Assert.Equal(-5, _env.GetVariable("F").AsInteger);
    }

    [Fact]
    public void TestVariableSigilsAndDistinctScopes()
    {
        _interpreter.ExecuteInputLine("A% = 10");
        _interpreter.ExecuteInputLine("A! = 20.5");
        _interpreter.ExecuteInputLine("A# = 30.75");
        _interpreter.ExecuteInputLine("A$ = \"HELLO\"");

        Assert.Equal(10, _env.GetVariable("A%").AsInteger);
        Assert.Equal(20.5f, _env.GetVariable("A!").AsSingle);
        Assert.Equal(30.75, _env.GetVariable("A#").AsDouble);
        Assert.Equal("HELLO", _env.GetVariable("A$").AsString);
    }

    [Fact]
    public void TestDefIntDefStr()
    {
        _interpreter.ExecuteInputLine("DEFINT I-N");
        _interpreter.ExecuteInputLine("DEFSTR S");

        _interpreter.ExecuteInputLine("I = 12.8");
        Assert.Equal(13, _env.GetVariable("I").AsInteger); // Banker's rounding on integer assignment

        _interpreter.ExecuteInputLine("S = \"GW-BASIC\"");
        Assert.Equal("GW-BASIC", _env.GetVariable("S").AsString);
    }

    [Fact]
    public void TestHexAndOctalLiterals()
    {
        _interpreter.ExecuteInputLine("H = &H1A");
        Assert.Equal(26, _env.GetVariable("H").AsInteger);

        _interpreter.ExecuteInputLine("O = &O77");
        Assert.Equal(63, _env.GetVariable("O").AsInteger);

        _interpreter.ExecuteInputLine("O2 = &77");
        Assert.Equal(63, _env.GetVariable("O2").AsInteger);
    }
}
