using GWBASIC.Core.Common;

namespace GWBASIC.Core.Lexer;

public sealed record Token(TokenType Type, string Text, BasicValue? Value, int Position, int Line = 1)
{
    public override string ToString() =>
        Value.HasValue ? $"{Type}('{Text}', {Value.Value})" : $"{Type}('{Text}')";
}
