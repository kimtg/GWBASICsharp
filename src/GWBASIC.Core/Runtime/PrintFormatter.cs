using System.Globalization;
using System.Text;
using GWBASIC.Core.Common;

namespace GWBASIC.Core.Runtime;

public static class PrintFormatter
{
    public static string FormatUsing(string format, List<BasicValue> values)
    {
        var sb = new StringBuilder();
        int fPos = 0;
        int valIdx = 0;

        while (fPos < format.Length || valIdx < values.Count)
        {
            if (fPos >= format.Length)
            {
                if (valIdx >= values.Count) break;
                fPos = 0;
            }

            char c = format[fPos];

            if (valIdx >= values.Count && (c is '#' or '!' or '&' or '\\' or '*' or '$'))
            {
                break;
            }

                // Literal escape with '_'
                if (c == '_')
                {
                    fPos++;
                    if (fPos < format.Length) sb.Append(format[fPos++]);
                    continue;
                }

                // String field: '!' (1 character)
                if (c == '!')
                {
                    fPos++;
                    var val = values[valIdx++];
                    string s = val.AsString;
                    sb.Append(s.Length > 0 ? s[0] : ' ');
                    continue;
                }

                // String field: '&' (entire string)
                if (c == '&')
                {
                    fPos++;
                    var val = values[valIdx++];
                    sb.Append(val.AsString);
                    continue;
                }

                // String field: '\ ... \'
                if (c == '\\')
                {
                    int start = fPos++;
                    while (fPos < format.Length && format[fPos] == ' ') fPos++;
                    if (fPos < format.Length && format[fPos] == '\\')
                    {
                        fPos++;
                        int width = fPos - start;
                        var val = values[valIdx++];
                        string s = val.AsString;
                        if (s.Length >= width) sb.Append(s[..width]);
                        else sb.Append(s.PadRight(width));
                        continue;
                    }
                    else
                    {
                        fPos = start + 1;
                        sb.Append('\\');
                        continue;
                    }
                }

                // Numeric field: checks for #, +, -, **, $$, **$, comma, ^^^^
                if (c is '#' or '+' or '*' or '$')
                {
                    int start = fPos;
                    bool hasAsteriskFill = false;
                    bool hasDollar = false;
                    bool plusPrefix = false;
                    bool plusSuffix = false;
                    bool minusSuffix = false;
                    bool hasCommas = false;
                    bool hasExp = false;
                    int intDigits = 0;
                    int fracDigits = 0;
                    bool inFrac = false;

                    if (format.Substring(fPos).StartsWith("**$"))
                    {
                        hasAsteriskFill = true;
                        hasDollar = true;
                        fPos += 3;
                        intDigits += 2;
                    }
                    else if (format.Substring(fPos).StartsWith("**"))
                    {
                        hasAsteriskFill = true;
                        fPos += 2;
                        intDigits += 2;
                    }
                    else if (format.Substring(fPos).StartsWith("$$"))
                    {
                        hasDollar = true;
                        fPos += 2;
                        intDigits += 1;
                    }
                    else if (c == '+')
                    {
                        plusPrefix = true;
                        fPos++;
                    }

                    while (fPos < format.Length)
                    {
                        char ch = format[fPos];
                        if (ch == '#')
                        {
                            if (inFrac) fracDigits++; else intDigits++;
                            fPos++;
                        }
                        else if (ch == '.' && !inFrac)
                        {
                            inFrac = true;
                            fPos++;
                        }
                        else if (ch == ',' && !inFrac)
                        {
                            hasCommas = true;
                            fPos++;
                        }
                        else break;
                    }

                    if (fPos + 4 <= format.Length && format.Substring(fPos, 4) == "^^^^")
                    {
                        hasExp = true;
                        fPos += 4;
                    }

                    if (fPos < format.Length && format[fPos] == '+')
                    {
                        plusSuffix = true;
                        fPos++;
                    }
                    else if (fPos < format.Length && format[fPos] == '-')
                    {
                        minusSuffix = true;
                        fPos++;
                    }

                    if (intDigits > 0 || fracDigits > 0)
                    {
                        var val = values[valIdx++];
                        string formattedNum = FormatNumber(val, intDigits, fracDigits, hasCommas, hasAsteriskFill, hasDollar, plusPrefix, plusSuffix, minusSuffix, hasExp);
                        sb.Append(formattedNum);
                        continue;
                    }
                    else
                    {
                        fPos = start;
                        sb.Append(format[fPos++]);
                        continue;
                    }
                }

                sb.Append(c);
                fPos++;
        }

        return sb.ToString();
    }

    private static string FormatNumber(BasicValue val, int intDigits, int fracDigits, bool commas, bool asteriskFill, bool dollar, bool plusPrefix, bool plusSuffix, bool minusSuffix, bool exp)
    {
        double num = val.AsDouble;
        bool isNegative = num < 0;
        double absNum = Math.Abs(num);

        if (exp)
        {
            // Exponential format
            string expStr = absNum.ToString("0." + new string('0', fracDigits) + "E+00", CultureInfo.InvariantCulture);
            string sign = isNegative ? "-" : (plusPrefix ? "+" : " ");
            return sign + expStr;
        }

        double maxInt = Math.Pow(10, intDigits);
        if (absNum >= maxInt)
        {
            // Overflow: GW-BASIC prepends '%'
            return "%" + val.FormatBasicNumber();
        }

        // Standard decimal formatting
        string formatSpec = (commas ? "#,##0" : "0");
        if (fracDigits > 0)
        {
            formatSpec += "." + new string('0', fracDigits);
        }

        string formattedDigits = absNum.ToString(formatSpec, CultureInfo.InvariantCulture);

        string prefix = "";
        if (dollar) prefix = "$" + prefix;
        if (plusPrefix) prefix = (isNegative ? "-" : "+") + prefix;
        else if (isNegative && !plusSuffix && !minusSuffix) prefix = "-" + prefix;

        string suffix = "";
        if (plusSuffix) suffix = isNegative ? "-" : "+";
        else if (minusSuffix) suffix = isNegative ? "-" : " ";

        int totalWidth = intDigits + (fracDigits > 0 ? fracDigits + 1 : 0) + (commas ? (intDigits - 1) / 3 : 0) + prefix.Length + suffix.Length;
        string combined = prefix + formattedDigits + suffix;

        if (combined.Length < totalWidth)
        {
            char fill = asteriskFill ? '*' : ' ';
            combined = combined.PadLeft(totalWidth, fill);
        }

        return combined;
    }
}
