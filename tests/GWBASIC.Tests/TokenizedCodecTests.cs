using GWBASIC.Core.Drivers;
using GWBASIC.Core.IO;
using GWBASIC.Core.Runtime;
using Xunit;

namespace GWBASIC.Tests;

public class TokenizedCodecTests
{
    private readonly VirtualScreenDriver _screen;
    private readonly VirtualAudioDriver _audio;
    private readonly VirtualInputDriver _input;
    private readonly VirtualFileSystemDriver _fs;
    private readonly BasicEnvironment _env;
    private readonly Interpreter _interpreter;

    public TokenizedCodecTests()
    {
        _screen = new VirtualScreenDriver();
        _audio = new VirtualAudioDriver();
        _input = new VirtualInputDriver();
        _fs = new VirtualFileSystemDriver();
        _env = new BasicEnvironment(_screen, _audio, _input, _fs);
        _interpreter = new Interpreter(_env);
    }

    [Fact]
    public void TestTokenizedSaveAndLoadCycle()
    {
        _interpreter.ExecuteInputLine("10 FOR I = 1 TO 5");
        _interpreter.ExecuteInputLine("20 PRINT I");
        _interpreter.ExecuteInputLine("30 NEXT I");

        // Save in binary tokenized format (default SAVE without ,A)
        _interpreter.ExecuteInputLine("SAVE \"LOOP.BAS\"");

        byte[] savedBytes = _fs.ReadAllBytes("LOOP.BAS");
        Assert.True(TokenizedBasicCodec.IsTokenized(savedBytes));
        Assert.Equal(0xFF, savedBytes[0]);

        // Clear program and reload from tokenized file
        _interpreter.ExecuteInputLine("NEW");
        Assert.Equal(0, _env.Program.Count);

        _interpreter.ExecuteInputLine("LOAD \"LOOP.BAS\"");
        Assert.Equal(3, _env.Program.Count);

        var line10 = _env.Program.GetLine(10);
        Assert.NotNull(line10);
        Assert.Contains("FOR", line10.Text);
    }

    [Fact]
    public void TestProtectedProgramDecoding()
    {
        _interpreter.ExecuteInputLine("10 PRINT \"PROTECTED SECRET\"");
        _interpreter.ExecuteInputLine("20 END");

        byte[] tokenized = TokenizedBasicCodec.Encode(_env.Program.GetLines());

        // Encrypt with 11-byte XOR key schedule
        byte[] key = { 0xA5, 0x5A, 0xF3, 0x3F, 0xC6, 0x6C, 0x99, 0x99, 0x55, 0xAA, 0x00 };
        byte[] encrypted = new byte[tokenized.Length];
        encrypted[0] = 0xFE; // Protected magic byte
        for (int i = 1; i < tokenized.Length; i++)
        {
            encrypted[i] = (byte)(tokenized[i] ^ key[(i - 1) % key.Length]);
        }

        _fs.WriteAllBytes("SECRET.BAS", encrypted);

        _interpreter.ExecuteInputLine("NEW");
        Assert.Equal(0, _env.Program.Count);

        _interpreter.ExecuteInputLine("LOAD \"SECRET.BAS\"");
        Assert.Equal(2, _env.Program.Count);

        var line10 = _env.Program.GetLine(10);
        Assert.NotNull(line10);
        Assert.Contains("PROTECTED SECRET", line10.Text);
    }

    [Fact]
    public void TestBsaveAndBload()
    {
        _interpreter.ExecuteInputLine("DEF SEG = 0");
        _interpreter.ExecuteInputLine("POKE 100, 42");
        _interpreter.ExecuteInputLine("POKE 101, 84");
        _interpreter.ExecuteInputLine("BSAVE \"MEM.BIN\", 100, 2");

        // Clear memory
        _interpreter.ExecuteInputLine("POKE 100, 0");
        _interpreter.ExecuteInputLine("POKE 101, 0");
        Assert.Equal(0, _env.Peek(100));

        // Load back
        _interpreter.ExecuteInputLine("BLOAD \"MEM.BIN\", 100");
        Assert.Equal(42, _env.Peek(100));
        Assert.Equal(84, _env.Peek(101));
    }
}
