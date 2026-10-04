using GWBASIC.Core.Drivers;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class FileIoTests
{
    private readonly VirtualScreenDriver _screen;
    private readonly VirtualAudioDriver _audio;
    private readonly VirtualInputDriver _input;
    private readonly VirtualFileSystemDriver _fs;
    private readonly BasicEnvironment _env;
    private readonly Interpreter _interpreter;

    public FileIoTests()
    {
        _screen = new VirtualScreenDriver();
        _audio = new VirtualAudioDriver();
        _input = new VirtualInputDriver();
        _fs = new VirtualFileSystemDriver();
        _env = new BasicEnvironment(_screen, _audio, _input, _fs);
        _interpreter = new Interpreter(_env);
    }

    [Fact]
    public void TestSequentialFileWriteAndRead()
    {
        _interpreter.ExecuteInputLine("10 OPEN \"TEST.DAT\" FOR OUTPUT AS #1");
        _interpreter.ExecuteInputLine("20 PRINT #1, \"LINE ONE\"");
        _interpreter.ExecuteInputLine("30 PRINT #1, \"LINE TWO\"");
        _interpreter.ExecuteInputLine("40 CLOSE #1");
        _interpreter.ExecuteInputLine("50 OPEN \"TEST.DAT\" FOR INPUT AS #1");
        _interpreter.ExecuteInputLine("60 LINE INPUT #1, A$");
        _interpreter.ExecuteInputLine("70 LINE INPUT #1, B$");
        _interpreter.ExecuteInputLine("80 CLOSE #1");
        _interpreter.Run();

        Assert.Equal("LINE ONE", _env.GetVariable("A$").AsString);
        Assert.Equal("LINE TWO", _env.GetVariable("B$").AsString);
    }

    [Fact]
    public void TestRandomAccessFileFieldPutGet()
    {
        _interpreter.ExecuteInputLine("10 OPEN \"RECS.DAT\" AS #1 LEN = 32");
        _interpreter.ExecuteInputLine("20 FIELD #1, 20 AS N$, 2 AS A$");
        _interpreter.ExecuteInputLine("30 LSET N$ = \"ALICE\"");
        _interpreter.ExecuteInputLine("40 LSET A$ = MKI$(30)");
        _interpreter.ExecuteInputLine("50 PUT #1, 1");
        _interpreter.ExecuteInputLine("60 CLOSE #1");

        _interpreter.ExecuteInputLine("70 OPEN \"RECS.DAT\" AS #1 LEN = 32");
        _interpreter.ExecuteInputLine("80 FIELD #1, 20 AS RN$, 2 AS RA$");
        _interpreter.ExecuteInputLine("90 GET #1, 1");
        _interpreter.ExecuteInputLine("100 NAME$ = RN$");
        _interpreter.ExecuteInputLine("110 AGE% = CVI(RA$)");
        _interpreter.ExecuteInputLine("120 CLOSE #1");
        _interpreter.Run();

        Assert.StartsWith("ALICE", _env.GetVariable("RN$").AsString);
        Assert.Equal(30, _env.GetVariable("AGE%").AsInteger);
    }
}
