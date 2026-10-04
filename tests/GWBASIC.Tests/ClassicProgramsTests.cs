using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class ClassicProgramsTests
{
    private readonly VirtualScreenDriver _screen;
    private readonly VirtualAudioDriver _audio;
    private readonly VirtualInputDriver _input;
    private readonly VirtualFileSystemDriver _fs;
    private readonly BasicEnvironment _env;
    private readonly Interpreter _interpreter;

    public ClassicProgramsTests()
    {
        _screen = new VirtualScreenDriver();
        _audio = new VirtualAudioDriver();
        _input = new VirtualInputDriver();
        _fs = new VirtualFileSystemDriver();
        _env = new BasicEnvironment(_screen, _audio, _input, _fs);
        _interpreter = new Interpreter(_env);
    }

    [Fact]
    public void TestPrimeNumbersSieve()
    {
        // Finds primes up to 50
        _interpreter.ExecuteInputLine("10 N = 50");
        _interpreter.ExecuteInputLine("20 DIM P(50)");
        _interpreter.ExecuteInputLine("30 FOR I = 2 TO N: P(I) = 1: NEXT I");
        _interpreter.ExecuteInputLine("40 FOR I = 2 TO SQR(N)");
        _interpreter.ExecuteInputLine("50 IF P(I) = 0 THEN GOTO 80");
        _interpreter.ExecuteInputLine("60 FOR J = I * I TO N STEP I");
        _interpreter.ExecuteInputLine("70 P(J) = 0: NEXT J");
        _interpreter.ExecuteInputLine("80 NEXT I");
        _interpreter.ExecuteInputLine("90 COUNT = 0");
        _interpreter.ExecuteInputLine("100 FOR I = 2 TO N");
        _interpreter.ExecuteInputLine("110 IF P(I) = 1 THEN COUNT = COUNT + 1");
        _interpreter.ExecuteInputLine("120 NEXT I");
        _interpreter.Run();

        // There are 15 primes <= 50: 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47
        Assert.Equal(15, _env.GetVariable("COUNT").AsSingle);
    }

    [Fact]
    public void TestBubbleSort()
    {
        _interpreter.ExecuteInputLine("10 DATA 64, 34, 25, 12, 22, 11, 90");
        _interpreter.ExecuteInputLine("20 N = 7: DIM A(7)");
        _interpreter.ExecuteInputLine("30 FOR I = 1 TO N: READ A(I): NEXT I");
        _interpreter.ExecuteInputLine("40 FOR I = 1 TO N - 1");
        _interpreter.ExecuteInputLine("50 FOR J = 1 TO N - I");
        _interpreter.ExecuteInputLine("60 IF A(J) > A(J + 1) THEN SWAP A(J), A(J + 1)");
        _interpreter.ExecuteInputLine("70 NEXT J: NEXT I");
        _interpreter.Run();

        // Verify sorted: 11, 12, 22, 25, 34, 64, 90
        Assert.Equal(11, _env.GetArrayElement("A", new[] { 1 }).AsSingle);
        Assert.Equal(12, _env.GetArrayElement("A", new[] { 2 }).AsSingle);
        Assert.Equal(22, _env.GetArrayElement("A", new[] { 3 }).AsSingle);
        Assert.Equal(25, _env.GetArrayElement("A", new[] { 4 }).AsSingle);
        Assert.Equal(34, _env.GetArrayElement("A", new[] { 5 }).AsSingle);
        Assert.Equal(64, _env.GetArrayElement("A", new[] { 6 }).AsSingle);
        Assert.Equal(90, _env.GetArrayElement("A", new[] { 7 }).AsSingle);
    }

    [Fact]
    public void TestPeekAndPoke()
    {
        _interpreter.ExecuteInputLine("DEF SEG = &H1000");
        _interpreter.ExecuteInputLine("POKE &H0020, 42");
        _interpreter.ExecuteInputLine("V = PEEK(&H0020)");

        Assert.Equal(42, _env.GetVariable("V").AsInteger);
    }

    [Fact]
    public void TestTronTraceMode()
    {
        _screen.ClearOutputLog();
        _interpreter.ExecuteInputLine("10 TRON");
        _interpreter.ExecuteInputLine("20 X = 1");
        _interpreter.ExecuteInputLine("30 Y = 2");
        _interpreter.ExecuteInputLine("40 TROFF");
        _interpreter.Run();

        string log = _screen.GetOutputLog();
        Assert.Contains("[20]", log);
        Assert.Contains("[30]", log);
        Assert.Contains("[40]", log);
    }

    [Fact]
    public void TestInteractiveInputPrompts()
    {
        _input.EnqueueLine("John, 25");
        _interpreter.ExecuteInputLine("INPUT \"ENTER NAME AND AGE\"; NAME$, AGE%");

        Assert.Equal("John", _env.GetVariable("NAME$").AsString);
        Assert.Equal(25, _env.GetVariable("AGE%").AsInteger);
    }

    [Fact]
    public void TestLunarLanderSimulation()
    {
        _interpreter.ExecuteInputLine("10 ALT = 50: VEL = 10: FUEL = 50: GRAV = 5");
        _interpreter.ExecuteInputLine("20 WHILE ALT > 0");
        _interpreter.ExecuteInputLine("30   INPUT BURN");
        _interpreter.ExecuteInputLine("40   IF BURN > FUEL THEN BURN = FUEL");
        _interpreter.ExecuteInputLine("50   FUEL = FUEL - BURN");
        _interpreter.ExecuteInputLine("60   VEL = VEL + GRAV - (BURN * 0.4)");
        _interpreter.ExecuteInputLine("70   ALT = ALT - VEL");
        _interpreter.ExecuteInputLine("80 WEND");
        _interpreter.ExecuteInputLine("90 PRINT \"LANDED WITH VEL\"; VEL");

        _input.EnqueueLine("10");
        _input.EnqueueLine("10");
        _input.EnqueueLine("10");
        _input.EnqueueLine("10");
        _interpreter.Run();

        string log = _screen.GetOutputLog();
        Assert.Contains("LANDED WITH VEL", log);
    }

    [Fact]
    public void TestElizaSimulation()
    {
        _interpreter.ExecuteInputLine("10 REM ELIZA");
        _interpreter.ExecuteInputLine("20 INPUT \"> \"; USER$");
        _interpreter.ExecuteInputLine("30 IF USER$ = \"QUIT\" THEN PRINT \"ELIZA: GOODBYE.\": END");
        _interpreter.ExecuteInputLine("40 IF INSTR(USER$, \"MOTHER\") > 0 THEN PRINT \"ELIZA: FAMILY\": GOTO 20");
        _interpreter.ExecuteInputLine("50 GOTO 20");

        _input.EnqueueLine("MY MOTHER IS NICE");
        _input.EnqueueLine("QUIT");
        _interpreter.Run();

        string log = _screen.GetOutputLog();
        Assert.Contains("ELIZA: FAMILY", log);
        Assert.Contains("ELIZA: GOODBYE.", log);
    }
}
