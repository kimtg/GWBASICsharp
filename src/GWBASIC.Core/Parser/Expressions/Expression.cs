using System.Globalization;
using System.Text;
using GWBASIC.Core.Common;
using GWBASIC.Core.Lexer;
using GWBASIC.Core.Runtime;

namespace GWBASIC.Core.Parser.Expressions;

public abstract class Expression
{
    public abstract BasicValue Evaluate(BasicEnvironment env);
}

public class LiteralExpression : Expression
{
    public BasicValue Value { get; }

    public LiteralExpression(BasicValue value)
    {
        Value = value;
    }

    public override BasicValue Evaluate(BasicEnvironment env) => Value;

    public override string ToString() => Value.ToString();
}

public class VariableExpression : Expression
{
    public string Name { get; }

    public VariableExpression(string name)
    {
        Name = name.ToUpperInvariant();
    }

    public override BasicValue Evaluate(BasicEnvironment env) => env.GetVariable(Name);

    public override string ToString() => Name;
}

public class ArrayAccessExpression : Expression
{
    public string Name { get; }
    public List<Expression> Indices { get; }

    public ArrayAccessExpression(string name, List<Expression> indices)
    {
        Name = name.ToUpperInvariant();
        Indices = indices;
    }

    public override BasicValue Evaluate(BasicEnvironment env)
    {
        int[] evalIndices = new int[Indices.Count];
        for (int i = 0; i < Indices.Count; i++)
        {
            evalIndices[i] = Indices[i].Evaluate(env).AsInteger;
        }
        return env.GetArrayElement(Name, evalIndices);
    }

    public override string ToString() => $"{Name}({string.Join(", ", Indices)})";
}

public class UnaryExpression : Expression
{
    public TokenType Operator { get; }
    public Expression Operand { get; }

    public UnaryExpression(TokenType op, Expression operand)
    {
        Operator = op;
        Operand = operand;
    }

    public override BasicValue Evaluate(BasicEnvironment env)
    {
        var val = Operand.Evaluate(env);
        return Operator switch
        {
            TokenType.Minus => BasicValue.Negate(val),
            TokenType.Plus => val,
            TokenType.Not => BasicValue.Not(val),
            _ => throw new BasicException(BasicErrorCode.SyntaxError, null, $"Unknown unary operator {Operator}")
        };
    }
}

public class BinaryExpression : Expression
{
    public TokenType Operator { get; }
    public Expression Left { get; }
    public Expression Right { get; }

    public BinaryExpression(TokenType op, Expression left, Expression right)
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    public override BasicValue Evaluate(BasicEnvironment env)
    {
        var l = Left.Evaluate(env);
        var r = Right.Evaluate(env);

        return Operator switch
        {
            TokenType.Plus => BasicValue.Add(l, r),
            TokenType.Minus => BasicValue.Subtract(l, r),
            TokenType.Multiply => BasicValue.Multiply(l, r),
            TokenType.Divide => BasicValue.Divide(l, r),
            TokenType.IntegerDivide => BasicValue.IntegerDivide(l, r),
            TokenType.Mod => BasicValue.Modulo(l, r),
            TokenType.Power => BasicValue.Power(l, r),

            TokenType.Equal => BasicValue.Equal(l, r),
            TokenType.NotEqual => BasicValue.NotEqual(l, r),
            TokenType.LessThan => BasicValue.LessThan(l, r),
            TokenType.GreaterThan => BasicValue.GreaterThan(l, r),
            TokenType.LessEqual => BasicValue.LessThanOrEqual(l, r),
            TokenType.GreaterEqual => BasicValue.GreaterThanOrEqual(l, r),

            TokenType.And => BasicValue.And(l, r),
            TokenType.Or => BasicValue.Or(l, r),
            TokenType.Xor => BasicValue.Xor(l, r),
            TokenType.Eqv => BasicValue.Eqv(l, r),
            TokenType.Imp => BasicValue.Imp(l, r),

            _ => throw new BasicException(BasicErrorCode.SyntaxError, null, $"Unknown binary operator {Operator}")
        };
    }
}

public class FunctionCallExpression : Expression
{
    public string Name { get; }
    public List<Expression> Arguments { get; }

    public FunctionCallExpression(string name, List<Expression> arguments)
    {
        Name = name.ToUpperInvariant();
        Arguments = arguments;
    }

    public override BasicValue Evaluate(BasicEnvironment env)
    {
        // Check for user-defined function DEF FN...
        if (Name.StartsWith("FN", StringComparison.OrdinalIgnoreCase))
        {
            var evaluatedArgs = Arguments.Select(a => a.Evaluate(env)).ToList();
            return env.CallFn(Name, evaluatedArgs);
        }

        return EvaluateBuiltIn(Name, Arguments, env);
    }

    private static BasicValue EvaluateBuiltIn(string name, List<Expression> args, BasicEnvironment env)
    {
        switch (name)
        {
            case "ABS":
                EnsureArgCount(args, 1);
                var vAbs = args[0].Evaluate(env);
                return vAbs.Type switch
                {
                    BasicType.Integer => BasicValue.FromInteger(Math.Abs(vAbs.AsInteger)),
                    BasicType.Single => BasicValue.FromSingle(Math.Abs(vAbs.AsSingle)),
                    BasicType.Double => BasicValue.FromDouble(Math.Abs(vAbs.AsDouble)),
                    _ => throw new BasicException(BasicErrorCode.TypeMismatch)
                };

            case "ASC":
                EnsureArgCount(args, 1);
                string sAsc = args[0].Evaluate(env).AsString;
                if (sAsc.Length == 0)
                    throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                return BasicValue.FromInteger((short)(byte)sAsc[0]);

            case "ATN":
                EnsureArgCount(args, 1);
                return BasicValue.FromSingle((float)Math.Atan(args[0].Evaluate(env).AsDouble));

            case "CDBL":
                EnsureArgCount(args, 1);
                return BasicValue.FromDouble(args[0].Evaluate(env).AsDouble);

            case "CHR$":
                EnsureArgCount(args, 1);
                int code = args[0].Evaluate(env).AsInteger;
                if (code is < 0 or > 255)
                    throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                return BasicValue.FromString(((char)code).ToString());

            case "CINT":
                EnsureArgCount(args, 1);
                return BasicValue.FromInteger(args[0].Evaluate(env).AsInteger);

            case "COS":
                EnsureArgCount(args, 1);
                return BasicValue.FromSingle((float)Math.Cos(args[0].Evaluate(env).AsDouble));

            case "CSNG":
                EnsureArgCount(args, 1);
                return BasicValue.FromSingle(args[0].Evaluate(env).AsSingle);

            case "CSRLIN":
                EnsureArgCount(args, 0);
                return BasicValue.FromInteger((short)env.Screen.CursorRow);

            case "EXP":
                EnsureArgCount(args, 1);
                return BasicValue.FromSingle((float)Math.Exp(args[0].Evaluate(env).AsDouble));

            case "FIX":
                EnsureArgCount(args, 1);
                var vFix = args[0].Evaluate(env);
                double dFix = vFix.AsDouble;
                return vFix.Type switch
                {
                    BasicType.Integer => vFix,
                    BasicType.Single => BasicValue.FromSingle((float)Math.Truncate(dFix)),
                    _ => BasicValue.FromDouble(Math.Truncate(dFix))
                };

            case "FRE":
                // In GW-BASIC, FRE(0) returns free memory in bytes
                return BasicValue.FromDouble(60300.0);

            case "HEX$":
                EnsureArgCount(args, 1);
                short hVal = args[0].Evaluate(env).AsInteger;
                return BasicValue.FromString(hVal.ToString("X"));

            case "INKEY$":
                EnsureArgCount(args, 0);
                string? key = env.Input.ReadInkey();
                return BasicValue.FromString(key ?? "");

            case "INSTR":
                if (args.Count == 2)
                {
                    string str = args[0].Evaluate(env).AsString;
                    string search = args[1].Evaluate(env).AsString;
                    if (search.Length == 0) return BasicValue.FromInteger(1);
                    int idx = str.IndexOf(search, StringComparison.Ordinal);
                    return BasicValue.FromInteger((short)(idx >= 0 ? idx + 1 : 0));
                }
                if (args.Count == 3)
                {
                    int start = args[0].Evaluate(env).AsInteger;
                    if (start <= 0) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                    string str = args[1].Evaluate(env).AsString;
                    string search = args[2].Evaluate(env).AsString;
                    if (start > str.Length) return BasicValue.FromInteger(0);
                    if (search.Length == 0) return BasicValue.FromInteger((short)start);
                    int idx = str.IndexOf(search, start - 1, StringComparison.Ordinal);
                    return BasicValue.FromInteger((short)(idx >= 0 ? idx + 1 : 0));
                }
                throw new BasicException(BasicErrorCode.SyntaxError);

            case "INT":
                EnsureArgCount(args, 1);
                var vInt = args[0].Evaluate(env);
                double dInt = vInt.AsDouble;
                return vInt.Type switch
                {
                    BasicType.Integer => vInt,
                    BasicType.Single => BasicValue.FromSingle((float)Math.Floor(dInt)),
                    _ => BasicValue.FromDouble(Math.Floor(dInt))
                };

            case "LEFT$":
                EnsureArgCount(args, 2);
                string sLeft = args[0].Evaluate(env).AsString;
                int nLeft = args[1].Evaluate(env).AsInteger;
                if (nLeft < 0) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                if (nLeft >= sLeft.Length) return BasicValue.FromString(sLeft);
                return BasicValue.FromString(sLeft[..nLeft]);

            case "LEN":
                EnsureArgCount(args, 1);
                return BasicValue.FromInteger((short)args[0].Evaluate(env).AsString.Length);

            case "LOG":
                EnsureArgCount(args, 1);
                double argLog = args[0].Evaluate(env).AsDouble;
                if (argLog <= 0) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                return BasicValue.FromSingle((float)Math.Log(argLog));

            case "MID$":
                if (args.Count is < 2 or > 3)
                    throw new BasicException(BasicErrorCode.SyntaxError);
                string sMid = args[0].Evaluate(env).AsString;
                int startMid = args[1].Evaluate(env).AsInteger;
                if (startMid <= 0) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                if (startMid > sMid.Length) return BasicValue.EmptyString;
                int lengthMid = args.Count == 3 ? args[2].Evaluate(env).AsInteger : sMid.Length - startMid + 1;
                if (lengthMid < 0) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                int actualLen = Math.Min(lengthMid, sMid.Length - startMid + 1);
                return BasicValue.FromString(sMid.Substring(startMid - 1, actualLen));

            case "OCT$":
                EnsureArgCount(args, 1);
                short oVal = args[0].Evaluate(env).AsInteger;
                return BasicValue.FromString(Convert.ToString(oVal, 8));

            case "PEEK":
                EnsureArgCount(args, 1);
                int addr = args[0].Evaluate(env).AsInteger;
                return BasicValue.FromInteger(env.Peek(addr));

            case "POINT":
                EnsureArgCount(args, 2);
                int px = args[0].Evaluate(env).AsInteger;
                int py = args[1].Evaluate(env).AsInteger;
                return BasicValue.FromInteger((short)env.Screen.Point(px, py));

            case "POS":
                EnsureArgCount(args, 1);
                return BasicValue.FromInteger((short)env.Screen.CursorCol);

            case "RIGHT$":
                EnsureArgCount(args, 2);
                string sRight = args[0].Evaluate(env).AsString;
                int nRight = args[1].Evaluate(env).AsInteger;
                if (nRight < 0) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                if (nRight >= sRight.Length) return BasicValue.FromString(sRight);
                return BasicValue.FromString(sRight[^nRight..]);

            case "RND":
                if (args.Count == 0) return env.GetRnd(1.0);
                return env.GetRnd(args[0].Evaluate(env).AsDouble);

            case "SGN":
                EnsureArgCount(args, 1);
                double dSgn = args[0].Evaluate(env).AsDouble;
                return BasicValue.FromInteger((short)Math.Sign(dSgn));

            case "SIN":
                EnsureArgCount(args, 1);
                return BasicValue.FromSingle((float)Math.Sin(args[0].Evaluate(env).AsDouble));

            case "SPACE$":
                EnsureArgCount(args, 1);
                int nSpace = args[0].Evaluate(env).AsInteger;
                if (nSpace < 0) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                return BasicValue.FromString(new string(' ', nSpace));

            case "SQR":
                EnsureArgCount(args, 1);
                double dSqr = args[0].Evaluate(env).AsDouble;
                if (dSqr < 0) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                return BasicValue.FromSingle((float)Math.Sqrt(dSqr));

            case "STR$":
                EnsureArgCount(args, 1);
                var vStr = args[0].Evaluate(env);
                if (vStr.IsString) throw new BasicException(BasicErrorCode.TypeMismatch);
                // In GW-BASIC, STR$(X) includes leading space if non-negative!
                string formatted = vStr.FormatBasicNumber();
                return BasicValue.FromString(vStr.AsDouble >= 0 ? " " + formatted : formatted);

            case "STRING$":
                EnsureArgCount(args, 2);
                int nRepeat = args[0].Evaluate(env).AsInteger;
                if (nRepeat < 0) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                var secondArg = args[1].Evaluate(env);
                char fillChar = secondArg.IsString ? (secondArg.AsString.Length > 0 ? secondArg.AsString[0] : ' ') : (char)(byte)secondArg.AsInteger;
                return BasicValue.FromString(new string(fillChar, nRepeat));

            case "TAN":
                EnsureArgCount(args, 1);
                return BasicValue.FromSingle((float)Math.Tan(args[0].Evaluate(env).AsDouble));

            case "TIMER":
                EnsureArgCount(args, 0);
                var now = DateTime.Now;
                double seconds = now.Hour * 3600 + now.Minute * 60 + now.Second + now.Millisecond / 1000.0;
                return BasicValue.FromSingle((float)seconds);

            case "VAL":
                EnsureArgCount(args, 1);
                string sVal = args[0].Evaluate(env).AsString.TrimStart();
                if (string.IsNullOrEmpty(sVal)) return BasicValue.Zero;
                return ParseBasicVal(sVal);

            case "EOF":
                EnsureArgCount(args, 1);
                int eofNum = args[0].Evaluate(env).AsInteger;
                return env.IsEof(eofNum) ? BasicValue.True : BasicValue.False;

            case "LOF":
                EnsureArgCount(args, 1);
                int lofNum = args[0].Evaluate(env).AsInteger;
                return BasicValue.FromDouble(env.GetLof(lofNum));

            case "LOC":
                EnsureArgCount(args, 1);
                int locNum = args[0].Evaluate(env).AsInteger;
                return BasicValue.FromDouble(env.GetLoc(locNum));

            case "ERR":
                EnsureArgCount(args, 0);
                return BasicValue.FromInteger((short)env.LastErrorCode);

            case "ERL":
                EnsureArgCount(args, 0);
                return BasicValue.FromInteger((short)env.LastErrorLine);

            case "DATE$":
                EnsureArgCount(args, 0);
                return BasicValue.FromString(DateTime.Now.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture));

            case "TIME$":
                EnsureArgCount(args, 0);
                return BasicValue.FromString(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));

            case "ENVIRON$":
                EnsureArgCount(args, 1);
                string envName = args[0].Evaluate(env).AsString;
                string? envVal = Environment.GetEnvironmentVariable(envName);
                return BasicValue.FromString(envVal ?? "");

            // Binary conversions for FIELD
            case "MKI$":
                EnsureArgCount(args, 1);
                short mki = args[0].Evaluate(env).AsInteger;
                return BasicValue.FromString(Encoding.Latin1.GetString(BitConverter.GetBytes(mki)));

            case "CVI":
                EnsureArgCount(args, 1);
                byte[] bytesCvi = Encoding.Latin1.GetBytes(args[0].Evaluate(env).AsString);
                if (bytesCvi.Length < 2) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                return BasicValue.FromInteger(BitConverter.ToInt16(bytesCvi, 0));

            case "MKS$":
                EnsureArgCount(args, 1);
                float mks = args[0].Evaluate(env).AsSingle;
                return BasicValue.FromString(Encoding.Latin1.GetString(BitConverter.GetBytes(mks)));

            case "CVS":
                EnsureArgCount(args, 1);
                byte[] bytesCvs = Encoding.Latin1.GetBytes(args[0].Evaluate(env).AsString);
                if (bytesCvs.Length < 4) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                return BasicValue.FromSingle(BitConverter.ToSingle(bytesCvs, 0));

            case "MKD$":
                EnsureArgCount(args, 1);
                double mkd = args[0].Evaluate(env).AsDouble;
                return BasicValue.FromString(Encoding.Latin1.GetString(BitConverter.GetBytes(mkd)));

            case "CVD":
                EnsureArgCount(args, 1);
                byte[] bytesCvd = Encoding.Latin1.GetBytes(args[0].Evaluate(env).AsString);
                if (bytesCvd.Length < 8) throw new BasicException(BasicErrorCode.IllegalFunctionCall);
                return BasicValue.FromDouble(BitConverter.ToDouble(bytesCvd, 0));

            default:
                throw new BasicException(BasicErrorCode.SyntaxError, null, $"Unknown function {name}");
        }
    }

    private static BasicValue ParseBasicVal(string str)
    {
        // GW-BASIC VAL parses numbers until non-numeric character
        int len = 0;
        bool hasDecimal = false;
        bool hasExp = false;
        if (str[0] is '+' or '-') len++;
        while (len < str.Length)
        {
            char c = str[len];
            if (char.IsAsciiDigit(c)) len++;
            else if (c == '.' && !hasDecimal && !hasExp) { hasDecimal = true; len++; }
            else if ((c is 'E' or 'e' or 'D' or 'd') && !hasExp)
            {
                hasExp = true;
                len++;
                if (len < str.Length && (str[len] is '+' or '-')) len++;
            }
            else break;
        }
        string sub = str[..len];
        if (string.IsNullOrEmpty(sub) || sub == "+" || sub == "-") return BasicValue.Zero;
        sub = sub.Replace('D', 'E').Replace('d', 'e');
        if (double.TryParse(sub, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
        {
            return BasicValue.FromDouble(d);
        }
        return BasicValue.Zero;
    }

    private static void EnsureArgCount(List<Expression> args, int count)
    {
        if (args.Count != count)
            throw new BasicException(BasicErrorCode.SyntaxError, null, $"Expected {count} arguments, got {args.Count}");
    }
}
