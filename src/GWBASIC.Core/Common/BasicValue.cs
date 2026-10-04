using System.Globalization;

namespace GWBASIC.Core.Common;

/// <summary>
/// Represents a value in GW-BASIC (Integer, Single, Double, or String).
/// Faithfully reproduces GW-BASIC type coercion, arithmetic, bitwise logic, and printing rules.
/// </summary>
public readonly struct BasicValue : IEquatable<BasicValue>
{
    public BasicType Type { get; }
    private readonly double _numValue;
    private readonly string? _strValue;

    private BasicValue(BasicType type, double numValue, string? strValue)
    {
        Type = type;
        _numValue = numValue;
        _strValue = strValue;
    }

    public static BasicValue FromInteger(short value) => new(BasicType.Integer, value, null);
    public static BasicValue FromInteger(int value)
    {
        if (value is >= short.MinValue and <= short.MaxValue)
            return new(BasicType.Integer, value, null);
        return new(BasicType.Single, value, null);
    }
    public static BasicValue FromSingle(float value) => new(BasicType.Single, value, null);
    public static BasicValue FromDouble(double value) => new(BasicType.Double, value, null);
    public static BasicValue FromString(string value) => new(BasicType.String, 0, value ?? "");

    public static readonly BasicValue True = FromInteger(-1);
    public static readonly BasicValue False = FromInteger(0);
    public static readonly BasicValue Zero = FromInteger(0);
    public static readonly BasicValue One = FromInteger(1);
    public static readonly BasicValue EmptyString = FromString("");

    public bool IsNumeric => Type != BasicType.String;
    public bool IsString => Type == BasicType.String;

    public short AsInteger
    {
        get
        {
            if (Type == BasicType.String)
                throw new BasicException(BasicErrorCode.TypeMismatch);
            double rounded = Math.Round(_numValue, MidpointRounding.ToEven);
            if (rounded is < short.MinValue or > short.MaxValue)
                throw new BasicException(BasicErrorCode.Overflow);
            return (short)rounded;
        }
    }

    public float AsSingle
    {
        get
        {
            if (Type == BasicType.String)
                throw new BasicException(BasicErrorCode.TypeMismatch);
            return (float)_numValue;
        }
    }

    public double AsDouble
    {
        get
        {
            if (Type == BasicType.String)
                throw new BasicException(BasicErrorCode.TypeMismatch);
            return _numValue;
        }
    }

    public string AsString
    {
        get
        {
            if (Type != BasicType.String)
                throw new BasicException(BasicErrorCode.TypeMismatch);
            return _strValue ?? "";
        }
    }

    public bool AsBoolean
    {
        get
        {
            if (Type == BasicType.String)
                throw new BasicException(BasicErrorCode.TypeMismatch);
            return Math.Abs(_numValue) > 1e-12;
        }
    }

    public BasicValue ConvertTo(BasicType targetType)
    {
        if (Type == targetType) return this;
        return targetType switch
        {
            BasicType.Integer => FromInteger(AsInteger),
            BasicType.Single => FromSingle(AsSingle),
            BasicType.Double => FromDouble(AsDouble),
            BasicType.String => throw new BasicException(BasicErrorCode.TypeMismatch),
            _ => throw new BasicException(BasicErrorCode.TypeMismatch)
        };
    }

    #region Arithmetic Operations

    public static BasicValue Add(BasicValue left, BasicValue right)
    {
        if (left.IsString && right.IsString)
        {
            string combined = left.AsString + right.AsString;
            if (combined.Length > 255)
                throw new BasicException(BasicErrorCode.StringTooLong);
            return FromString(combined);
        }

        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);

        BasicType resultType = PromotedType(left.Type, right.Type);
        if (resultType == BasicType.Double)
            return FromDouble(left.AsDouble + right.AsDouble);
        if (resultType == BasicType.Single)
            return FromSingle(left.AsSingle + right.AsSingle);

        long res = (long)left.AsInteger + right.AsInteger;
        if (res is < short.MinValue or > short.MaxValue)
            return FromSingle((float)res);
        return FromInteger((short)res);
    }

    public static BasicValue Subtract(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);

        BasicType resultType = PromotedType(left.Type, right.Type);
        if (resultType == BasicType.Double)
            return FromDouble(left.AsDouble - right.AsDouble);
        if (resultType == BasicType.Single)
            return FromSingle(left.AsSingle - right.AsSingle);

        long res = (long)left.AsInteger - right.AsInteger;
        if (res is < short.MinValue or > short.MaxValue)
            return FromSingle((float)res);
        return FromInteger((short)res);
    }

    public static BasicValue Multiply(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);

        BasicType resultType = PromotedType(left.Type, right.Type);
        if (resultType == BasicType.Double)
            return FromDouble(left.AsDouble * right.AsDouble);
        if (resultType == BasicType.Single)
            return FromSingle(left.AsSingle * right.AsSingle);

        long res = (long)left.AsInteger * right.AsInteger;
        if (res is < short.MinValue or > short.MaxValue)
            return FromSingle((float)res);
        return FromInteger((short)res);
    }

    public static BasicValue Divide(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);

        double denom = right.AsDouble;
        if (Math.Abs(denom) < double.Epsilon)
            throw new BasicException(BasicErrorCode.DivisionByZero);

        BasicType resultType = PromotedType(left.Type, right.Type);
        if (resultType == BasicType.Double)
            return FromDouble(left.AsDouble / denom);

        return FromSingle(left.AsSingle / right.AsSingle);
    }

    public static BasicValue IntegerDivide(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);

        short r = right.AsInteger;
        if (r == 0)
            throw new BasicException(BasicErrorCode.DivisionByZero);

        short l = left.AsInteger;
        return FromInteger((short)(l / r));
    }

    public static BasicValue Modulo(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);

        short r = right.AsInteger;
        if (r == 0)
            throw new BasicException(BasicErrorCode.DivisionByZero);

        short l = left.AsInteger;
        return FromInteger((short)(l % r));
    }

    public static BasicValue Power(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);

        double baseVal = left.AsDouble;
        double expVal = right.AsDouble;
        double res = Math.Pow(baseVal, expVal);
        if (double.IsNaN(res) || double.IsInfinity(res))
            throw new BasicException(BasicErrorCode.Overflow);

        if (left.Type == BasicType.Double || right.Type == BasicType.Double)
            return FromDouble(res);
        return FromSingle((float)res);
    }

    public static BasicValue Negate(BasicValue val)
    {
        if (val.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);

        return val.Type switch
        {
            BasicType.Integer => val.AsInteger == short.MinValue ? FromSingle(-val.AsInteger) : FromInteger((short)-val.AsInteger),
            BasicType.Single => FromSingle(-val.AsSingle),
            BasicType.Double => FromDouble(-val.AsDouble),
            _ => throw new BasicException(BasicErrorCode.TypeMismatch)
        };
    }

    #endregion

    #region Bitwise / Logical Operations

    public static BasicValue And(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);
        return FromInteger((short)(left.AsInteger & right.AsInteger));
    }

    public static BasicValue Or(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);
        return FromInteger((short)(left.AsInteger | right.AsInteger));
    }

    public static BasicValue Xor(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);
        return FromInteger((short)(left.AsInteger ^ right.AsInteger));
    }

    public static BasicValue Not(BasicValue val)
    {
        if (val.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);
        return FromInteger((short)(~val.AsInteger));
    }

    public static BasicValue Eqv(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);
        return FromInteger((short)(~(left.AsInteger ^ right.AsInteger)));
    }

    public static BasicValue Imp(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);
        return FromInteger((short)((ushort)~left.AsInteger | (ushort)right.AsInteger));
    }

    #endregion

    #region Relational Operations

    public static BasicValue Equal(BasicValue left, BasicValue right) =>
        Compare(left, right) == 0 ? True : False;

    public static BasicValue NotEqual(BasicValue left, BasicValue right) =>
        Compare(left, right) != 0 ? True : False;

    public static BasicValue LessThan(BasicValue left, BasicValue right) =>
        Compare(left, right) < 0 ? True : False;

    public static BasicValue GreaterThan(BasicValue left, BasicValue right) =>
        Compare(left, right) > 0 ? True : False;

    public static BasicValue LessThanOrEqual(BasicValue left, BasicValue right) =>
        Compare(left, right) <= 0 ? True : False;

    public static BasicValue GreaterThanOrEqual(BasicValue left, BasicValue right) =>
        Compare(left, right) >= 0 ? True : False;

    public static int Compare(BasicValue left, BasicValue right)
    {
        if (left.IsString && right.IsString)
            return string.CompareOrdinal(left.AsString, right.AsString);

        if (left.IsString || right.IsString)
            throw new BasicException(BasicErrorCode.TypeMismatch);

        return left.AsDouble.CompareTo(right.AsDouble);
    }

    #endregion

    private static BasicType PromotedType(BasicType t1, BasicType t2)
    {
        if (t1 == BasicType.Double || t2 == BasicType.Double) return BasicType.Double;
        if (t1 == BasicType.Single || t2 == BasicType.Single) return BasicType.Single;
        return BasicType.Integer;
    }

    public bool Equals(BasicValue other)
    {
        if (Type != other.Type) return false;
        if (Type == BasicType.String) return _strValue == other._strValue;
        return Math.Abs(_numValue - other._numValue) < double.Epsilon;
    }

    public override bool Equals(object? obj) => obj is BasicValue other && Equals(other);

    public override int GetHashCode() =>
        Type == BasicType.String ? HashCode.Combine(Type, _strValue) : HashCode.Combine(Type, _numValue);

    public static bool operator ==(BasicValue left, BasicValue right) => left.Equals(right);
    public static bool operator !=(BasicValue left, BasicValue right) => !left.Equals(right);

    /// <summary>
    /// Formats the value exactly as GW-BASIC's PRINT statement would.
    /// Numeric values: leading space if non-negative, trailing space for all.
    /// String values: no extra spaces.
    /// </summary>
    public string ToBasicPrintString()
    {
        if (Type == BasicType.String)
            return _strValue ?? "";

        string numStr = FormatBasicNumber();
        string prefix = _numValue >= 0 ? " " : "";
        return $"{prefix}{numStr} ";
    }

    /// <summary>
    /// Formats the number according to GW-BASIC precision and scientific notation rules (without surrounding spaces).
    /// </summary>
    public string FormatBasicNumber()
    {
        if (Type == BasicType.Integer)
        {
            return ((short)Math.Round(_numValue, MidpointRounding.ToEven)).ToString(CultureInfo.InvariantCulture);
        }

        if (Type == BasicType.Single)
        {
            float f = (float)_numValue;
            if (f == 0.0f) return "0";

            float absVal = Math.Abs(f);
            if (absVal is >= 1e7f or < 0.01f)
            {
                // Format in scientific notation: e.g. 1.23456E+08
                string sci = f.ToString("0.######E+00", CultureInfo.InvariantCulture);
                return sci;
            }

            // Standard decimal
            string dec = f.ToString("0.######", CultureInfo.InvariantCulture);
            return dec;
        }

        // Double precision
        double d = _numValue;
        if (d == 0.0) return "0";

        double absD = Math.Abs(d);
        if (absD is >= 1e16 or < 0.01)
        {
            string sci = d.ToString("0.###############E+00", CultureInfo.InvariantCulture);
            return sci.Replace("E", "D");
        }

        return d.ToString("0.###############", CultureInfo.InvariantCulture);
    }

    public override string ToString() => Type == BasicType.String ? (_strValue ?? "") : FormatBasicNumber();
}
