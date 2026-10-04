using System.Globalization;
using GWBASIC.Core.Common;

namespace GWBASIC.Core.Lexer;

public class BasicLexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        { "AUTO", TokenType.Auto },
        { "BEEP", TokenType.Beep },
        { "BLOAD", TokenType.Bload },
        { "BSAVE", TokenType.Bsave },
        { "CALL", TokenType.Call },
        { "CHAIN", TokenType.Chain },
        { "CIRCLE", TokenType.Circle },
        { "CLEAR", TokenType.Clear },
        { "CLOSE", TokenType.Close },
        { "CLS", TokenType.Cls },
        { "COLOR", TokenType.Color },
        { "COMMON", TokenType.Common },
        { "CONT", TokenType.Cont },
        { "DATA", TokenType.Data },
        { "DATE$", TokenType.DateStr },
        { "DEF", TokenType.Def },
        { "DEFDBL", TokenType.DefDbl },
        { "DEFINT", TokenType.DefInt },
        { "DEFSNG", TokenType.DefSng },
        { "DEFSTR", TokenType.DefStr },
        { "DELETE", TokenType.Delete },
        { "DIM", TokenType.Dim },
        { "DRAW", TokenType.Draw },
        { "EDIT", TokenType.Edit },
        { "ELSE", TokenType.Else },
        { "END", TokenType.End },
        { "ENVIRON", TokenType.Environ },
        { "ENVIRON$", TokenType.EnvironStr },
        { "ERASE", TokenType.Erase },
        { "ERROR", TokenType.Error },
        { "FIELD", TokenType.Field },
        { "FILES", TokenType.Files },
        { "FOR", TokenType.For },
        { "GET", TokenType.Get },
        { "GOSUB", TokenType.Gosub },
        { "GOTO", TokenType.Goto },
        { "IF", TokenType.If },
        { "INPUT", TokenType.Input },
        { "KEY", TokenType.Key },
        { "KILL", TokenType.Kill },
        { "LET", TokenType.Let },
        { "LINE", TokenType.Line },
        { "LIST", TokenType.List },
        { "LLIST", TokenType.Llist },
        { "LOAD", TokenType.Load },
        { "LOCATE", TokenType.Locate },
        { "LSET", TokenType.Lset },
        { "MERGE", TokenType.Merge },
        { "MID$", TokenType.MidStr },
        { "NAME", TokenType.Name },
        { "NEW", TokenType.New },
        { "NEXT", TokenType.Next },
        { "ON", TokenType.On },
        { "OPEN", TokenType.Open },
        { "OPTION", TokenType.Option },
        { "BASE", TokenType.Base },
        { "OUT", TokenType.Out },
        { "PAINT", TokenType.Paint },
        { "PLAY", TokenType.Play },
        { "POKE", TokenType.Poke },
        { "PRESET", TokenType.Preset },
        { "PSET", TokenType.Pset },
        { "PRINT", TokenType.Print },
        { "PUT", TokenType.Put },
        { "OUTPUT", TokenType.Output },
        { "APPEND", TokenType.Append },
        { "OFF", TokenType.Off },
        { "RANDOMIZE", TokenType.Randomize },
        { "READ", TokenType.Read },
        { "REM", TokenType.Rem },
        { "RENUM", TokenType.Renum },
        { "RESET", TokenType.Reset },
        { "RESTORE", TokenType.Restore },
        { "RESUME", TokenType.Resume },
        { "RETURN", TokenType.Return },
        { "RSET", TokenType.Rset },
        { "RUN", TokenType.Run },
        { "SAVE", TokenType.Save },
        { "SCREEN", TokenType.Screen },
        { "SEG", TokenType.Seg },
        { "SHELL", TokenType.Shell },
        { "SOUND", TokenType.Sound },
        { "SPC", TokenType.Spc },
        { "STEP", TokenType.Step },
        { "STOP", TokenType.Stop },
        { "SWAP", TokenType.Swap },
        { "SYSTEM", TokenType.System },
        { "TAB", TokenType.Tab },
        { "THEN", TokenType.Then },
        { "TIME$", TokenType.TimeStr },
        { "TIMER", TokenType.Timer },
        { "TO", TokenType.To },
        { "TROFF", TokenType.Troff },
        { "TRON", TokenType.Tron },
        { "USING", TokenType.Using },
        { "VIEW", TokenType.View },
        { "WAIT", TokenType.Wait },
        { "WEND", TokenType.Wend },
        { "WHILE", TokenType.While },
        { "WIDTH", TokenType.Width },
        { "WINDOW", TokenType.Window },
        { "WRITE", TokenType.Write },
        { "AS", TokenType.As },
        { "FN", TokenType.Fn },

        // Logical / Bitwise
        { "NOT", TokenType.Not },
        { "AND", TokenType.And },
        { "OR", TokenType.Or },
        { "XOR", TokenType.Xor },
        { "EQV", TokenType.Eqv },
        { "IMP", TokenType.Imp },
        { "MOD", TokenType.Mod },

        // Built-in functions
        { "ABS", TokenType.Abs },
        { "ASC", TokenType.Asc },
        { "ATN", TokenType.Atn },
        { "CDBL", TokenType.Cdbl },
        { "CHR$", TokenType.ChrStr },
        { "CINT", TokenType.Cint },
        { "COS", TokenType.Cos },
        { "CSNG", TokenType.Csng },
        { "CSRLIN", TokenType.Csrlin },
        { "CVD", TokenType.Cvd },
        { "CVI", TokenType.Cvi },
        { "CVS", TokenType.Cvs },
        { "EOF", TokenType.Eof },
        { "ERL", TokenType.Erl },
        { "ERR", TokenType.Err },
        { "EXP", TokenType.Exp },
        { "FIX", TokenType.Fix },
        { "FRE", TokenType.Fre },
        { "HEX$", TokenType.HexStr },
        { "INKEY$", TokenType.InkeyStr },
        { "INP", TokenType.Inp },
        { "INPUT$", TokenType.InputStr },
        { "INSTR", TokenType.Instr },
        { "INT", TokenType.Int },
        { "LEFT$", TokenType.LeftStr },
        { "LEN", TokenType.Len },
        { "LOC", TokenType.Loc },
        { "LOF", TokenType.Lof },
        { "LOG", TokenType.Log },
        { "LPOS", TokenType.Lpos },
        { "MKD$", TokenType.MkdStr },
        { "MKI$", TokenType.MkiStr },
        { "MKS$", TokenType.MksStr },
        { "OCT$", TokenType.OctStr },
        { "PEEK", TokenType.Peek },
        { "POINT", TokenType.Point },
        { "POS", TokenType.Pos },
        { "RIGHT$", TokenType.RightStr },
        { "RND", TokenType.Rnd },
        { "SGN", TokenType.Sgn },
        { "SIN", TokenType.Sin },
        { "SPACE$", TokenType.SpaceStr },
        { "SQR", TokenType.Sqr },
        { "STR$", TokenType.StrStr },
        { "STRING$", TokenType.StringStr },
        { "TAN", TokenType.Tan },
        { "VAL", TokenType.Val }
    };

    private readonly string _source;
    private int _pos;
    private int _line = 1;

    public BasicLexer(string source)
    {
        _source = source ?? "";
        _pos = 0;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (_pos < _source.Length)
        {
            char c = _source[_pos];

            // Skip whitespace (spaces and tabs, but keep newlines)
            if (c is ' ' or '\t' or '\r')
            {
                _pos++;
                continue;
            }

            if (c == '\n')
            {
                tokens.Add(new Token(TokenType.EndOfLine, "\n", null, _pos, _line));
                _pos++;
                _line++;
                continue;
            }

            // Comment starting with '
            if (c == '\'')
            {
                int start = _pos;
                // Treat as colon + rem, or rem token
                // In GW-BASIC ' is shorthand for : REM
                tokens.Add(new Token(TokenType.Colon, ":", null, start, _line));
                ReadUntilEndOfLine(tokens, start);
                continue;
            }

            // ? is shorthand for PRINT
            if (c == '?')
            {
                tokens.Add(new Token(TokenType.Print, "PRINT", null, _pos, _line));
                _pos++;
                continue;
            }

            // Operators & Delimiters
            if (c == ':')
            {
                tokens.Add(new Token(TokenType.Colon, ":", null, _pos++, _line));
                continue;
            }
            if (c == ',')
            {
                tokens.Add(new Token(TokenType.Comma, ",", null, _pos++, _line));
                continue;
            }
            if (c == ';')
            {
                tokens.Add(new Token(TokenType.Semicolon, ";", null, _pos++, _line));
                continue;
            }
            if (c == '(')
            {
                tokens.Add(new Token(TokenType.OpenParen, "(", null, _pos++, _line));
                continue;
            }
            if (c == ')')
            {
                tokens.Add(new Token(TokenType.CloseParen, ")", null, _pos++, _line));
                continue;
            }
            if (c == '#')
            {
                tokens.Add(new Token(TokenType.Hash, "#", null, _pos++, _line));
                continue;
            }
            if (c == '+')
            {
                tokens.Add(new Token(TokenType.Plus, "+", null, _pos++, _line));
                continue;
            }
            if (c == '-')
            {
                tokens.Add(new Token(TokenType.Minus, "-", null, _pos++, _line));
                continue;
            }
            if (c == '*')
            {
                tokens.Add(new Token(TokenType.Multiply, "*", null, _pos++, _line));
                continue;
            }
            if (c == '/')
            {
                tokens.Add(new Token(TokenType.Divide, "/", null, _pos++, _line));
                continue;
            }
            if (c == '\\')
            {
                tokens.Add(new Token(TokenType.IntegerDivide, "\\", null, _pos++, _line));
                continue;
            }
            if (c == '^')
            {
                tokens.Add(new Token(TokenType.Power, "^", null, _pos++, _line));
                continue;
            }
            if (c == '=')
            {
                tokens.Add(new Token(TokenType.Equal, "=", null, _pos++, _line));
                continue;
            }
            if (c == '<')
            {
                int start = _pos;
                _pos++;
                if (_pos < _source.Length && _source[_pos] == '>')
                {
                    _pos++;
                    tokens.Add(new Token(TokenType.NotEqual, "<>", null, start, _line));
                }
                else if (_pos < _source.Length && _source[_pos] == '=')
                {
                    _pos++;
                    tokens.Add(new Token(TokenType.LessEqual, "<=", null, start, _line));
                }
                else
                {
                    tokens.Add(new Token(TokenType.LessThan, "<", null, start, _line));
                }
                continue;
            }
            if (c == '>')
            {
                int start = _pos;
                _pos++;
                if (_pos < _source.Length && _source[_pos] == '=')
                {
                    _pos++;
                    tokens.Add(new Token(TokenType.GreaterEqual, ">=", null, start, _line));
                }
                else
                {
                    tokens.Add(new Token(TokenType.GreaterThan, ">", null, start, _line));
                }
                continue;
            }

            // String literals
            if (c == '"')
            {
                tokens.Add(ReadStringLiteral());
                continue;
            }

            // Hex and Octal numbers (&H..., &O..., &...)
            if (c == '&')
            {
                tokens.Add(ReadHexOrOctal());
                continue;
            }

            // Numbers
            if (char.IsAsciiDigit(c) || (c == '.' && _pos + 1 < _source.Length && char.IsAsciiDigit(_source[_pos + 1])))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            // Identifiers and Keywords
            if (char.IsAsciiLetter(c))
            {
                var tok = ReadIdentifierOrKeyword();
                tokens.Add(tok);

                // If token was REM, read rest of line
                if (tok.Type == TokenType.Rem)
                {
                    ReadUntilEndOfLine(tokens, _pos);
                }
                continue;
            }

            // Unknown character
            throw new BasicException(BasicErrorCode.SyntaxError, _line, $"Unexpected character '{c}'");
        }

        tokens.Add(new Token(TokenType.EndOfFile, "", null, _pos, _line));
        return tokens;
    }

    private void ReadUntilEndOfLine(List<Token> tokens, int start)
    {
        int p = _pos;
        while (p < _source.Length && _source[p] != '\n' && _source[p] != '\r')
        {
            p++;
        }
        string comment = _source[_pos..p];
        _pos = p;
        tokens.Add(new Token(TokenType.Rem, comment, BasicValue.FromString(comment), start, _line));
    }

    private Token ReadStringLiteral()
    {
        int start = _pos;
        _pos++; // skip open quote
        int contentStart = _pos;
        while (_pos < _source.Length && _source[_pos] != '"' && _source[_pos] != '\n' && _source[_pos] != '\r')
        {
            _pos++;
        }
        string text = _source[contentStart.._pos];
        if (_pos < _source.Length && _source[_pos] == '"')
        {
            _pos++; // skip close quote
        }
        return new Token(TokenType.StringLiteral, text, BasicValue.FromString(text), start, _line);
    }

    private Token ReadHexOrOctal()
    {
        int start = _pos;
        _pos++; // skip '&'
        bool isHex = false;

        if (_pos < _source.Length && (_source[_pos] == 'H' || _source[_pos] == 'h'))
        {
            isHex = true;
            _pos++;
        }
        else if (_pos < _source.Length && (_source[_pos] == 'O' || _source[_pos] == 'o'))
        {
            _pos++;
        }

        int digitsStart = _pos;
        if (isHex)
        {
            while (_pos < _source.Length && char.IsAsciiHexDigit(_source[_pos]))
            {
                _pos++;
            }
            string hexStr = _source[digitsStart.._pos];
            if (hexStr.Length == 0)
                throw new BasicException(BasicErrorCode.SyntaxError, _line, "Invalid hex literal");
            long val = Convert.ToInt64(hexStr, 16);
            short sVal = unchecked((short)val);
            return new Token(TokenType.IntegerLiteral, _source[start.._pos], BasicValue.FromInteger(sVal), start, _line);
        }
        else
        {
            while (_pos < _source.Length && _source[_pos] >= '0' && _source[_pos] <= '7')
            {
                _pos++;
            }
            string octStr = _source[digitsStart.._pos];
            if (octStr.Length == 0)
                throw new BasicException(BasicErrorCode.SyntaxError, _line, "Invalid octal literal");
            long val = Convert.ToInt64(octStr, 8);
            short sVal = unchecked((short)val);
            return new Token(TokenType.IntegerLiteral, _source[start.._pos], BasicValue.FromInteger(sVal), start, _line);
        }
    }

    private Token ReadNumber()
    {
        int start = _pos;
        bool hasDecimal = false;
        bool hasExp = false;
        char expChar = ' ';

        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (char.IsAsciiDigit(c))
            {
                _pos++;
            }
            else if (c == '.' && !hasDecimal && !hasExp)
            {
                hasDecimal = true;
                _pos++;
            }
            else if ((c is 'E' or 'e' or 'D' or 'd') && !hasExp)
            {
                hasExp = true;
                expChar = char.ToUpperInvariant(c);
                _pos++;
                if (_pos < _source.Length && (_source[_pos] == '+' || _source[_pos] == '-'))
                {
                    _pos++;
                }
            }
            else
            {
                break;
            }
        }

        // Optional type sigil at end of literal: %, !, #
        char sigil = ' ';
        if (_pos < _source.Length && _source[_pos] is '%' or '!' or '#')
        {
            sigil = _source[_pos++];
        }

        string text = _source[start.._pos];
        string parseText = text.TrimEnd('%', '!', '#');

        if (sigil == '%')
        {
            short sVal = (short)Math.Round(double.Parse(parseText, CultureInfo.InvariantCulture), MidpointRounding.ToEven);
            return new Token(TokenType.IntegerLiteral, text, BasicValue.FromInteger(sVal), start, _line);
        }
        if (sigil == '#' || expChar == 'D')
        {
            string dText = parseText.Replace('D', 'E').Replace('d', 'e');
            double dVal = double.Parse(dText, CultureInfo.InvariantCulture);
            return new Token(TokenType.DoubleLiteral, text, BasicValue.FromDouble(dVal), start, _line);
        }
        if (sigil == '!' || expChar == 'E' || hasDecimal)
        {
            float fVal = float.Parse(parseText, CultureInfo.InvariantCulture);
            return new Token(TokenType.SingleLiteral, text, BasicValue.FromSingle(fVal), start, _line);
        }

        // Plain digits
        if (long.TryParse(parseText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long lVal))
        {
            if (lVal is >= short.MinValue and <= short.MaxValue)
            {
                return new Token(TokenType.IntegerLiteral, text, BasicValue.FromInteger((short)lVal), start, _line);
            }
            if (lVal is >= int.MinValue and <= int.MaxValue)
            {
                return new Token(TokenType.SingleLiteral, text, BasicValue.FromSingle((float)lVal), start, _line);
            }
            return new Token(TokenType.DoubleLiteral, text, BasicValue.FromDouble((double)lVal), start, _line);
        }

        double fallback = double.Parse(parseText, CultureInfo.InvariantCulture);
        return new Token(TokenType.DoubleLiteral, text, BasicValue.FromDouble(fallback), start, _line);
    }

    private Token ReadIdentifierOrKeyword()
    {
        int start = _pos;
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (char.IsAsciiLetterOrDigit(c) || c == '.')
            {
                _pos++;
            }
            else
            {
                break;
            }
        }

        // Check if ends with type sigil %, !, #, $
        if (_pos < _source.Length && _source[_pos] is '%' or '!' or '#' or '$')
        {
            _pos++;
        }

        string text = _source[start.._pos];

        // Check for keyword match
        if (Keywords.TryGetValue(text, out TokenType type))
        {
            return new Token(type, text, null, start, _line);
        }

        // Special handling for FN function call: e.g. FNA, FNB%
        if (text.StartsWith("FN", StringComparison.OrdinalIgnoreCase) && text.Length > 2)
        {
            return new Token(TokenType.Identifier, text, null, start, _line);
        }

        return new Token(TokenType.Identifier, text, null, start, _line);
    }
}
